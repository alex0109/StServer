using Application.Ai.Pricing;
using Domain.Utility.Ai;

namespace Application.Exceptions;

public enum AiDenialCode
{
    // The plan's quota for the period is used up; credits may pay instead
    QuotaExhausted = 1,
    // Credits were to pay, but there are not enough of them
    InsufficientCredits = 2,
    // Free's one-time media trial is spent
    TrialUsed = 3,
    // The input is longer than one request is priced for
    InputTooLarge = 4,
    // An anonymous session: AI needs an account with an email
    AccountRequired = 5,
}

// An AI request that can't run on what the user has: answered with 402 and what would let it run
public class AiUsageDeniedException : Exception
{
    public AiUsageDeniedException(
        AiDenialCode code,
        AiFeature feature,
        string message,
        int remaining = 0,
        int? creditsRequired = null,
        int creditBalance = 0,
        int? maxInputChars = null)
        : base(message)
    {
        Code = code;
        Feature = feature;
        Remaining = remaining;
        CreditsRequired = creditsRequired;
        CreditBalance = creditBalance;
        MaxInputChars = maxInputChars;
    }

    public AiDenialCode Code { get; }
    public AiFeature Feature { get; }
    public AiBucket Bucket => AiBuckets.Of(Feature);
    // Units of the quota still left, too few for this request
    public int Remaining { get; }
    // Null when the feature isn't sold for credits
    public int? CreditsRequired { get; }
    public int CreditBalance { get; }
    public int? MaxInputChars { get; }

    public static AiUsageDeniedException InputTooLarge(AiFeature feature, int maxInputChars) =>
        new(AiDenialCode.InputTooLarge, feature, $"The text is too long for one AI request: at most {maxInputChars} characters", maxInputChars: maxInputChars);
}
