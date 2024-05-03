namespace Mailozaurr;

public class SmtpResult {
    public bool Status { get; set; }
    public string SentTo { get; set; }
    public string SentFrom { get; set; }
    public string? Message { get; set; }
    public TimeSpan TimeToExecute { get; set; }
    public string Server { get; set; }
    public int Port { get; set; }

    public SmtpResult(bool status, string sentTo, string sentFrom, string server, int port, TimeSpan timeToExecute, string? outputMessage = null) {
        Status = status;

        SentTo = sentTo;
        SentFrom = sentFrom;
        Server = server;
        Port = port;
        TimeToExecute = timeToExecute;
        Message = outputMessage;
    }
}
