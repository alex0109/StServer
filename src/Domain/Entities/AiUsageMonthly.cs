using System.ComponentModel.DataAnnotations;
using Domain.Utility.Ai;

namespace Domain.Entities;

// Monthly totals of AI requests older than a year, the single requests are removed
public class AiUsageMonthly
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public AiFeature Feature { get; set; }
    [MaxLength(40)]
    public string Provider { get; set; } = "";
    [MaxLength(100)]
    public string Model { get; set; } = "";
    public int Requests { get; set; }
    public int FailedRequests { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
