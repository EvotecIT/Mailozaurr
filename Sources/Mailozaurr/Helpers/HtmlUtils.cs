using System.Text.RegularExpressions;

namespace Mailozaurr;

public static class HtmlUtils {
    public static (string Html, List<string> Paths) ExtractLocalImagePaths(string html) {
        var paths = new List<string>();
        if (string.IsNullOrEmpty(html)) return (html, paths);

        string pattern = "<img[^>]+src=[\"']([^\"']+)[\"']";
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase)) {
            var path = match.Groups[1].Value;
            if (string.IsNullOrEmpty(path)) continue;
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
