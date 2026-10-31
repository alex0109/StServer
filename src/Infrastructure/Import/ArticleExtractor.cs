using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Application.DTOs.RichTextDocument;
using Application.Import;
using Node = Application.DTOs.RichTextDocument.Node;
using Mark = Application.DTOs.RichTextDocument.Mark;

namespace Infrastructure.Import;

// Turns a web page into the editor's document: the article only, no menus, ads, scripts or images.
// AngleSharp parses HTML the way browsers do; on top of it a small Readability-style score finds the
// block with the most paragraph text. Only text, headings, lists, quotes, code, bold, italic and
// links with safe schemes survive, so nothing from the page can run in the editor
public static partial class ArticleExtractor
{
    public const int MaxTitleLength = 70;
    public const int MinTextLength = 250;
    public const int MaxTextLength = 200_000;

    private static readonly string[] Dropped =
    [
        "script", "style", "noscript", "template", "iframe", "frame", "object", "embed", "svg", "canvas",
        "form", "input", "button", "select", "textarea", "nav", "header", "footer", "aside", "img", "picture",
        "video", "audio", "source", "figure", "dialog", "menu",
    ];

    // Text-level tags; any other element is a block of its own, so two divs never run into one line
    private static readonly HashSet<string> Inlines =
    [
        "A", "ABBR", "B", "BDI", "BDO", "BR", "CITE", "CODE", "DATA", "DFN", "EM", "FONT", "I", "KBD", "LABEL",
        "MARK", "Q", "S", "SAMP", "SMALL", "SPAN", "STRONG", "SUB", "SUP", "TIME", "U", "VAR", "WBR",
    ];

    private static readonly HashSet<string> Blocks =
    [
        "P", "H1", "H2", "H3", "H4", "H5", "H6", "UL", "OL", "BLOCKQUOTE", "PRE", "TABLE", "HR",
    ];

    static ArticleExtractor()
    {
        // Pages in windows-1251, iso-8859-x and others
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static ImportedArticle Extract(FetchedPage page)
    {
        var parser = new HtmlParser();
        var document = page.Charset is { } charset && TryEncoding(charset) is { } encoding
            ? parser.ParseDocument(encoding.GetString(page.Body))
            : parser.ParseDocument(new MemoryStream(page.Body));

        var title = Title(document);
        var url = Canonical(document, page.Url) ?? page.Url.ToString();

        foreach (var element in document.QuerySelectorAll(string.Join(',', Dropped)).ToList()) element.Remove();
        foreach (var element in document.All.Where(IsNoise).ToList()) element.Remove();

        var root = MainContent(document);
        var nodes = new List<Node>();
        var budget = new TextBudget(MaxTextLength);
        AppendBlocks(root, page.Url, nodes, budget);

        // The page's own title is the material's title, a repeat at the top is dropped
        if (nodes.FirstOrDefault() is { Type: "heading" } first && Same(PlainText(first), title)) nodes.RemoveAt(0);

        var length = nodes.Sum(x => PlainText(x).Length);
        if (length < MinTextLength)
            throw new ArticleImportException(ArticleImportError.NotAnArticle, "No article text was found on the page");

        return new ImportedArticle(title, url, new RichTextDocument { Content = nodes }, length);
    }

    private static Encoding? TryEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name.Trim('"'));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string Title(IHtmlDocument document)
    {
        var raw = document.QuerySelector("meta[property='og:title']")?.GetAttribute("content")
                  ?? document.Title
                  ?? document.QuerySelector("h1")?.TextContent
                  ?? "";
        var title = Spaces().Replace(raw, " ").Trim();
        if (title.Length == 0) return "Article";
        if (title.Length <= MaxTitleLength) return title;

        // At a word boundary, with an ellipsis
        var cut = title[..(MaxTitleLength - 1)];
        var space = cut.LastIndexOf(' ');
        return (space > MaxTitleLength / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    private static string? Canonical(IHtmlDocument document, Uri pageUrl)
    {
        var href = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        return href is not null && Uri.TryCreate(pageUrl, href, out var canonical)
               && (canonical.Scheme == Uri.UriSchemeHttps || canonical.Scheme == Uri.UriSchemeHttp)
            ? canonical.ToString()
            : null;
    }

    private static bool IsNoise(IElement element)
    {
        if (element.TagName is "HTML" or "BODY" or "MAIN" or "ARTICLE") return false;

        if (element.GetAttribute("aria-hidden") == "true" || element.HasAttribute("hidden")) return true;

        var role = element.GetAttribute("role");
        if (role is "navigation" or "banner" or "contentinfo" or "complementary" or "dialog" or "alert") return true;

        // "article-body" or "main-content" must stay, so a match on the whole name list is needed
        var names = $"{element.ClassName} {element.Id}";
        return names.Length > 1 && NoiseNames().IsMatch(names) && !KeepNames().IsMatch(names);
    }

    // The article element when there is one with real text, otherwise the block whose paragraphs hold the most text
    private static IElement MainContent(IHtmlDocument document)
    {
        var body = (IElement?)document.Body ?? document.DocumentElement;

        var article = document.QuerySelectorAll("article, [itemprop='articleBody'], main")
            .OrderByDescending(ParagraphText)
            .FirstOrDefault();
        if (article is not null && ParagraphText(article) >= MinTextLength) return article;

        var scores = new Dictionary<IElement, double>();
        foreach (var paragraph in body.QuerySelectorAll("p, pre, blockquote"))
        {
            var length = paragraph.TextContent.Trim().Length;
            if (length < 25) continue;

            var score = 1 + Math.Min(length / 100.0, 3) + paragraph.TextContent.Count(c => c == ',') * 0.1;
            if (paragraph.ParentElement is { } parent) scores[parent] = scores.GetValueOrDefault(parent) + score;
            if (paragraph.ParentElement?.ParentElement is { } grandParent)
                scores[grandParent] = scores.GetValueOrDefault(grandParent) + score / 2;
        }

        return scores.Count == 0 ? body : scores.MaxBy(x => x.Value * (1 - LinkDensity(x.Key))).Key;
    }

    private static int ParagraphText(IElement element) =>
        element.QuerySelectorAll("p").Sum(x => x.TextContent.Trim().Length);

    private static double LinkDensity(IElement element)
    {
        var text = element.TextContent.Length;
        return text == 0 ? 1 : element.QuerySelectorAll("a").Sum(x => x.TextContent.Length) / (double)text;
    }

    private static void AppendBlocks(INode parent, Uri baseUrl, List<Node> nodes, TextBudget budget)
    {
        var loose = new List<Node>();

        void FlushLoose()
        {
            if (loose.Any(x => !string.IsNullOrWhiteSpace(x.Text) || x.Type != "text"))
                nodes.Add(new Node { Type = "paragraph", Content = Trimmed(loose) });
            loose.Clear();
        }

        foreach (var child in parent.ChildNodes)
        {
            if (budget.Spent) break;

            if (child is not IElement element)
            {
                if (child.NodeType == NodeType.Text) AppendInline(child, baseUrl, loose, [], budget);
                continue;
            }

            if (!Blocks.Contains(element.TagName) && !HasBlocks(element))
            {
                if (Inlines.Contains(element.TagName))
                {
                    AppendInline(element, baseUrl, loose, [], budget);
                    continue;
                }

                // A div or a section with plain text inside is a paragraph of its own
                FlushLoose();
                AddParagraph(element, baseUrl, nodes, budget);
                continue;
            }

            FlushLoose();

            switch (element.TagName)
            {
                case "P":
                    AddParagraph(element, baseUrl, nodes, budget);
                    break;
                case "H1" or "H2" or "H3" or "H4" or "H5" or "H6":
                    var inline = Inline(element, baseUrl, budget);
                    if (inline.Count > 0)
                    {
                        nodes.Add(new Node
                        {
                            Type = "heading",
                            Attrs = new() { ["level"] = element.TagName[1] - '0' },
                            Content = inline,
                        });
                    }
                    break;
                case "UL" or "OL":
                    AddList(element, baseUrl, nodes, budget);
                    break;
                case "BLOCKQUOTE":
                    var quoted = new List<Node>();
                    AppendBlocks(element, baseUrl, quoted, budget);
                    if (quoted.Count > 0) nodes.Add(new Node { Type = "blockquote", Content = quoted });
                    break;
                case "PRE":
                    var code = budget.Take(element.TextContent.TrimEnd());
                    if (code.Length > 0)
                        nodes.Add(new Node { Type = "codeBlock", Content = [new Node { Type = "text", Text = code }] });
                    break;
                case "TABLE":
                    // Tables are kept as rows of text: the editor has no tables
                    foreach (var row in element.QuerySelectorAll("tr"))
                    {
                        var cells = string.Join(" · ", row.Children.Select(x => Spaces().Replace(x.TextContent, " ").Trim()).Where(x => x.Length > 0));
                        if (cells.Length > 0) nodes.Add(new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = budget.Take(cells) }] });
                    }
                    break;
                case "HR":
                    break;
                default:
                    AppendBlocks(element, baseUrl, nodes, budget);
                    break;
            }
        }

        FlushLoose();
    }

    private static bool HasBlocks(IElement element) => element.QuerySelector("p, h1, h2, h3, h4, h5, h6, ul, ol, blockquote, pre, table") is not null;

    private static void AddParagraph(IElement element, Uri baseUrl, List<Node> nodes, TextBudget budget)
    {
        var inline = Inline(element, baseUrl, budget);
        if (inline.Count > 0) nodes.Add(new Node { Type = "paragraph", Content = inline });
    }

    private static void AddList(IElement list, Uri baseUrl, List<Node> nodes, TextBudget budget)
    {
        var items = new List<Node>();
        foreach (var item in list.Children.Where(x => x.TagName == "LI"))
        {
            var content = new List<Node>();
            AppendBlocks(item, baseUrl, content, budget);
            if (content.Count > 0) items.Add(new Node { Type = "listItem", Content = content });
        }

        if (items.Count > 0)
            nodes.Add(new Node { Type = list.TagName == "OL" ? "orderedList" : "bulletList", Content = items });
    }

    private static List<Node> Inline(IElement element, Uri baseUrl, TextBudget budget)
    {
        var result = new List<Node>();
        foreach (var child in element.ChildNodes) AppendInline(child, baseUrl, result, [], budget);
        return Trimmed(result);
    }

    private static void AppendInline(INode node, Uri baseUrl, List<Node> result, List<Mark> marks, TextBudget budget)
    {
        if (budget.Spent) return;

        if (node.NodeType == NodeType.Text)
        {
            var text = Spaces().Replace(node.TextContent, " ");
            if (text.Length == 0) return;

            text = budget.Take(text);
            // Next to the previous piece with the same marks, so the document stays small
            if (result.LastOrDefault() is { Type: "text" } last && SameMarks(last.Marks, marks))
                last.Text += text;
            else
                result.Add(new Node { Type = "text", Text = text, Marks = marks.Count > 0 ? [.. marks] : null });
            return;
        }

        if (node is not IElement element) return;

        if (element.TagName == "BR")
        {
            result.Add(new Node { Type = "hardBreak" });
            return;
        }

        var added = element.TagName switch
        {
            "STRONG" or "B" => new Mark { Type = "bold" },
            "EM" or "I" => new Mark { Type = "italic" },
            "CODE" => new Mark { Type = "code" },
            "A" when SafeLink(element.GetAttribute("href"), baseUrl) is { } href => new Mark
            {
                Type = "link",
                Attrs = new() { ["href"] = href, ["target"] = "_blank", ["rel"] = "noopener noreferrer" },
            },
            _ => null,
        };

        var inner = added is null || marks.Any(x => x.Type == added.Type) ? marks : [.. marks, added];
        foreach (var child in element.ChildNodes) AppendInline(child, baseUrl, result, inner, budget);

        // Nested blocks read as one line here, so their texts must not run into each other
        if (!Inlines.Contains(element.TagName) && result.LastOrDefault() is { Type: "text" } previous && !previous.Text!.EndsWith(' '))
            previous.Text += " ";
    }

    // Same rule as the editor: web and mail links only, so a "javascript:" link can never run
    public static string? SafeLink(string? href, Uri baseUrl)
    {
        if (string.IsNullOrWhiteSpace(href) || href.TrimStart().StartsWith('#')) return null;
        if (!Uri.TryCreate(baseUrl, href.Trim(), out var url)) return null;

        return url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeMailto
            ? url.ToString()
            : null;
    }

    private static bool SameMarks(Mark[]? a, List<Mark> b) =>
        (a?.Length ?? 0) == b.Count && (a ?? []).Zip(b).All(x =>
            x.First.Type == x.Second.Type
            && Equals(x.First.Attrs?.GetValueOrDefault("href"), x.Second.Attrs?.GetValueOrDefault("href")));

    // No spaces at the edges of a paragraph, and no empty paragraphs
    private static List<Node> Trimmed(List<Node> nodes)
    {
        var list = nodes.Where(x => x.Type != "text" || !string.IsNullOrEmpty(x.Text)).ToList();
        while (list.Count > 0 && list[0].Type == "hardBreak") list.RemoveAt(0);
        while (list.Count > 0 && list[^1].Type == "hardBreak") list.RemoveAt(list.Count - 1);

        if (list.FirstOrDefault() is { Type: "text" } first) first.Text = first.Text!.TrimStart();
        if (list.LastOrDefault() is { Type: "text" } last) last.Text = last.Text!.TrimEnd();

        return list.Where(x => x.Type != "text" || !string.IsNullOrEmpty(x.Text)).ToList();
    }

    private static string PlainText(Node node) =>
        node.Text ?? string.Concat(node.Content?.Select(PlainText) ?? []);

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim().TrimEnd('…'), b.Trim().TrimEnd('…'), StringComparison.OrdinalIgnoreCase)
        || (b.EndsWith('…') && a.StartsWith(b.TrimEnd('…'), StringComparison.OrdinalIgnoreCase));

    private sealed class TextBudget(int limit)
    {
        private int _left = limit;

        public bool Spent => _left <= 0;

        public string Take(string text)
        {
            if (text.Length <= _left)
            {
                _left -= text.Length;
                return text;
            }

            var part = text[..Math.Max(0, _left)];
            _left = 0;
            return part;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"(^|[\s_-])(comments?|share|sharing|social|related|recommend|promo|advert|ads?|banner|sponsor|cookie|consent|subscribe|newsletter|sidebar|breadcrumbs?|pagination|popup|modal|footer|navbar|menu|toc|noprint|hatnote|shortdescription|editsection|reference|references|reflist|navbox|infobox|metadata|thumb|tmulti|thumbcaption|gallery|caption|wp-caption)([\s_-]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseNames();

    [GeneratedRegex(@"(article|post|entry|story|main|body|content|text)", RegexOptions.IgnoreCase)]
    private static partial Regex KeepNames();
}
