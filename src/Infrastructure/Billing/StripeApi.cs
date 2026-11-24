using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Billing;

// Calls to Stripe's REST API with the secret key and one pinned API version. Failures become
// BillingUnavailableException with Stripe's error code in the log, never the key or the body
public sealed class StripeApi
{
    public const string HttpClientName = "Stripe";

    // Requests use one API version, so an upgrade in the dashboard doesn't change what the app sends
    public const string ApiVersion = "2026-09-30.endive";

    // A list longer than this many pages of 100 is cut: reports stay fast, the log tells
    private const int MaxPages = 50;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeApi> _logger;

    public StripeApi(IHttpClientFactory httpClientFactory, StripeSettings settings, ILogger<StripeApi> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<JsonElement> SendAsync(
        HttpMethod method,
        string path,
        List<KeyValuePair<string, string>>? form,
        string what,
        CancellationToken cancellationToken,
        bool missingIsFine = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
        request.Headers.Add("Stripe-Version", ApiVersion);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        if (missingIsFine && response.StatusCode == HttpStatusCode.NotFound) return default;

        // Stripe's error code and message help, they never contain the key
        var (code, message) = StripeJson.ErrorOf(body);
        _logger.LogWarning("Stripe answered {Status} to the {What} request: {Code} {Message}",
            (int)response.StatusCode, what, code, message);
        throw new BillingUnavailableException($"Stripe answered {(int)response.StatusCode}");
    }

    // Every object of a list, page after page; query is the list's own filter, without limit and cursor
    public async Task<List<JsonElement>> ListAsync(string path, string query, string what, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        string? after = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var cursor = after is null ? "" : $"&starting_after={Uri.EscapeDataString(after)}";
            var list = await SendAsync(HttpMethod.Get, $"{path}?limit=100&{query}{cursor}", null, what, cancellationToken);
            if (!list.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) break;

            items.AddRange(data.EnumerateArray());
            if (!StripeJson.Flag(list, "has_more") || data.GetArrayLength() == 0) return items;
            after = StripeJson.Text(data[data.GetArrayLength() - 1], "id");
            if (after is null) return items;
        }

        _logger.LogWarning("The Stripe {What} list was cut after {Pages} pages", what, MaxPages);
        return items;
    }
}
