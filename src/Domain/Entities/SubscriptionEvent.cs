using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Utility.Subscription;

namespace Domain.Entities;

// One webhook of a billing provider. The provider's event id makes a repeated delivery a no-op.
// The payload is the parsed event, not the raw body, so no card or address data is stored
public class SubscriptionEvent
{
    public Guid Id { get; set; }
    public required SubscriptionProvider Provider { get; set; }

    [MaxLength(200)]
    public required string ProviderEventId { get; set; }

    [MaxLength(100)]
    public required string Type { get; set; }

    public Guid? UserId { get; set; }

    public required JsonDocument Payload { get; set; }

    public required DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(500)]
    public string? Error { get; set; }
}
