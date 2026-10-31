using Application.Account;
using Application.Billing;
using Application.DTOs.Account;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Admin;
using Domain.Utility.Subscription;
using Infrastructure.Account;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db)
    {
        _db = db;
    }

    // Every query filters by the user itself, so nothing of other users can get in
    public async Task<AccountExportDto> GetExportAsync(Guid userId, string? email, DateTime exportedAt)
    {
        var materials = await _db.Materials.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ExportMaterial(
                m.Id, m.Title, m.Type, m.Status, m.Link, m.Content, m.IsActive,
                m.MaterialTags.Select(t => t.TagId).ToList(),
                m.MaterialFiles.Select(f => new ExportFile(f.Id, f.OriginalFileName, f.ContentType, f.FileSize, f.CreatedAt)).ToList(),
                m.ImportedFromMaterialId, m.ImportedFromLibraryItemId, m.CreatedAt, m.UpdatedAt, m.DeletedAt))
            .ToListAsync();

        var tags = await _db.Tags.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new ExportTag(t.Id, t.Name, t.Color))
            .ToListAsync();

        var notes = await _db.Notes.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.MaterialId).ThenBy(n => n.Order)
            .Select(n => new ExportNote(n.Id, n.MaterialId, n.Title, n.Type, n.TextContent, n.DrawingContent, n.Order, n.CreatedAt, n.UpdatedAt))
            .ToListAsync();

        var questions = await _db.Questions.AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.CreatedAt)
            .Select(q => new ExportQuestion(
                q.Id, q.MaterialId, q.NoteId, q.DictionaryEntryId, q.Title, q.Answer, q.QuestionType,
                q.QuestionDifficulty, q.Explanation, q.IsActive,
                q.Options.OrderBy(o => o.Position).Select(o => new ExportOption(o.Id, o.Name, o.Position, o.IsCorrect)).ToList(),
                q.Progress == null
                    ? null
                    : new ExportReviewState(
                        q.Progress.Algorithm, q.Progress.State, q.Progress.Due, q.Progress.Stability,
                        q.Progress.Difficulty, q.Progress.ScheduledDays, q.Progress.Reps, q.Progress.Lapses,
                        q.Progress.LastReviewAt),
                q.CreatedAt, q.UpdatedAt, q.DeletedAt))
            .ToListAsync();

        var attempts = await _db.Attempts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.StartedAt)
            .Select(a => new ExportAttempt(
                a.Id, a.Assessment.MaterialId, a.AttemptStatus, a.StartedAt, a.FinishedAt,
                a.Results.OrderBy(r => r.AnsweredAt).Select(r => new ExportResult(
                    r.Id, r.QuestionId, r.UserAnswer, r.UserAnswerOptionIds, r.IsCorrect, r.Score,
                    r.ConfidenceLevel, r.Rating, r.Method, r.AiExplanation, r.TimeSpent, r.OverriddenAt,
                    r.AnsweredAt)).ToList()))
            .ToListAsync();

        var dictionaries = await _db.Dictionaries.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ExportDictionary(
                d.Id, d.MaterialId, d.Title, d.Description,
                d.Entries.OrderBy(e => e.Position).Select(e => new ExportWord(
                    e.Id, e.Term, e.Translation, e.Transcription, e.PartOfSpeech, e.GrammarInfo, e.Example,
                    e.ExampleTranslation, e.Note, e.Synonyms, e.IsArchived,
                    e.Progress == null
                        ? null
                        : new ExportReviewState(
                            e.Progress.Algorithm, e.Progress.State, e.Progress.Due, e.Progress.Stability,
                            e.Progress.Difficulty, e.Progress.ScheduledDays, e.Progress.Reps, e.Progress.Lapses,
                            e.Progress.LastReviewAt),
                    e.CreatedAt)).ToList(),
                d.CreatedAt, d.UpdatedAt))
            .ToListAsync();

        var flashcardSessions = await _db.FlashcardSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.StartedAt)
            .Select(s => new ExportFlashcardSession(s.Id, s.DictionaryId, s.Scope, s.Mode, s.CardsTotal, s.StartedAt, s.FinishedAt))
            .ToListAsync();

        var flashcardReviews = await _db.FlashcardReviews.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.ReviewedAt)
            .Select(r => new ExportFlashcardReview(r.Id, r.SessionId, r.EntryId, r.Rating, r.DurationMs, r.ReviewedAt))
            .ToListAsync();

        var questionSessions = await _db.QuestionReviewSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.StartedAt)
            .Select(s => new ExportQuestionReviewSession(s.Id, s.QuestionsTotal, s.StartedAt, s.FinishedAt))
            .ToListAsync();

        var questionReviews = await _db.QuestionReviews.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.ReviewedAt)
            .Select(r => new ExportQuestionReview(r.Id, r.QuestionId, r.AttemptId, r.SessionId, r.Rating, r.ReviewedAt))
            .ToListAsync();

        var shares = await _db.MaterialShares.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new ExportShare(s.Id, s.MaterialId, s.ImportsCount, s.CreatedAt, s.RevokedAt))
            .ToListAsync();

        var libraryRequests = await _db.LibraryRequests.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ExportLibraryRequest(
                r.Id, r.Type, r.Status, r.MaterialId, r.LibraryItemId, r.Title, r.AuthorNickname, r.Comment,
                r.RejectionReason, r.ReviewComment, r.CreatedAt, r.ReviewedAt))
            .ToListAsync();

        var libraryItems = await _db.LibraryItems.AsNoTracking()
            .Where(i => i.AuthorUserId == userId)
            .OrderBy(i => i.PublishedAt)
            .Select(i => new ExportLibraryItem(i.Id, i.Title, i.AuthorNickname, i.Status, i.Version, i.ImportsCount, i.PublishedAt, i.RemovedAt))
            .ToListAsync();

        var settings = await _db.UserNotificationSettings.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new ExportSettings(s.Email, s.EmailDailyReminder, s.EmailWeeklyDigest, s.EmailStreakReminder))
            .FirstOrDefaultAsync();

        var profile = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ExportProfile(p.TimeZone, p.InterfaceLanguage, p.ExplanationLanguage, p.NativeLanguage, p.StudyLanguage, p.Nickname, p.DailyGoal,
                p.DesiredRetention, p.NewCardsPerDay, p.MaxReviewsPerDay, p.DailyReminderHour, p.StreakReminderHour,
                p.StreakFreezesPerMonth))
            .FirstOrDefaultAsync();

        var subscription = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new ExportSubscription(s.Tier, s.CurrentPeriodEnd, s.CreatedAt))
            .FirstOrDefaultAsync();

        var notifications = await _db.UserNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new ExportNotification(n.Id, n.Type, n.Data, n.CreatedAt, n.ReadAt, n.EmailSentAt))
            .ToListAsync();

        var aiUsage = await _db.AiUsages.AsNoTracking()
            .Where(u => u.UserId == userId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new ExportAiUsage(u.Feature, u.Status, u.Provider, u.Model, u.InputTokens, u.OutputTokens, u.CreatedAt))
            .ToListAsync();

        var aiUsageMonths = await _db.AiUsageMonthly.AsNoTracking()
            .Where(u => u.UserId == userId)
            .OrderBy(u => u.Year).ThenBy(u => u.Month)
            .Select(u => new ExportAiUsageMonth(u.Year, u.Month, u.Feature, u.Provider, u.Model, u.Requests, u.FailedRequests, u.InputTokens, u.OutputTokens))
            .ToListAsync();

        return new AccountExportDto
        {
            ExportedAt = exportedAt,
            UserId = userId,
            Email = email,
            Materials = materials,
            Tags = tags,
            Notes = notes,
            Questions = questions,
            Attempts = attempts,
            Dictionaries = dictionaries,
            FlashcardSessions = flashcardSessions,
            FlashcardReviews = flashcardReviews,
            QuestionReviewSessions = questionSessions,
            QuestionReviews = questionReviews,
            Shares = shares,
            LibraryRequests = libraryRequests,
            LibraryItems = libraryItems,
            Profile = profile,
            Settings = settings,
            Subscription = subscription,
            Notifications = notifications,
            AiUsage = aiUsage,
            AiUsageMonths = aiUsageMonths,
        };
    }

    public async Task<AccountDeletionSummary> DeleteAsync(Guid userId, DateTime deletedAt)
    {
        var tombstone = Guid.NewGuid();
        var deleted = 0;
        var anonymized = 0;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Read before the row goes: the provider's customer is closed after the commit
        var billingCustomer = await _db.UserSubscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Provider != SubscriptionProvider.Manual && x.ProviderCustomerId != null)
            .Select(x => new BillingCustomerRef(x.Provider, x.ProviderCustomerId!))
            .FirstOrDefaultAsync();

        foreach (var rule in AccountDataPolicy.Rules)
        {
            if (rule.Step is null) continue;

            var rows = await rule.Step(_db, userId, tombstone);
            if (rule.Action == AccountDataAction.Delete) deleted += rows;
            else anonymized += rows;
        }

        // The owner sees erasures in the admin log: a short id only, no email, no address
        _db.AdminAuditEntries.Add(new AdminAuditEntry
        {
            Id = Guid.NewGuid(),
            AdminUserId = Guid.Empty,
            Action = AdminAction.AccountDeleted,
            Details = $"user {AccountIds.Short(userId)}…, {deleted} rows removed, {anonymized} anonymized",
            CreatedAt = deletedAt,
        });
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return new AccountDeletionSummary(deleted, anonymized, billingCustomer);
    }
}
