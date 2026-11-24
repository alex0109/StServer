using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Application.DTOs.RichTextDocument;

// Editor JSON comes from the browser and is shown to other people too: shared copies, the library and
// the admin panel. Attributes are checked here, on every write and every read, so a link or an image can
// only point to the web (never "javascript:"), and nothing that looks like an event handler survives.
// The editor's own formatting (levels, alignment, colours, sizes) is left as it is
public static partial class RichTextSanitizer
{
    // Attributes a browser follows or loads
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "url", "action", "formaction", "xlink:href", "poster", "cite", "background",
        "data", "codebase", "dynsrc", "lowsrc", "ping", "manifest",
    };

    // Several addresses or a whole document in one value: the editor doesn't need them
    private static readonly HashSet<string> DroppedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "srcset", "srcdoc",
    };

    private static readonly HashSet<string> SafeSchemes = new(StringComparer.Ordinal)
    {
        "http", "https", "mailto", "tel",
    };

    private const int MaxAttributeNameLength = 64;
    private const int MaxNestedDepth = 16;
    // Web links; inline images are checked by their content instead
    private const int MaxUrlLength = 8192;
    private const int SchemeWindow = 128;

    public static RichTextDocument? Sanitize(RichTextDocument? document)
    {
        if (document is null) return null;

        document.Content = SanitizeNodes(document.Content) ?? [];
        return document;
    }

    // An address the editor may keep: a web, mail or phone link, a relative one, or an inline raster image
    public static bool IsSafeUrl(string? url)
    {
        if (url is null) return true;

        // Only the scheme decides, and it is at the start: a real one is never this long
        var head = Compact(url, SchemeWindow);
        if (head.Length == 0) return true;

        // "&#106;avascript:" or "javascript&colon;" is harmless as an attribute, but turns into a script
        // link the moment some renderer builds HTML from strings
        if (HasCharacterReference(head)) return false;

        // "/page", "#part", "?q", "./x": stays on the site that shows it
        if (head[0] is '/' or '#' or '?' or '.') return true;

        var colon = head.IndexOf(':');
        var delimiter = head.IndexOfAny(['/', '?', '#']);
        // No scheme at all ("page.html"), or the colon comes after the path started
        if (colon < 0 || (delimiter >= 0 && delimiter < colon)) return true;

        var scheme = head[..colon];
        if (SafeSchemes.Contains(scheme)) return url.Length <= MaxUrlLength;

        return scheme == "data" && IsInlineRasterImage(url, head);
    }

    // "data:image/png;base64,...": a picture pasted into the editor. SVG is not one of them, it can carry script
    private static bool IsInlineRasterImage(string url, string head)
    {
        if (!SafeDataImagePrefix().IsMatch(head)) return false;

        var start = url.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;

        for (var i = start + "base64,".Length; i < url.Length; i++)
        {
            var c = url[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=' || char.IsWhiteSpace(c))) return false;
        }

        return true;
    }

    private static List<Node>? SanitizeNodes(List<Node>? nodes)
    {
        if (nodes is null) return null;

        var kept = new List<Node>(nodes.Count);
        foreach (var node in nodes)
        {
            // A type the editor can't have ("<img onerror=...>" as a name) drops the node with its content
            if (node is null || !IsTypeName(node.Type)) continue;

            node.Attrs = SanitizeAttributes(node.Attrs);
            node.Marks = node.Marks?
                .Where(mark => mark is not null && IsTypeName(mark.Type))
                .Select(mark =>
                {
                    mark.Attrs = SanitizeAttributes(mark.Attrs);
                    return mark;
                })
                .ToArray();
            node.Content = SanitizeNodes(node.Content);

            kept.Add(node);
        }

        return kept;
    }

    private static Dictionary<string, object?>? SanitizeAttributes(Dictionary<string, object?>? attributes)
    {
        if (attributes is null) return null;

        foreach (var name in attributes.Keys.ToList())
        {
            if (!IsSafeAttribute(name, attributes[name], 0)) attributes.Remove(name);
        }

        return attributes;
    }

    private static bool IsSafeAttribute(string name, object? value, int depth)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxAttributeNameLength) return false;

        // onclick, onerror, onload...: never data
        if (name.Length > 2 && name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) return false;
        if (DroppedAttributes.Contains(name)) return false;

        if (value is null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }) return true;

        if (UrlAttributes.Contains(name)) return AsString(value) is { } url && IsSafeUrl(url);

        if (AsString(value) is { } text) return !LooksLikeScript(text);

        // Objects and arrays may hide the same things deeper down
        return value is not JsonElement element || IsSafeElement(element, depth + 1);
    }

    private static bool IsSafeElement(JsonElement element, int depth)
    {
        if (depth > MaxNestedDepth) return false;

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().All(property => IsSafeAttribute(property.Name, property.Value, depth)),
            JsonValueKind.Array => element.EnumerateArray().All(item => IsSafeElement(item, depth + 1)),
            JsonValueKind.String => !LooksLikeScript(element.GetString() ?? string.Empty),
            _ => true,
        };
    }

    private static string? AsString(object value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    // A value that a renderer might put into an address even though its name isn't one of ours
    private static bool LooksLikeScript(string value)
    {
        var head = Compact(value, SchemeWindow);
        return HasCharacterReference(head)
               || head.StartsWith("javascript:", StringComparison.Ordinal)
               || head.StartsWith("vbscript:", StringComparison.Ordinal)
               || head.StartsWith("data:text/html", StringComparison.Ordinal)
               || head.StartsWith("data:image/svg", StringComparison.Ordinal)
               || head.StartsWith("data:application/", StringComparison.Ordinal);
    }

    // Browsers skip whitespace and control characters inside a scheme ("java\tscript:"), so they go
    // before the check; letters are compared in lower case. Only the first characters are needed
    private static string Compact(string value, int max)
    {
        var builder = new StringBuilder(Math.Min(value.Length, max));
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            builder.Append(char.ToLowerInvariant(c));
            if (builder.Length >= max) break;
        }

        return builder.ToString();
    }

    // HTML character references that can spell a scheme or its colon
    private static bool HasCharacterReference(string head) =>
        head.Contains("&#", StringComparison.Ordinal)
        || head.Contains("&colon", StringComparison.Ordinal)
        || head.Contains("&tab", StringComparison.Ordinal)
        || head.Contains("&newline", StringComparison.Ordinal);

    private static bool IsTypeName(string? type) => type is not null && TypeName().IsMatch(type);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex TypeName();

    [GeneratedRegex("^data:image/(png|jpe?g|gif|webp|avif);base64,")]
    private static partial Regex SafeDataImagePrefix();
}
