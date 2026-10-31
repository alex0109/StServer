using System.Globalization;
using Application.Billing;

namespace Infrastructure.Billing;

public enum StripeTaxMode
{
    // Prices are charged as they are, VAT is the owner's own business
    None,
    // Stripe Tax adds VAT by the buyer's address; the owner registers and files it
    Automatic,
    // Managed Payments: Stripe is the merchant of record and files the VAT itself
    Managed,
}

// Stripe is configured only through environment variables, never appsettings. Without the key,
// the webhook secret and at least one price it stays off and the app shows no upgrade
public sealed record StripeSettings(
    string? SecretKey,
    string? WebhookSecret,
    IReadOnlyDictionary<(BillingPlan Plan, BillingInterval Interval), string> Prices,
    StripeTaxMode TaxMode,
    int TrialDays,
    // Where Stripe sends the user back; the app's own address, never one from the request
    Uri? SiteUrl)
{
    public const string SecretKeyVariable = "STRIPE_SECRET_KEY";
    public const string WebhookSecretVariable = "STRIPE_WEBHOOK_SECRET";
    public const string TaxModeVariable = "STRIPE_TAX_MODE";
    public const string TrialDaysVariable = "STRIPE_TRIAL_DAYS";

    private static readonly Dictionary<(BillingPlan, BillingInterval), string> PriceVariables = new()
    {
        [(BillingPlan.Plus, BillingInterval.Month)] = "STRIPE_PRICE_PLUS_MONTHLY",
        [(BillingPlan.Plus, BillingInterval.Year)] = "STRIPE_PRICE_PLUS_YEARLY",
        [(BillingPlan.Pro, BillingInterval.Month)] = "STRIPE_PRICE_PRO_MONTHLY",
        [(BillingPlan.Pro, BillingInterval.Year)] = "STRIPE_PRICE_PRO_YEARLY",
    };

    public bool IsComplete => SecretKey is not null && WebhookSecret is not null && SiteUrl is not null && Prices.Count > 0;

    public static StripeSettings FromEnvironment(string appUrl)
    {
        var prices = new Dictionary<(BillingPlan, BillingInterval), string>();
        foreach (var (key, variable) in PriceVariables)
        {
            if (Value(variable, "price_") is { } price) prices[key] = price;
        }

        // Secret or restricted key, live or test
        var secretKey = Value(SecretKeyVariable, "sk_") ?? Value(SecretKeyVariable, "rk_");

        return new StripeSettings(
            secretKey,
            Value(WebhookSecretVariable, "whsec_"),
            prices,
            TaxModeOf(Value(TaxModeVariable, "")),
            int.TryParse(Value(TrialDaysVariable, ""), NumberStyles.None, CultureInfo.InvariantCulture, out var days)
                ? Math.Clamp(days, 0, 90)
                : 0,
            SiteUrlOf(appUrl));
    }

    private static string? Value(string name, string prefix)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal) ? null : value;
    }

    private static StripeTaxMode TaxModeOf(string? value) => value?.ToLowerInvariant() switch
    {
        "managed" => StripeTaxMode.Managed,
        "automatic" => StripeTaxMode.Automatic,
        _ => StripeTaxMode.None,
    };

    // HTTPS only, plain HTTP is fine for a site on this machine
    private static Uri? SiteUrlOf(string appUrl)
    {
        if (!Uri.TryCreate(appUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var url)) return null;
        return url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback) ? url : null;
    }
}
