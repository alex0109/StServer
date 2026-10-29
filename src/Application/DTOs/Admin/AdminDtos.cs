using Domain.Utility.Admin;
using Domain.Utility.Ai;

namespace Application.DTOs.Admin;

public record AdminMeDto(Guid UserId, string? Email, string AssuranceLevel);

public record AdminAuditEntryDto(
    Guid Id, Guid AdminUserId, AdminAction Action, Guid? TargetId, string? Details, string? Ip, string? UserAgent, DateTime CreatedAt);

// Users are everyone the app has seen: they created something, studied or used AI
public record AdminUsersDto(int Total, int ActiveToday, int Active7Days, int Active30Days, int New7Days, int New30Days);

public record AdminContentDto(
    int Materials, int Notes, int Questions, int Dictionaries, int Words, int Tags,
    int FinishedAttempts, int FlashcardReviews, int ActiveShares);

public record AdminLibraryDto(int Published, int PendingRequests, int Imports, int RequestsLast30Days);

public record AdminFeedbackDto(int ThisMonth, int LastMonth);

// One UTC day of activity
public record AdminDayDto(
    DateOnly Date, int ActiveUsers, int NewUsers, int FinishedAttempts, int FlashcardReviews, int NewMaterials, int AiRequests);

public record AdminOverviewDto(
    AdminUsersDto Users,
    AdminContentDto Content,
    AdminLibraryDto Library,
    AdminFeedbackDto Feedback,
    List<AdminDayDto> Days,
    DateTime GeneratedAt);

public record AdminAiTotalsDto(
    long AllTime, int Today, int Last7Days, int Last30Days, int ThisMonth,
    int FailedLast30Days, double AveragePerDay30, double AveragePerActiveUser30, int AiUsers30);

public record AdminAiTokensDto(long InputLast30Days, long OutputLast30Days, long InputThisMonth, long OutputThisMonth);

// Estimates in USD from AiPricing; requests of models without a price are counted, not priced
public record AdminAiCostDto(
    decimal Today, decimal Last7Days, decimal Last30Days, decimal ThisMonth, decimal AllTime,
    decimal AveragePerRequest30, int UnpricedRequests30, string? PricesCheckedOn);

public record AdminAiFeatureDto(AiFeature Feature, int Requests, int Failed, decimal Cost);

public record AdminAiModelDto(string Provider, string Model, int Requests, int Failed, long InputTokens, long OutputTokens, decimal? Cost);

public record AdminAiDayDto(DateOnly Date, int Requests, int Failed, decimal Cost);

public record AdminAiUserDto(Guid UserId, int Requests, decimal Cost);

public record AdminAiStatsDto(
    AdminAiTotalsDto Totals,
    AdminAiTokensDto Tokens,
    AdminAiCostDto Cost,
    List<AdminAiFeatureDto> ByFeature,
    List<AdminAiModelDto> ByModel,
    List<AdminAiDayDto> Days,
    List<AdminAiUserDto> TopUsers,
    DateTime GeneratedAt);

// Raw rows the AI statistics are built from, already grouped by the database
public record AiUsageGroupRow(DateOnly Date, AiFeature Feature, string? Provider, string? Model, Guid UserId, AiUsageStatus Status, int Requests, long InputTokens, long OutputTokens);

public record AiMonthlyRow(string Provider, string Model, int Requests, int Failed, long InputTokens, long OutputTokens);

// Raw material for the overview, counted by the database
public record AdminOverviewRaw(
    Dictionary<Guid, DateTime> FirstSeen,
    List<(Guid UserId, DateOnly Day)> ActiveDays,
    AdminContentDto Content,
    AdminLibraryDto Library,
    AdminFeedbackDto Feedback,
    Dictionary<DateOnly, int> FinishedAttemptsByDay,
    Dictionary<DateOnly, int> ReviewsByDay,
    Dictionary<DateOnly, int> NewMaterialsByDay,
    Dictionary<DateOnly, int> AiRequestsByDay);
