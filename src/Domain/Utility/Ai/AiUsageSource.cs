namespace Domain.Utility.Ai;

// What paid for an AI request
public enum AiUsageSource
{
    Quota = 1,
    Credits = 2,
    // Free's media, once per account
    Trial = 3,
}
