namespace Mailozaurr;

public class ValidatedEmail {
    public string EmailAddress { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Error { get; set; } = string.Empty;
}