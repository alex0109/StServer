using Application.Interfaces;
using Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

public class FlashcardSessionCleanupJob
{
    private readonly IFlashcardRepository _repo;
    private readonly TimeProvider _time;
    private readonly ILogger<FlashcardSessionCleanupJob> _logger;

    public FlashcardSessionCleanupJob(
        IFlashcardRepository repo,
        TimeProvider time,
        ILogger<FlashcardSessionCleanupJob> logger)
    {
        _repo = repo;
        _time = time;
        _logger = logger;
    }

    // Same rule as abandoned attempts: a session not finished within a day is closed
    public async Task Cleanup()
    {
        var closed = await _repo.CloseStaleSessionsAsync(_time.GetUtcNow().UtcDateTime.AddDays(-1));
        _logger.LogInformation("Closed {Count} unfinished flashcard sessions", closed);
    }
}

public class DeletedQuestionsPurgeJob
{
    private const int KeepDays = 30;

    private readonly IQuestionRepository _repo;
    private readonly TimeProvider _time;
    private readonly ILogger<DeletedQuestionsPurgeJob> _logger;

    public DeletedQuestionsPurgeJob(
        IQuestionRepository repo,
        TimeProvider time,
        ILogger<DeletedQuestionsPurgeJob> logger)
    {
        _repo = repo;
        _time = time;
        _logger = logger;
    }

    public async Task Purge()
    {
        var removed = await _repo.PurgeDeletedAsync(_time.GetUtcNow().UtcDateTime.AddDays(-KeepDays));
        _logger.LogInformation("Removed {Count} questions deleted more than {Days} days ago", removed, KeepDays);
    }
}

public class AiUsageRollupJob
{
    private readonly IAiUsageRepository _repo;
    private readonly TimeProvider _time;
    private readonly ILogger<AiUsageRollupJob> _logger;

    public AiUsageRollupJob(IAiUsageRepository repo, TimeProvider time, ILogger<AiUsageRollupJob> logger)
    {
        _repo = repo;
        _time = time;
        _logger = logger;
    }

    // Whole months older than a year become monthly totals
    public async Task RollUp()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var cutoff = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-12);

        var removed = await _repo.RollUpBeforeAsync(cutoff);
        _logger.LogInformation("Rolled {Count} AI requests older than {Cutoff:yyyy-MM} into monthly totals", removed, cutoff);
    }
}

public class NotificationCleanupJob
{
    private readonly INotificationRepository _repo;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _time;

    public NotificationCleanupJob(INotificationRepository repo, NotificationOptions options, TimeProvider time)
    {
        _repo = repo;
        _options = options;
        _time = time;
    }

    public async Task Cleanup()
    {
        await _repo.DeleteCreatedBeforeAsync(_time.GetUtcNow().UtcDateTime.AddDays(-_options.KeepDays));
    }
}

// The monthly feedback limit only looks at the current month: older records are not needed
public class FeedbackLogCleanupJob
{
    private readonly IFeedbackLogRepository _repo;
    private readonly TimeProvider _time;

    public FeedbackLogCleanupJob(IFeedbackLogRepository repo, TimeProvider time)
    {
        _repo = repo;
        _time = time;
    }

    public async Task Cleanup()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await _repo.DeleteCreatedBeforeAsync(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1));
    }
}
