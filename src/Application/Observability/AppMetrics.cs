using System.Diagnostics.Metrics;

namespace Application.Observability;

// Business counters next to the HTTP ones. Plain .NET metrics: the API publishes them to Prometheus
// as studytracker_<name> (the meter name becomes the prefix), so nothing in Application depends on
// a monitoring library. Tags are low-cardinality on purpose: never user ids, titles or URLs
public static class AppMetrics
{
    public const string MeterName = "Studytracker";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AiRequests =
        Meter.CreateCounter<long>("ai.requests", description: "AI requests by feature, provider and outcome");

    public static readonly Counter<long> AiTokens =
        Meter.CreateCounter<long>("ai.tokens", description: "AI tokens by feature, provider and direction");

    public static readonly Counter<long> Reviews =
        Meter.CreateCounter<long>("reviews", description: "Reviews of words and questions");

    public static readonly Counter<long> AttemptsFinished =
        Meter.CreateCounter<long>("attempts.finished", description: "Finished self-test attempts");

    public static readonly Counter<long> MaterialsCreated =
        Meter.CreateCounter<long>("materials.created", description: "New materials by where they came from");

    public static readonly Counter<long> ArticleImports =
        Meter.CreateCounter<long>("imports", description: "Article imports from a link by outcome");

    public static readonly Counter<long> AccountActions =
        Meter.CreateCounter<long>("account.actions", description: "Data exports and account deletions");

    public static readonly Counter<long> BillingWebhooks =
        Meter.CreateCounter<long>("billing.webhooks", description: "Payment provider webhooks by outcome");

    public static void AiRequest(string feature, string provider, string outcome) =>
        AiRequests.Add(1, new("feature", feature), new("provider", provider), new("outcome", outcome));

    public static void AiTokensUsed(string feature, string provider, long input, long output)
    {
        AiTokens.Add(input, new("feature", feature), new("provider", provider), new("direction", "input"));
        AiTokens.Add(output, new("feature", feature), new("provider", provider), new("direction", "output"));
    }

    public static void Review(string kind, string source) =>
        Reviews.Add(1, new("kind", kind), new("source", source));

    public static void MaterialCreated(string source) =>
        MaterialsCreated.Add(1, new KeyValuePair<string, object?>("source", source));

    public static void ArticleImport(string outcome) =>
        ArticleImports.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public static void AccountAction(string action) =>
        AccountActions.Add(1, new KeyValuePair<string, object?>("action", action));

    public static void BillingWebhook(string outcome) =>
        BillingWebhooks.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
