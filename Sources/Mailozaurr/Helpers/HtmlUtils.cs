using System.Text.RegularExpressions;

namespace Mailozaurr;

/// <summary>
/// Helper utilities for working with HTML content.
/// </summary>
public static class HtmlUtils {
    /// <summary>
    /// Replaces local image <c>src</c> references with <c>cid:</c> links and returns the
    /// updated HTML and a collection of the embedded file paths.
    /// </summary>
    /// <param name="html">HTML content that may contain local image paths.</param>
    /// <returns>The updated HTML and list of file paths that were replaced.</returns>
    public static (string Html, List<string> Paths) ExtractLocalImagePaths(string html) {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(html)) return (html, paths);

        string pattern = "<img[^>]+src=[\"']([^\"']+)[\"']";
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase)) {
            var path = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (File.Exists(path)) {
                var fileName = Path.GetFileName(path);
                html = html.Replace(path, $"cid:{fileName}");
                paths.Add(path);
            }
        }
        return (html, paths);
    }
}
