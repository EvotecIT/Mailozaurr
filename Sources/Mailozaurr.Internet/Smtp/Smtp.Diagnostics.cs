using System.Threading;

namespace Mailozaurr;

public partial class Smtp {
    internal bool SuppressDiagnostics { get; set; }

    /// <summary>Composes and sends the configured message while suppressing sensitive diagnostics.</summary>
    /// <param name="cancellationToken">Token used to cancel composition or sending.</param>
    /// <returns>The send result with provider messages and post-send error details removed.</returns>
    /// <remarks>
    /// Uses the existing session without connecting, authenticating or disposing it. Restores diagnostic
    /// settings and the webhook URL on exit. Webhooks receive only the sanitized result.
    /// </remarks>
    public async Task<SmtpResult> CreateAndSendSafelyAsync(CancellationToken cancellationToken = default) {
        using var diagnostics = new SmtpDiagnosticScope(this);
        string? webhookUrl = WebhookUrl;
        WebhookUrl = null;
        try {
            await CreateMessageAsync(cancellationToken).ConfigureAwait(false);
            SmtpResult result = await SendAsync(cancellationToken).ConfigureAwait(false);
            result.Message = string.Empty;
            result.Error = result.Status ? string.Empty : "SMTP send failed.";
            if (result.PostSendErrors.Count > 0) {
                result.PostSendErrors = new List<string> { "SMTP post-send operation failed." };
            }
            await RecordPostSendErrorAsync(result, "Webhook notification", () => Helpers.PostWebhookAsync(webhookUrl, result,
                MailozaurrJsonContext.Default.SmtpResult, cancellationToken, throwOnFailure: true)).ConfigureAwait(false);
            if (result.PostSendErrors.Count > 0) {
                result.PostSendErrors = new List<string> { "SMTP post-send operation failed." };
            }
            return result;
        } catch (OperationCanceledException exception) {
            throw new OperationCanceledException("SMTP operation canceled.", exception.CancellationToken);
        } catch {
            throw new InvalidOperationException("SMTP send failed.");
        } finally {
            WebhookUrl = webhookUrl;
        }
    }
}
