using System.Net;
using System.Text;

namespace Application.Email;

// Plain, mail client friendly layout. Everything that comes from users is HTML encoded here
public static class EmailTemplates
{
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // One line, so user text can never add email headers
    public static string SingleLine(string value, int maxLength)
    {
        var line = value.ReplaceLineEndings(" ").Trim();
        return line.Length <= maxLength ? line : line[..maxLength];
    }

    public static string Layout(string title, string bodyHtml, string? footerHtml = null)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><body style=\"margin:0;padding:24px;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#171717\">");
        html.Append("<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e5e5;border-radius:16px;padding:24px\">");
        html.Append($"<h1 style=\"margin:0 0 16px;font-size:20px;color:#171717\">{Encode(title)}</h1>");
        html.Append(bodyHtml);
        html.Append("</div>");
        if (footerHtml is not null)
        {
            html.Append($"<p style=\"max-width:560px;margin:16px auto 0;font-size:12px;color:#737373;text-align:center\">{footerHtml}</p>");
        }
        html.Append("</body></html>");

        return html.ToString();
    }

    public static string Paragraph(string text) =>
        $"<p style=\"margin:0 0 12px;font-size:15px;line-height:1.5\">{Encode(text)}</p>";

    public static string Button(string text, string url) =>
        $"<p style=\"margin:20px 0 4px\"><a href=\"{Encode(url)}\" style=\"display:inline-block;background:#171717;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:12px;font-weight:bold;font-size:14px\">{Encode(text)}</a></p>";
}
