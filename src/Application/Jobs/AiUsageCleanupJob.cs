using Application.Interfaces;

namespace Application.Jobs;

public class AiUsageCleanupJob
{
    private readonly IAiUsageService _aiUsageService;

    public AiUsageCleanupJob(IAiUsageService aiUsageService)
    {
        _aiUsageService = aiUsageService;
    }

    // Requests lost with a restarted process stay pending, this gives them back to the quota
    public async Task Cleanup()
    {
        await _aiUsageService.FailStaleAsync();
    }
}
