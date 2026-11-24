using System.ComponentModel.DataAnnotations;
using Domain.Utility.Ai;

namespace Domain.Entities;

// One AI request of a user: the quota counter and the cost log
public class AiUsage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AiFeature Feature { get; set; }
    public AiUsageStatus Status { get; set; }
    [MaxLength(40)]
    public string? Provider { get; set; }
    [MaxLength(100)]
    public string? Model { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }

    // How much of the feature it took: 1 request for most, minutes for a video
    public int Units { get; set; } = 1;
    // Paid from the plan's quota, from credits, or from Free's one-time media trial
    public AiUsageSource Source { get; set; } = AiUsageSource.Quota;
    // Credits held for it; given back with the request when it fails
    public int Credits { get; set; }
    // Answered by a provider after the first one, which costs more
    public bool IsFallback { get; set; }
    // The tokens at the prices of AiPricing, USD; null for a model without a price
    public decimal? EstimatedCost { get; set; }
    // Length of the video the request watched
    public int? VideoSeconds { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
