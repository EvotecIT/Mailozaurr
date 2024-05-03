namespace Mailozaurr;

public static class Validator {
    public static ValidatedEmail ValidateEmail(string emailAddress, bool allowInternational = false, bool allowTopLevelDomains = false) {
        try {
            var isValid = EmailValidation.EmailValidator.Validate(emailAddress, allowTopLevelDomains, allowInternational);
            return new ValidatedEmail {
                EmailAddress = emailAddress,
                IsValid = isValid,
                Error = ""
            };
        } catch (Exception ex) {
            return new ValidatedEmail {
                EmailAddress = emailAddress,
                IsValid = false,
                Error = ex.Message
            };
        }
    }
}