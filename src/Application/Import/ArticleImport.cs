using Application.DTOs.RichTextDocument;

namespace Application.Import;

public record ImportedArticle(string Title, string Url, RichTextDocument Document, int TextLength);

// What went wrong, as a code the client translates
public enum ArticleImportError
{
    InvalidUrl,
    BlockedAddress,
    Unreachable,
    Timeout,
    TooLarge,
    NotHtml,
    NotAnArticle,
    VideoNotSupported,
}

public class ArticleImportException : Exception
{
    public ArticleImportException(ArticleImportError code, string message)
        : base(message)
    {
        Code = code;
    }

    public ArticleImportError Code { get; }
}
