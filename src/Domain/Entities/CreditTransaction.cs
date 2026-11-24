using System.ComponentModel.DataAnnotations;
using Domain.Utility.Ai;

namespace Domain.Entities;

// One change of a credit balance. Never changed or removed; the idempotency key makes a repeated
// webhook or a repeated release a no-op
public class CreditTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Plus for purchases and returns, minus for spending and refunds
    public int Delta { get; set; }
    public CreditReason Reason { get; set; }

    // The AI request that spent or returned them, and its feature for the history
    public Guid? UsageId { get; set; }
    public AiFeature? Feature { get; set; }

    [MaxLength(40)]
    public string? PackId { get; set; }

    // The Stripe payment a purchase came from, so its refund finds it
    [MaxLength(200)]
    public string? StripeReference { get; set; }

    [MaxLength(200)]
    public required string IdempotencyKey { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }

    public required DateTime CreatedAt { get; set; }
}
