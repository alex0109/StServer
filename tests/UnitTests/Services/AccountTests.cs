using System.IO.Compression;
using System.Text;
using Application.Account;
using Application.Billing;
using Application.DTOs.Account;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Utility.Subscription;
using Infrastructure.Account;
using Infrastructure.Auth;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AccountDataPolicyTests
{
    // Properties that tie a row to a person
    private static readonly string[] UserLinks = ["UserId", "AuthorUserId", "AdminUserId", "SenderKey"];

    private static AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only").Options);

    [Fact]
    public void EveryTableWithAUser_HasARule()
    {
        using var db = Context();
        var ruled = AccountDataPolicy.Rules.Select(r => r.Entity).ToHashSet();

        var missing = db.Model.GetEntityTypes()
            .Where(t => t.GetProperties().Any(p => UserLinks.Contains(p.Name)))
            .Select(t => t.ClrType)
            .Where(t => !ruled.Contains(t))
            .Select(t => t.Name)
            .ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void EveryOtherTable_GoesByCascadeWithItsParent()
    {
        using var db = Context();
        var deleted = AccountDataPolicy.Rules
            .Where(r => r.Action == AccountDataAction.Delete)
            .Select(r => r.Entity)
            .ToHashSet();

        var orphans = db.Model.GetEntityTypes()
            .Where(t => !t.GetProperties().Any(p => UserLinks.Contains(p.Name)))
            .Where(t => !t.GetForeignKeys().Any(fk =>
                fk.IsRequired
                && fk.DeleteBehavior == DeleteBehavior.Cascade
                && deleted.Contains(fk.PrincipalEntityType.ClrType)))
            .Select(t => t.ClrType.Name)
            .ToList();

        orphans.ShouldBeEmpty();
    }

    [Fact]
    public void EveryRuleIsForATableOfTheModel_AndOnlyKeepHasNoStep()
    {
        using var db = Context();
        var tables = db.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        foreach (var rule in AccountDataPolicy.Rules)
        {
            tables.ShouldContain(rule.Entity);
            (rule.Step is null).ShouldBe(rule.Action == AccountDataAction.Keep, rule.Entity.Name);
        }

        AccountDataPolicy.Rules.Select(r => r.Entity).ShouldBeUnique();
    }
}

public class AccountReauthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AmrObject_GivesTheSignInTime()
    {
        var at = Now.AddMinutes(-3);

        AccountReauth.LastSignInAt([$$"""{"method":"otp","timestamp":{{at.ToUnixTimeSeconds()}}}"""], [])
            .ShouldBe(at);
    }

    [Fact]
    public void SeveralMethods_TheLatestCounts()
    {
        var older = Now.AddDays(-20).ToUnixTimeSeconds();
        var newer = Now.AddMinutes(-1).ToUnixTimeSeconds();

        AccountReauth.LastSignInAt(
                [$$"""[{"method":"oauth","timestamp":{{older}}},{"method":"otp","timestamp":{{newer}}}]"""],
                [])
            .ShouldBe(DateTimeOffset.FromUnixTimeSeconds(newer));
    }

    [Fact]
    public void AuthTime_IsUsedToo_AndBareMethodNamesAreIgnored()
    {
        var at = Now.AddMinutes(-2).ToUnixTimeSeconds();

        AccountReauth.LastSignInAt(["otp"], [at.ToString()]).ShouldBe(DateTimeOffset.FromUnixTimeSeconds(at));
        AccountReauth.LastSignInAt(["otp"], []).ShouldBeNull();
    }

    [Fact]
    public void Freshness_IsTenMinutes()
    {
        AccountReauth.IsFresh(Now.AddMinutes(-9), Now).ShouldBeTrue();
        AccountReauth.IsFresh(Now.AddMinutes(-11), Now).ShouldBeFalse();
        AccountReauth.IsFresh(null, Now).ShouldBeFalse();
        // A sign in "from the future" beyond the skew is not trusted
        AccountReauth.IsFresh(Now.AddMinutes(5), Now).ShouldBeFalse();
    }
}

public class AccountExportArchiveTests
{
    internal static AccountExportDto Data(params ExportWord[] words) => new()
    {
        ExportedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
        UserId = Guid.NewGuid(),
        Email = "reader@example.com",
        Materials = [],
        Tags = [new ExportTag(Guid.NewGuid(), "Біологія", "#22c55e")],
        Notes = [],
        Questions = [],
        Attempts = [],
        Dictionaries = [new ExportDictionary(Guid.NewGuid(), Guid.NewGuid(), "Unit 1", null, [.. words], DateTime.UtcNow, DateTime.UtcNow)],
        FlashcardSessions = [],
        FlashcardReviews = [],
        QuestionReviewSessions = [],
        QuestionReviews = [],
        Shares = [],
        LibraryRequests = [],
        LibraryItems = [],
        Notifications = [],
        AiUsage = [],
        AiUsageMonths = [],
    };

    internal static ExportWord Word(string term, string translation, string? example = null) =>
        new(Guid.NewGuid(), term, translation, null, null, null, example, null, null, [], false, null, DateTime.UtcNow);

    [Fact]
    public void Archive_HasData_Words_AndReadme()
    {
        var bytes = AccountExportArchive.Build(Data(Word("however", "проте")));

        using var zip = new ZipArchive(new MemoryStream(bytes));
        zip.Entries.Select(e => e.FullName).ShouldBe(
            [AccountExportArchive.DataFileName, AccountExportArchive.WordsFileName, AccountExportArchive.ReadmeFileName],
            ignoreOrder: true);

        using var reader = new StreamReader(zip.GetEntry(AccountExportArchive.DataFileName)!.Open());
        var json = reader.ReadToEnd();
        json.ShouldContain("\"email\": \"reader@example.com\"");
        // Readable letters, not \u escapes
        json.ShouldContain("Біологія");
        json.ShouldContain("проте");
    }

    [Fact]
    public void Csv_QuotesCommasAndQuotes()
    {
        var csv = Encoding.UTF8.GetString(AccountExportArchive.WordsCsv(Data(Word("say \"hi\"", "привіт, світ", "Line one\nline two"))));
        var lines = csv.TrimStart('﻿').Split("\r\n");

        lines[0].ShouldBe("term,translation,transcription,partOfSpeech,example,exampleTranslation,dictionary");
        lines[1].ShouldBe("\"say \"\"hi\"\"\",\"привіт, світ\",,,\"Line one\nline two\",,Unit 1");
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+47", "'+47")]
    [InlineData("@home", "'@home")]
    [InlineData("-ness", "-ness")]
    [InlineData("", "")]
    public void Csv_FormulaCellsAreDefused(string value, string expected)
    {
        AccountExportArchive.Cell(value).ShouldBe(expected);
    }
}

public class AccountServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IAccountRepository> _repo = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IAccountJobs> _jobs = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly FakeDeletedAccounts _deleted = new();
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _user.Setup(x => x.Email).Returns("reader@example.com");
        _user.Setup(x => x.Claims(It.IsAny<string>())).Returns([]);
        SignedIn(Now.AddMinutes(-2));

        _repo.Setup(x => x.DeleteAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync(new AccountDeletionSummary(12, 3));
        _email.Setup(x => x.IsConfigured).Returns(true);

        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(Now);

        _sut = new AccountService(
            _repo.Object, _user.Object, _jobs.Object, _deleted, _email.Object,
            new EmailOptions(), time.Object, NullLogger<AccountService>.Instance);
    }

    private void SignedIn(DateTimeOffset at) =>
        _user.Setup(x => x.Claims("amr")).Returns([$$"""{"method":"otp","timestamp":{{at.ToUnixTimeSeconds()}}}"""]);

    [Fact]
    public async Task Delete_RemovesData_ThenTheSignInAccount_ThenSendsTheEmail()
    {
        await _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = " Reader@Example.com ", Language = "uk" });

        _repo.Verify(x => x.DeleteAsync(_userId, Now.UtcDateTime), Times.Once);
        _jobs.Verify(x => x.DeleteAuthUser(_userId), Times.Once);
        _jobs.Verify(x => x.DeleteBillingCustomer(It.IsAny<SubscriptionProvider>(), It.IsAny<string>()), Times.Never);
        _deleted.Contains(_userId).ShouldBeTrue();
        _email.Verify(x => x.SendAsync(
            It.Is<OutgoingEmail>(e => e.To == "reader@example.com" && e.Subject.Contains("видалено")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WithAPaidPlan_ClosesTheCustomerAtTheProvider()
    {
        _repo.Setup(x => x.DeleteAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new AccountDeletionSummary(12, 3, new BillingCustomerRef(SubscriptionProvider.Stripe, "cus_123")));

        await _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = "reader@example.com", Language = "uk" });

        _jobs.Verify(x => x.DeleteBillingCustomer(SubscriptionProvider.Stripe, "cus_123"), Times.Once);
    }

    [Fact]
    public async Task WrongEmail_DeletesNothing()
    {
        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = "someone@example.com" }));

        _repo.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task OldSignIn_NeedsANewOne()
    {
        SignedIn(Now.AddHours(-3));

        await Should.ThrowAsync<ReauthRequiredException>(() =>
            _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = "reader@example.com" }));

        _repo.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task TokenWithoutSignInTime_NeedsANewOne()
    {
        _user.Setup(x => x.Claims("amr")).Returns([]);

        await Should.ThrowAsync<ReauthRequiredException>(() =>
            _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = "reader@example.com" }));
    }

    [Fact]
    public async Task SecondDelete_IsANoOp()
    {
        var dto = new AccountDeleteDto { ConfirmEmail = "reader@example.com" };

        await _sut.DeleteAsync(dto);
        await Should.NotThrowAsync(() => _sut.DeleteAsync(dto));

        _repo.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Once);
        _email.Verify(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailFailure_DoesNotUndoTheDeletion()
    {
        _email.Setup(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailUnavailableException("down"));

        await Should.NotThrowAsync(() => _sut.DeleteAsync(new AccountDeleteDto { ConfirmEmail = "reader@example.com" }));

        _jobs.Verify(x => x.DeleteAuthUser(_userId), Times.Once);
    }

    [Fact]
    public async Task Export_IsTheUsersOwnData()
    {
        _repo.Setup(x => x.GetExportAsync(_userId, "reader@example.com", Now.UtcDateTime))
            .ReturnsAsync(AccountExportArchiveTests.Data());

        var file = await _sut.ExportAsync();

        file.FileName.ShouldBe("studytracker-export-2026-10-29.zip");
        file.Content.ShouldNotBeEmpty();
        _repo.Verify(x => x.GetExportAsync(_userId, It.IsAny<string?>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Theory]
    [InlineData("https://abc.supabase.co", true)]
    [InlineData("http://127.0.0.1:54321", true)]
    [InlineData("http://abc.supabase.co", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void SupabaseAdmin_TalksOnlyOverHttps(string? url, bool allowed)
    {
        (SupabaseAuthAdmin.SafeBaseUrl(url) is not null).ShouldBe(allowed);
    }

    private sealed class FakeDeletedAccounts : IDeletedAccounts
    {
        private readonly HashSet<Guid> _ids = [];

        public void Add(Guid userId) => _ids.Add(userId);

        public bool Contains(Guid userId) => _ids.Contains(userId);
    }
}
