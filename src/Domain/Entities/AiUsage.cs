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
    public required DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
