using System;

namespace Mailozaurr;

internal static class StringCompatibilityExtensions {
    internal static bool Contains(this string source, string value, StringComparison comparisonType) =>
        source.IndexOf(value, comparisonType) >= 0;
}
