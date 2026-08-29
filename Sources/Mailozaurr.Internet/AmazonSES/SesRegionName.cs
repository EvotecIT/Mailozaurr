namespace Mailozaurr;

/// <summary>Validates the DNS-label portion used by regional Amazon SES endpoints.</summary>
internal static class SesRegionName {
    internal static bool IsValid(string? region) {
        if (string.IsNullOrWhiteSpace(region) || region!.Length > 63) return false;
        if (!IsLowerAlphaNumeric(region[0]) || !IsLowerAlphaNumeric(region[region.Length - 1])) {
            return false;
        }

        foreach (char character in region) {
            if (!IsLowerAlphaNumeric(character) && character != '-') return false;
        }
        return true;
    }

    internal static void Validate(string? region, string parameterName) {
        if (!IsValid(region)) {
            throw new ArgumentException(
                "Amazon SES region names must contain only lowercase ASCII letters, digits, and interior hyphens.",
                parameterName);
        }
    }

    private static bool IsLowerAlphaNumeric(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';
}
