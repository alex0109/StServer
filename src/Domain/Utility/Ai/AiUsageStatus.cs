namespace Domain.Utility.Ai;

public enum AiUsageStatus
{
    // Quota place is taken while the AI request runs
    Pending = 0,
    Succeeded = 1,
    // Failed requests do not count towards the quota
    Failed = 2
}
