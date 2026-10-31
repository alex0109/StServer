using System.Text.Json;
using Domain.Utility.Ai;
using Domain.Utility.Attempt;
using Domain.Utility.Dictionary;
using Domain.Utility.Flashcard;
using Domain.Utility.Library;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Notification;
using Domain.Utility.Question;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Domain.Utility.Subscription;

namespace Application.DTOs.Account;

// Everything the app keeps about one user, as it is stored. Rich text and drawings stay the editor JSON
public record AccountExportDto
{
    public required DateTime ExportedAt { get; init; }
    public required Guid UserId { get; init; }
    public string? Email { get; init; }

    public required List<ExportMaterial> Materials { get; init; }
    public required List<ExportTag> Tags { get; init; }
    public required List<ExportNote> Notes { get; init; }
    public required List<ExportQuestion> Questions { get; init; }
    public required List<ExportAttempt> Attempts { get; init; }
    public required List<ExportDictionary> Dictionaries { get; init; }
    public required List<ExportFlashcardSession> FlashcardSessions { get; init; }
    public required List<ExportFlashcardReview> FlashcardReviews { get; init; }
    public required List<ExportQuestionReviewSession> QuestionReviewSessions { get; init; }
    public required List<ExportQuestionReview> QuestionReviews { get; init; }
    public required List<ExportShare> Shares { get; init; }
    public required List<ExportLibraryRequest> LibraryRequests { get; init; }
    public required List<ExportLibraryItem> LibraryItems { get; init; }
    public ExportProfile? Profile { get; init; }
    public ExportSettings? Settings { get; init; }
    public ExportSubscription? Subscription { get; init; }
    public required List<ExportNotification> Notifications { get; init; }
    public required List<ExportAiUsage> AiUsage { get; init; }
    public required List<ExportAiUsageMonth> AiUsageMonths { get; init; }
}

public record ExportMaterial(
    Guid Id,
    string Title,
    MaterialType Type,
    MaterialStatus Status,
    string? Link,
    JsonDocument? Content,
    bool IsActive,
    List<Guid> TagIds,
    List<ExportFile> Files,
    Guid? ImportedFromMaterialId,
    Guid? ImportedFromLibraryItemId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt);

// File content lives outside the database; the export lists what was attached
public record ExportFile(Guid Id, string FileName, string ContentType, long FileSize, DateTime CreatedAt);

public record ExportTag(Guid Id, string Name, string Color);

public record ExportNote(
    Guid Id,
    Guid MaterialId,
    string Title,
    NoteType Type,
    JsonDocument? TextContent,
    JsonDocument? DrawingContent,
    int Order,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ExportQuestion(
    Guid Id,
    Guid MaterialId,
    Guid? NoteId,
    Guid? DictionaryEntryId,
    string Title,
    string? Answer,
    QuestionType Type,
    QuestionDifficulty Difficulty,
    string? Explanation,
    bool IsActive,
    List<ExportOption> Options,
    ExportReviewState? Progress,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt);

public record ExportOption(Guid Id, string Name, int Position, bool IsCorrect);

// Spaced repetition state of a word or a question
public record ExportReviewState(
    FlashcardAlgorithm Algorithm,
    FlashcardState State,
    DateTime Due,
    double Stability,
    double Difficulty,
    int ScheduledDays,
    int Reps,
    int Lapses,
    DateTime? LastReviewAt);

public record ExportAttempt(
    Guid Id,
    Guid MaterialId,
    AttemptStatus Status,
    DateTime StartedAt,
    DateTime? FinishedAt,
    List<ExportResult> Results);

public record ExportResult(
    Guid Id,
    Guid QuestionId,
    string? UserAnswer,
    List<Guid> UserAnswerOptionIds,
    bool IsCorrect,
    double Score,
    ConfidenceLevel? Confidence,
    ReviewRating Rating,
    EvaluationMethod Method,
    string? AiExplanation,
    TimeSpan TimeSpent,
    DateTime? OverriddenAt,
    DateTime AnsweredAt);

public record ExportDictionary(
    Guid Id,
    Guid MaterialId,
    string Title,
    string? Description,
    List<ExportWord> Words,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ExportWord(
    Guid Id,
    string Term,
    string Translation,
    string? Transcription,
    PartOfSpeech? PartOfSpeech,
    string? GrammarInfo,
    string? Example,
    string? ExampleTranslation,
    string? Note,
    List<string> Synonyms,
    bool IsArchived,
    ExportReviewState? Progress,
    DateTime CreatedAt);

public record ExportFlashcardSession(
    Guid Id,
    Guid? DictionaryId,
    FlashcardSessionScope Scope,
    FlashcardSessionMode Mode,
    int CardsTotal,
    DateTime StartedAt,
    DateTime? FinishedAt);

public record ExportFlashcardReview(
    Guid Id,
    Guid? SessionId,
    Guid WordId,
    ReviewRating Rating,
    int? DurationMs,
    DateTime ReviewedAt);

public record ExportQuestionReviewSession(Guid Id, int QuestionsTotal, DateTime StartedAt, DateTime? FinishedAt);

public record ExportQuestionReview(
    Guid Id,
    Guid QuestionId,
    Guid? AttemptId,
    Guid? SessionId,
    ReviewRating Rating,
    DateTime ReviewedAt);

public record ExportShare(Guid Id, Guid MaterialId, int ImportsCount, DateTime CreatedAt, DateTime? RevokedAt);

public record ExportLibraryRequest(
    Guid Id,
    LibraryRequestType Type,
    LibraryRequestStatus Status,
    Guid? MaterialId,
    Guid? LibraryItemId,
    string Title,
    string AuthorNickname,
    string? Comment,
    LibraryRejectionReason? RejectionReason,
    string? ReviewComment,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

public record ExportLibraryItem(
    Guid Id,
    string Title,
    string AuthorNickname,
    LibraryItemStatus Status,
    int Version,
    int ImportsCount,
    DateTime PublishedAt,
    DateTime? RemovedAt);

public record ExportSettings(
    string? Email,
    bool EmailDailyReminder,
    bool EmailWeeklyDigest,
    bool EmailStreakReminder);

public record ExportSubscription(SubscriptionTier Tier, DateTime? CurrentPeriodEnd, DateTime CreatedAt);

public record ExportNotification(
    Guid Id,
    NotificationType Type,
    JsonDocument Data,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? EmailSentAt);

public record ExportAiUsage(
    AiFeature Feature,
    AiUsageStatus Status,
    string? Provider,
    string? Model,
    long InputTokens,
    long OutputTokens,
    DateTime CreatedAt);

// Older requests are kept only as monthly totals
public record ExportAiUsageMonth(
    int Year,
    int Month,
    AiFeature Feature,
    string Provider,
    string Model,
    int Requests,
    int FailedRequests,
    long InputTokens,
    long OutputTokens);

public record ExportProfile(
    string? TimeZone,
    string InterfaceLanguage,
    string? ExplanationLanguage,
    string? NativeLanguage,
    string? StudyLanguage,
    string? Nickname,
    int? DailyGoal,
    double? DesiredRetention,
    int? NewCardsPerDay,
    int? MaxReviewsPerDay,
    int? DailyReminderHour,
    int? StreakReminderHour,
    int? StreakFreezesPerMonth);
