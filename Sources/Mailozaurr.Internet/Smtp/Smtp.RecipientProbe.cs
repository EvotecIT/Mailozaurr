using System.Net.Security;
using System.Net.Sockets;

namespace Mailozaurr;

public partial class Smtp {
    private const int RecipientProbeTimeoutMilliseconds = 10000;

    /// <summary>
    /// Tests whether an SMTP server accepts a recipient at envelope stage without sending message DATA.
    /// </summary>
    /// <param name="server">SMTP server name.</param>
    /// <param name="port">Port number.</param>
    /// <param name="recipient">Recipient address used in RCPT TO.</param>
    /// <param name="sender">Envelope sender address used in MAIL FROM.</param>
    /// <param name="heloHost">EHLO/HELO name sent by the probe.</param>
    /// <param name="secureSocketOptions">Controls SSL/TLS usage.</param>
    /// <param name="useSsl">Compatibility flag overriding <paramref name="secureSocketOptions"/> when set.</param>
    public static SmtpRecipientProbeInfo TestRecipient(
        string server,
        int port,
        string recipient,
        string sender = "probe@example.com",
        string heloHost = "localhost",
        SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto,
        bool useSsl = false) {
        if (!SmtpValidation.TryValidateServer(server, port, out var validationError)) {
            return new SmtpRecipientProbeInfo(server ?? string.Empty, port, heloHost, sender, recipient, null, false, null, null, null, null, false, validationError);
        }

        if (string.IsNullOrWhiteSpace(recipient)) {
            return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient ?? string.Empty, null, false, null, null, null, null, false, "Recipient address is required.");
        }

        if (string.IsNullOrWhiteSpace(sender)) {
            return new SmtpRecipientProbeInfo(server, port, heloHost, sender ?? string.Empty, recipient, null, false, null, null, null, null, false, "Sender address is required.");
        }

        if (string.IsNullOrWhiteSpace(heloHost)) {
            heloHost = "localhost";
        }

        var commandValueError = ValidateSmtpCommandValue(nameof(heloHost), heloHost)
            ?? ValidateSmtpCommandValue(nameof(sender), sender)
            ?? ValidateSmtpCommandValue(nameof(recipient), recipient);
        if (commandValueError != null) {
            return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, null, false, null, null, null, null, false, commandValueError);
        }

        string? banner = null;
        string? mailFromResponse = null;
        string? recipientResponse = null;
        var startTlsUsed = false;

        try {
            using var tcpClient = new TcpClient();
            tcpClient.ReceiveTimeout = RecipientProbeTimeoutMilliseconds;
            tcpClient.SendTimeout = RecipientProbeTimeoutMilliseconds;
            var connectTask = tcpClient.ConnectAsync(server, port);
            if (!connectTask.Wait(RecipientProbeTimeoutMilliseconds)) {
                return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, null, false, null, null, null, null, false, "Connection timed out after " + RecipientProbeTimeoutMilliseconds + " ms.");
            }

            if (connectTask.IsFaulted) {
                throw connectTask.Exception?.GetBaseException() ?? new SocketException();
            }

            Stream stream = tcpClient.GetStream();
            var effectiveOptions = secureSocketOptions;
            if (useSsl && effectiveOptions == SecureSocketOptions.Auto) {
                effectiveOptions = SecureSocketOptions.StartTls;
            }

            if (effectiveOptions == SecureSocketOptions.SslOnConnect
                || (effectiveOptions == SecureSocketOptions.Auto && port == 465)) {
                stream = CreateAuthenticatedSslStream(stream, server);
                startTlsUsed = true;
            }

            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
            using var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: false) {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            banner = ReadSmtpResponse(reader);
            if (!TryGreetSmtpServer(writer, reader, heloHost, out var greetingResponse)) {
                return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, false, null, null, null, null, false, greetingResponse);
            }

            if (!startTlsUsed && ShouldUseStartTls(effectiveOptions, greetingResponse)) {
                WriteSmtpCommand(writer, "STARTTLS");
                var startTlsResponse = ReadSmtpResponse(reader);
                var startTlsCode = ParseSmtpStatusCode(startTlsResponse);
                if (startTlsCode is < 200 or >= 400) {
                    return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, false, null, null, null, null, false, startTlsResponse);
                }

                stream = CreateAuthenticatedSslStream(stream, server);
                startTlsUsed = true;
                using var tlsReader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                using var tlsWriter = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: false) {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                if (!TryGreetSmtpServer(tlsWriter, tlsReader, heloHost, out var tlsGreetingResponse)) {
                    return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, true, null, null, null, null, false, tlsGreetingResponse);
                }

                return ProbeRecipientEnvelope(server, port, heloHost, sender, recipient, banner, startTlsUsed, tlsReader, tlsWriter);
            }

            if (!startTlsUsed && effectiveOptions == SecureSocketOptions.StartTls) {
                return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, false, null, null, null, null, false, "STARTTLS was required but not advertised.");
            }

            return ProbeRecipientEnvelope(server, port, heloHost, sender, recipient, banner, startTlsUsed, reader, writer);
        } catch (Exception ex) {
            return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, startTlsUsed, ParseSmtpStatusCode(mailFromResponse), mailFromResponse, ParseSmtpStatusCode(recipientResponse), recipientResponse, false, ex.Message);
        }
    }

    private static bool TryGreetSmtpServer(StreamWriter writer, StreamReader reader, string heloHost, out string? response) {
        WriteSmtpCommand(writer, "EHLO " + heloHost);
        response = ReadSmtpResponse(reader);
        var statusCode = ParseSmtpStatusCode(response);
        if (statusCode is >= 500 and < 600) {
            WriteSmtpCommand(writer, "HELO " + heloHost);
            response = ReadSmtpResponse(reader);
            statusCode = ParseSmtpStatusCode(response);
        }

        return statusCode is >= 200 and < 400;
    }

    private static SmtpRecipientProbeInfo ProbeRecipientEnvelope(
        string server,
        int port,
        string heloHost,
        string sender,
        string recipient,
        string? banner,
        bool startTlsUsed,
        StreamReader reader,
        StreamWriter writer) {
        WriteSmtpCommand(writer, "MAIL FROM:<" + sender + ">");
        var mailFromResponse = ReadSmtpResponse(reader);
        var mailFromCode = ParseSmtpStatusCode(mailFromResponse);

        WriteSmtpCommand(writer, "RCPT TO:<" + recipient + ">");
        var recipientResponse = ReadSmtpResponse(reader);
        var recipientCode = ParseSmtpStatusCode(recipientResponse);

        try {
            WriteSmtpCommand(writer, "RSET");
            _ = ReadSmtpResponse(reader);
            WriteSmtpCommand(writer, "QUIT");
            _ = ReadSmtpResponse(reader);
        } catch (IOException) {
            // The probe result is already known; best-effort cleanup should not hide it.
        }

        var accepted = mailFromCode is >= 200 and < 300
            && (recipientCode is >= 200 and < 300 || recipientCode == 251 || recipientCode == 252);

        return new SmtpRecipientProbeInfo(server, port, heloHost, sender, recipient, banner, startTlsUsed, mailFromCode, mailFromResponse, recipientCode, recipientResponse, accepted, null);
    }

    private static SslStream CreateAuthenticatedSslStream(Stream stream, string server) {
        var sslStream = new SslStream(stream, false);
        sslStream.AuthenticateAsClient(server);
        return sslStream;
    }

    private static bool ShouldUseStartTls(SecureSocketOptions options, string? ehloResponse) {
        if (options != SecureSocketOptions.Auto
            && options != SecureSocketOptions.StartTls
            && options != SecureSocketOptions.StartTlsWhenAvailable) {
            return false;
        }

        return ehloResponse?.IndexOf("STARTTLS", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void WriteSmtpCommand(StreamWriter writer, string command) {
        writer.WriteLine(command);
    }

    private static string? ValidateSmtpCommandValue(string name, string value) {
        foreach (var character in value) {
            if (character == '\r' || character == '\n') {
                return name + " must not contain CR or LF characters.";
            }
        }

        return null;
    }

    private static string? ReadSmtpResponse(StreamReader reader) {
        var lines = new List<string>();
        var line = reader.ReadLine();
        if (line == null) {
            return null;
        }

        lines.Add(line);
        var code = line.Length >= 3 ? line.Substring(0, 3) : string.Empty;
        while (line.Length >= 4 && line[3] == '-') {
            line = reader.ReadLine();
            if (line == null) {
                break;
            }

            lines.Add(line);
            if (line.StartsWith(code + " ", StringComparison.Ordinal)) {
                break;
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int? ParseSmtpStatusCode(string? response) {
        if (response != null && response.Length >= 3 && int.TryParse(response.Substring(0, 3), out var code)) {
            return code;
        }

        return null;
    }
}
