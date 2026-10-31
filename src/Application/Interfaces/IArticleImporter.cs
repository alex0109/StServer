using Application.Import;

namespace Application.Interfaces;

// Downloads a public web page and keeps only the article text. Throws ArticleImportException
public interface IArticleImporter
{
    Task<ImportedArticle> ImportAsync(string url, CancellationToken cancellationToken = default);
}
