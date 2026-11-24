using Application.Ai.Pricing;
using Domain.Utility.Ai;

namespace Application.DTOs.Billing;

public record CreditPackDto(string Id, int Credits, decimal Price, string Currency);

// What one block of a feature costs in credits; Block: units (words, minutes) one charge covers
public record CreditPriceDto(AiFeature Feature, AiBucket Bucket, int Credits, int Block);

public record CreditPricingDto(bool CheckoutAvailable, List<CreditPackDto> Packs, List<CreditPriceDto> Prices);

public record CreditWalletDto(int Balance, bool AutoUseForStudy);

public record CreditTransactionDto(Guid Id, int Delta, CreditReason Reason, AiFeature? Feature, string? PackId, string? Note, DateTime CreatedAt);

public record CreditHistoryDto(List<CreditTransactionDto> Items, int Total, int Page, int PageSize);

public class CreditSettingsDto
{
    public bool AutoUseForStudy { get; set; }
}

public class CreditCheckoutCreateDto
{
    public string? PackId { get; set; }
    public string? Language { get; set; }
}
