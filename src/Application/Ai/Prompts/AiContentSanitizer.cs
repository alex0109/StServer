using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Ai.Prompts;

// Prepares untrusted user content before it reaches a model
public static partial class AiContentSanitizer
{
    // Random tag per request: users can't guess it, so they can't close it to escape the data block
    public static string NewContentTag() =>
        "user_content_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();

    public static string Wrap(string contentTag, string content) =>
        $"<{contentTag}>\n{content}\n</{contentTag}>";

    // Removes control characters and anything that looks like our data tags, then limits the length
    public static string Clean(string? text, int maxLength, out bool isTruncated)
    {
        isTruncated = false;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsControl(c) || c is '\n' or '\t') cleaned.Append(c);
        }

        var result = DataTagRegex().Replace(cleaned.ToString(), string.Empty);
        result = ExtraBlankLinesRegex().Replace(result, "\n\n").Trim();

        if (result.Length > maxLength)
        {
            isTruncated = true;
            result = result[..maxLength];
        }

        return result;
    }

    public static string Clean(string? text, int maxLength) => Clean(text, maxLength, out _);

    [GeneratedRegex(@"</?\s*user_content[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DataTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLinesRegex();
}
