using System.Net;
using System.Text;
using System.Text.Json;
using Application.DTOs.Ai;
using Application.DTOs.Material;
using Application.DTOs.Question;
using Application.DTOs.RichTextDocument;
using Application.Constants;
using Application.Exceptions;
using Application.Import;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Infrastructure.Import;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Import;

public class AddressRulesTests
{
    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.1")]
    [InlineData("192.168.1.10")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.7")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fd00::1")]
    [InlineData("fc12:3456::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a00:1")]
    public void PrivateAndSpecialAddresses_AreRefused(string address)
    {
        AddressRules.IsPublic(IPAddress.Parse(address)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:93.184.216.34")]
    public void PublicAddresses_AreAllowed(string address)
    {
        AddressRules.IsPublic(IPAddress.Parse(address)).ShouldBeTrue();
    }
}

public class SafeHttpFetcherTests
{
    private static readonly IPAddress Public = IPAddress.Parse("93.184.216.34");

    private readonly FakeResolver _resolver = new();
    private readonly FakeHandler _handler = new();

    public SafeHttpFetcherTests()
    {
        _resolver.Answers["news.example"] = [Public];
    }

    private SafeHttpFetcher Fetcher(TimeSpan? timeout = null) =>
        new(_resolver, _handler, "StudytrackerBot/1.0 (+https://studytracker.test)", timeout);

    private static async Task<ArticleImportError> ErrorOf(Func<Task> action) =>
        (await Should.ThrowAsync<ArticleImportException>(action)).Code;

    [Theory]
    [InlineData("file:///etc/passwd", ArticleImportError.InvalidUrl)]
    [InlineData("ftp://news.example/a", ArticleImportError.InvalidUrl)]
    [InlineData("gopher://news.example/a", ArticleImportError.InvalidUrl)]
    [InlineData("https://user:pass@news.example/a", ArticleImportError.InvalidUrl)]
    [InlineData("https://news.example:8443/a", ArticleImportError.BlockedAddress)]
    [InlineData("http://news.example:22/", ArticleImportError.BlockedAddress)]
    [InlineData("http://localhost/admin", ArticleImportError.BlockedAddress)]
    [InlineData("http://db.internal/", ArticleImportError.BlockedAddress)]
    public async Task SchemesPortsAndLocalNames_AreRefused(string url, ArticleImportError expected)
    {
        (await ErrorOf(() => Fetcher().FetchAsync(new Uri(url)))).ShouldBe(expected);
        _handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[fd00::5]/")]
    [InlineData("http://10.0.0.8/")]
    public async Task AddressesWritten_InTheLink_AreChecked(string url)
    {
        (await ErrorOf(() => Fetcher().FetchAsync(new Uri(url)))).ShouldBe(ArticleImportError.BlockedAddress);
        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ANameThatPointsInside_IsRefused()
    {
        _resolver.Answers["evil.example"] = [IPAddress.Parse("192.168.0.10")];
        // A name with a public and a private answer is refused as a whole
        _resolver.Answers["mixed.example"] = [Public, IPAddress.Parse("10.0.0.1")];

        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://evil.example/")))).ShouldBe(ArticleImportError.BlockedAddress);
        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://mixed.example/")))).ShouldBe(ArticleImportError.BlockedAddress);
        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DnsRebinding_TheSocketChecksTheAddressItConnectsTo()
    {
        // Public when the link is checked, private a moment later when the socket would open
        var answers = new Queue<IPAddress[]>([[Public], [IPAddress.Parse("127.0.0.1")]]);
        var resolver = new Mock<IAddressResolver>();
        resolver.Setup(x => x.ResolveAsync("rebind.example", It.IsAny<CancellationToken>())).ReturnsAsync(() => answers.Dequeue());

        await SafeHttpFetcher.CheckedAddressAsync(resolver.Object, "rebind.example", CancellationToken.None);
        var error = await Should.ThrowAsync<ArticleImportException>(() =>
            SafeHttpFetcher.ConnectAsync(resolver.Object, new DnsEndPoint("rebind.example", 443), CancellationToken.None).AsTask());

        error.Code.ShouldBe(ArticleImportError.BlockedAddress);
    }

    [Fact]
    public async Task ARedirectInside_IsRefused()
    {
        _handler.Respond("https://news.example/story", HttpStatusCode.Found, location: "http://169.254.169.254/latest/meta-data/");

        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://news.example/story")))).ShouldBe(ArticleImportError.BlockedAddress);
        _handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Redirects_AreFollowedUpToThree()
    {
        _handler.Respond("https://news.example/1", HttpStatusCode.MovedPermanently, location: "/2");
        _handler.Respond("https://news.example/2", HttpStatusCode.Found, location: "/3");
        _handler.Respond("https://news.example/3", HttpStatusCode.Found, location: "/4");
        _handler.Respond("https://news.example/4", HttpStatusCode.Found, location: "/5");

        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://news.example/1")))).ShouldBe(ArticleImportError.Unreachable);
        _handler.Requests.Count.ShouldBe(4);
    }

    [Fact]
    public async Task ABigPage_IsCutOff()
    {
        // No Content-Length: the limit is counted while reading
        _handler.Respond("https://news.example/huge", body: new string('a', SafeHttpFetcher.MaxBytes + 10), withLength: false);
        _handler.Respond("https://news.example/declared", body: "<p>x</p>", declaredLength: SafeHttpFetcher.MaxBytes + 1);

        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://news.example/huge")))).ShouldBe(ArticleImportError.TooLarge);
        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://news.example/declared")))).ShouldBe(ArticleImportError.TooLarge);
    }

    [Fact]
    public async Task ASlowSite_TimesOut()
    {
        _handler.Respond("https://news.example/slow", delay: TimeSpan.FromSeconds(5));

        (await ErrorOf(() => Fetcher(TimeSpan.FromMilliseconds(200)).FetchAsync(new Uri("https://news.example/slow"))))
            .ShouldBe(ArticleImportError.Timeout);
    }

    [Fact]
    public async Task OnlyWebPages()
    {
        _handler.Respond("https://news.example/file.pdf", contentType: "application/pdf");

        (await ErrorOf(() => Fetcher().FetchAsync(new Uri("https://news.example/file.pdf")))).ShouldBe(ArticleImportError.NotHtml);
    }

    [Fact]
    public async Task Requests_SayWhoAsks_AndCarryNoCookies()
    {
        _handler.Respond("https://news.example/ok", body: "<p>Hello</p>");

        var page = await Fetcher().FetchAsync(new Uri("https://news.example/ok"), TestContext.Current.CancellationToken);

        page.Url.ShouldBe(new Uri("https://news.example/ok"));
        var request = _handler.Requests.Single();
        request.Headers.UserAgent.ToString().ShouldStartWith("StudytrackerBot/1.0");
        request.Headers.Contains("Cookie").ShouldBeFalse();
        request.Headers.Authorization.ShouldBeNull();
    }

    internal sealed class FakeResolver : IAddressResolver
    {
        public Dictionary<string, IPAddress[]> Answers { get; } = [];

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(Answers.TryGetValue(host, out var answer) ? answer : []);
    }

    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _responses = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Respond(
            string url,
            HttpStatusCode status = HttpStatusCode.OK,
            string body = "",
            string contentType = "text/html; charset=utf-8",
            string? location = null,
            bool withLength = true,
            long? declaredLength = null,
            TimeSpan? delay = null)
        {
            _responses[url] = async token =>
            {
                if (delay is { } wait) await Task.Delay(wait, token);

                var bytes = Encoding.UTF8.GetBytes(body);
                HttpContent content = withLength
                    ? new ByteArrayContent(bytes)
                    : new StreamContent(new UnknownLengthStream(bytes));
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                if (declaredLength is { } length) content.Headers.ContentLength = length;

                var response = new HttpResponseMessage(status) { Content = content };
                if (location is not null) response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
                return response;
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _responses.TryGetValue(request.RequestUri!.ToString(), out var respond)
                ? respond(cancellationToken)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    // A body whose size is not known up front, like a chunked or compressed answer
    private sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}

public class ArticleExtractorTests
{
    private static readonly Uri PageUrl = new("https://news.example/2026/10/story?utm=1");

    private static ImportedArticle Extract(string html) =>
        ArticleExtractor.Extract(new FetchedPage(PageUrl, Encoding.UTF8.GetBytes(html), "utf-8"));

    private static string Json(ImportedArticle article) => JsonSerializer.Serialize(article.Document);

    private static readonly string Body = string.Concat(Enumerable.Repeat(
        "<p>Photosynthesis turns light into chemical energy, and plants store it as sugar for later use.</p>", 5));

    [Fact]
    public void KeepsTheArticle_AndDropsEverythingAround()
    {
        var article = Extract($$"""
            <html><head>
              <title>Site | Story</title>
              <meta property="og:title" content="How plants eat light">
              <link rel="canonical" href="/2026/10/story">
              <script>alert(1)</script><style>p { color: red }</style>
            </head><body>
              <nav><a href="/">Home</a><a href="/news">News</a></nav>
              <div class="cookie-consent">We use cookies</div>
              <article>
                <h1>How plants eat light</h1>
                <h2>The short version</h2>
                {{Body}}
                <p>It is <strong>very</strong> <em>important</em>. Read <a href="javascript:alert(1)">this</a>
                   and <a href="/more">that</a>.</p>
                <ul><li>Light</li><li>Water</li></ul>
                <img src="x.png" onerror="alert(1)">
                <iframe src="https://ads.example"></iframe>
              </article>
              <aside>Related stories</aside>
              <footer>© Site</footer>
            </body></html>
            """);

        article.Title.ShouldBe("How plants eat light");
        article.Url.ShouldBe("https://news.example/2026/10/story");

        var json = Json(article);
        json.ShouldContain("Photosynthesis turns light");
        json.ShouldContain("\"heading\"");
        json.ShouldContain("\"bulletList\"");
        json.ShouldContain("\"bold\"");
        json.ShouldContain("\"italic\"");
        json.ShouldContain("https://news.example/more");
        json.ShouldNotContain("javascript");
        json.ShouldNotContain("alert");
        json.ShouldNotContain("cookies");
        json.ShouldNotContain("Related stories");
        json.ShouldNotContain("Home");
        json.ShouldNotContain("x.png");
        json.ShouldNotContain("ads.example");
        // The page title is the material's title, not repeated as the first line
        JsonSerializer.Serialize(article.Document.Content.First()).ShouldNotContain("How plants eat light");
    }

    [Fact]
    public void WithoutAnArticleTag_TheBlockWithMostTextWins()
    {
        var article = Extract($"""
            <html><head><title>Blog</title></head><body>
              <div class="menu"><a href="/a">A</a><a href="/b">B</a></div>
              <div class="post-body">{Body}</div>
              <div class="comments"><p>Great post, thanks so much for writing it all down here!</p></div>
            </body></html>
            """);

        var json = Json(article);
        json.ShouldContain("Photosynthesis");
        json.ShouldNotContain("Great post");
    }

    [Fact]
    public void SiblingDivs_AreSeparateParagraphs_AndFootnoteMarksGo()
    {
        var article = Extract($$"""
            <html><head><title>Story</title></head><body><article>
              <div class="lead">Plants make their own food from light, water and air every single day.</div>
              <div>Animals have to eat plants or other animals to get the energy they need.<sup class="reference">[1]</sup></div>
              {{Body}}
            </article></body></html>
            """);

        var first = JsonSerializer.Serialize(article.Document.Content[0]);
        first.ShouldContain("every single day.");
        first.ShouldNotContain("Animals");
        Json(article).ShouldNotContain("[1]");
    }

    [Fact]
    public void ALongTitle_IsCutAtAWord()
    {
        var article = Extract($"<html><head><title>{string.Join(' ', Enumerable.Repeat("Photosynthesis", 12))}</title></head><body><article>{Body}</article></body></html>");

        article.Title.Length.ShouldBeLessThanOrEqualTo(ArticleExtractor.MaxTitleLength);
        article.Title.ShouldEndWith("…");
    }

    [Fact]
    public void APageWithoutText_IsNotAnArticle()
    {
        Should.Throw<ArticleImportException>(() => Extract("<html><body><nav>Menu</nav><p>Hi</p></body></html>"))
            .Code.ShouldBe(ArticleImportError.NotAnArticle);
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:text/html,x", null)]
    [InlineData("vbscript:x", null)]
    [InlineData("#top", null)]
    [InlineData("/about", "https://news.example/about")]
    [InlineData("mailto:editor@news.example", "mailto:editor@news.example")]
    public void Links_KeepOnlySafeSchemes(string href, string? expected)
    {
        ArticleExtractor.SafeLink(href, PageUrl).ShouldBe(expected);
    }
}

public class MaterialImportServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IArticleImporter> _importer = new();
    private readonly Mock<IMaterialRepository> _materials = new();
    private readonly Mock<IAssessmentRepository> _assessments = new();
    private readonly Mock<IQuestionGenerationService> _questions = new();
    private readonly Mock<IContentLimits> _limits = new();
    private readonly MaterialImportService _sut;

    public MaterialImportServiceTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        var limitLock = new Mock<ILimitLock>();
        limitLock.Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>())).ReturnsAsync(new Mock<ILimitLockScope>().Object);

        _importer.Setup(x => x.ImportAsync("https://news.example/story", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportedArticle("How plants eat light", "https://news.example/story",
                new RichTextDocument { Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = "Light" }] }] }, 5));

        _sut = new MaterialImportService(
            _importer.Object, _materials.Object, _assessments.Object, _questions.Object, user.Object,
            limitLock.Object, _limits.Object, NullLogger<MaterialImportService>.Instance);
    }

    [Fact]
    public async Task Import_MakesAPrivateArticle()
    {
        Material? saved = null;
        _materials.Setup(x => x.AddAsync(It.IsAny<Material>())).Callback<Material>(m => saved = m);

        var result = await _sut.ImportAsync(new MaterialImportDto { Url = "https://news.example/story" }, TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.UserId.ShouldBe(_userId);
        saved.Title.ShouldBe("How plants eat light");
        saved.Link.ShouldBe("https://news.example/story");
        saved.Type.ShouldBe(Domain.Utility.Material.MaterialType.article);
        result.QuestionsCreated.ShouldBe(0);
        _questions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AFullAccount_FetchesNothing()
    {
        _limits.Setup(x => x.EnsureMaterialPlaceAsync(_userId, false)).ThrowsAsync(new LimitExceededException("Material limit reached"));

        await Should.ThrowAsync<LimitExceededException>(() =>
            _sut.ImportAsync(new MaterialImportDto { Url = "https://news.example/story" }));

        _importer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoAiQuota_TheMaterialIsKeptWithoutQuestions()
    {
        _questions.Setup(x => x.GenerateAsync(It.IsAny<Guid>(), It.IsAny<QuestionGenerateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiQuotaExceededException("No quota"));

        var result = await _sut.ImportAsync(new MaterialImportDto { Url = "https://news.example/story", GenerateQuestions = true }, TestContext.Current.CancellationToken);

        result.QuestionsSkipped.ShouldBe("quota");
        _materials.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Questions_AreMadeWhenAskedFor()
    {
        _questions.Setup(x => x.GenerateAsync(It.IsAny<Guid>(), It.IsAny<QuestionGenerateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new QuestionResponseDto { Title = "Where?", CreatedAt = DateTime.UtcNow },
                new QuestionResponseDto { Title = "Why?", CreatedAt = DateTime.UtcNow },
            ]);

        var result = await _sut.ImportAsync(new MaterialImportDto { Url = "https://news.example/story", GenerateQuestions = true }, TestContext.Current.CancellationToken);

        result.QuestionsCreated.ShouldBe(2);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://youtu.be/abc")]
    public async Task Videos_AreNotImported(string url)
    {
        var importer = new ArticleImporter(new SafeHttpFetcher(
            new SafeHttpFetcherTests.FakeResolver(), new SafeHttpFetcherTests.FakeHandler(), "bot"));

        (await Should.ThrowAsync<ArticleImportException>(() => importer.ImportAsync(url))).Code
            .ShouldBe(ArticleImportError.VideoNotSupported);
    }
}
