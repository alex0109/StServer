using StServer.Application.Interfaces;

namespace StServer.Application.Jobs;

public class AttemptCleanupJob
{
    private readonly IAttemptService _attemptService;

    public AttemptCleanupJob(IAttemptService attemptService)
    {
        _attemptService = attemptService;
    }

    public async Task Cleanup()
    {
        await _attemptService.MarkAbandonedAttempts();
    }
}