using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Account;

public enum AccountDataAction
{
    Delete,
    Anonymize,
    Keep,
}

// What a step does with the rows of one user; tombstone is a fresh id for anonymized rows of this deletion
public delegate Task<int> AccountDataStep(AppDbContext db, Guid userId, Guid tombstone);

public sealed record AccountDataRule(Type Entity, AccountDataAction Action, string Why, AccountDataStep? Step);

// What happens to every table on account deletion. A test walks the EF model, so a new table
// with a user id can't be forgotten here. Tables without one go by cascade from their parent
public static class AccountDataPolicy
{
    public const string DeletedAuthor = "Deleted user";

    public static string FeedbackSenderKey(Guid userId) => $"user:{userId}";

    // Children first: the cascades from materials then have little left to find
    public static readonly IReadOnlyList<AccountDataRule> Rules =
    [
        DeleteByUser<QuestionReview>(),
        DeleteByUser<QuestionProgress>(),
        DeleteByUser<QuestionReviewSession>(),
        DeleteByUser<FlashcardReview>(),
        DeleteByUser<DictionaryEntryProgress>(),
        DeleteByUser<FlashcardSession>(),
        DeleteByUser<Result>(),
        DeleteByUser<Attempt>(),
        DeleteByUser<Assessment>(),
        // Options, dictionary entries, files and material tags go with these by cascade
        DeleteByUser<Question>(),
        DeleteByUser<Note>(),
        DeleteByUser<Dictionary>(),
        DeleteByUser<MaterialShare>(),
        DeleteByUser<Material>(),
        DeleteByUser<Tag>(),
        DeleteByUser<LibraryRequest>(),
        DeleteByUser<UserNotification>(),
        DeleteByUser<UserNotificationSettings>(),
        DeleteByUser<UserProfile>(),
        DeleteByUser<UserSubscription>(),

        new(typeof(FeedbackSubmission), AccountDataAction.Delete,
            "Only a counter for the monthly limit, the key holds the user id",
            (db, userId, _) => db.FeedbackSubmissions
                .Where(x => x.SenderKey == FeedbackSenderKey(userId))
                .ExecuteDeleteAsync()),

        new(typeof(LibraryItem), AccountDataAction.Anonymize,
            "Other users already have copies; the material stays without the author's name and link to the account",
            (db, userId, _) => db.LibraryItems
                .Where(x => x.AuthorUserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AuthorUserId, Guid.Empty)
                    .SetProperty(x => x.AuthorNickname, DeletedAuthor)
                    .SetProperty(x => x.SourceMaterialId, (Guid?)null))),

        new(typeof(AiUsage), AccountDataAction.Anonymize,
            "AI costs stay in the totals; the rows get an id that belongs to nobody",
            (db, userId, tombstone) => db.AiUsages
                .Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, tombstone))),

        // All months of the user get the same new id, so the unique month key still holds
        new(typeof(AiUsageMonthly), AccountDataAction.Anonymize,
            "Monthly AI totals for cost reports",
            (db, userId, tombstone) => db.AiUsageMonthly
                .Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, tombstone))),

        // Payment records are kept for accounting and tax; the link to the person goes
        new(typeof(SubscriptionEvent), AccountDataAction.Anonymize,
            "Billing records are kept for accounting; the user id is removed",
            (db, userId, _) => db.SubscriptionEvents
                .Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, (Guid?)null))),

        new(typeof(AdminAuditEntry), AccountDataAction.Keep,
            "Actions of the library owner are a security record; a deleted admin's entries stay as they are",
            null),
    ];

    private static AccountDataRule DeleteByUser<T>() where T : class =>
        new(typeof(T), AccountDataAction.Delete, "Data of the user",
            (db, userId, _) => db.Set<T>()
                .Where(x => EF.Property<Guid>(x, "UserId") == userId)
                .ExecuteDeleteAsync());
}
