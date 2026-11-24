using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Auth;

// Supabase Auth admin API. The service role key bypasses every rule of the project,
// so it comes only from the environment and goes only over HTTPS
public sealed class SupabaseAuthAdmin : IAuthUserAdmin
{
    public const string HttpClientName = "SupabaseAdmin";
    public const string ServiceKeyVariable = "SUPABASE_SERVICE_ROLE_KEY";

    // An email rarely changes; the panel asks for the same ones on every visit
    private static readonly TimeSpan EmailCacheFor = TimeSpan.FromHours(6);
    private const int ParallelLookups = 4;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SupabaseAuthAdmin> _logger;
    private readonly Uri? _baseUrl;
    private readonly string? _key;

    public SupabaseAuthAdmin(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<SupabaseAuthAdmin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;

        var key = Environment.GetEnvironmentVariable(ServiceKeyVariable)?.Trim();
        _key = string.IsNullOrEmpty(key) ? null : key;
        _baseUrl = SafeBaseUrl(Environment.GetEnvironmentVariable("SUPABASE_URL"));
    }

    public bool IsConfigured => _key is not null && _baseUrl is not null;

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_key is null || _baseUrl is null) throw new InvalidOperationException("Supabase admin is not configured");

        using var request = Request(HttpMethod.Delete, userId);
        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

        // Already deleted, by an earlier run or by hand in the dashboard
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;

        // Only the status: the body may echo the request
        throw new HttpRequestException($"Supabase admin returned {(int)response.StatusCode}", null, response.StatusCode);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var emails = new Dictionary<Guid, string>();
        if (_key is null || _baseUrl is null) return emails;

        var missing = new List<Guid>();
        foreach (var userId in userIds.Distinct())
        {
            if (_cache.TryGetValue(EmailKey(userId), out string? email) && email is not null) emails[userId] = email;
            else missing.Add(userId);
        }

        var found = new ConcurrentDictionary<Guid, string>();
        await Parallel.ForEachAsync(
            missing,
            new ParallelOptions { MaxDegreeOfParallelism = ParallelLookups, CancellationToken = cancellationToken },
            async (userId, token) =>
            {
                if (await LookUpEmailAsync(userId, token) is { } email) found[userId] = email;
            });

        foreach (var (userId, email) in found)
        {
            _cache.Set(EmailKey(userId), email, EmailCacheFor);
            emails[userId] = email;
        }

        return emails;
    }

    private async Task<string?> LookUpEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = Request(HttpMethod.Get, userId);
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            return document.RootElement.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String
                ? email.GetString()
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            // The panel shows the id instead
            _logger.LogWarning("Supabase admin: no email for a user ({Reason})", exception.GetType().Name);
            return null;
        }
    }

    private HttpRequestMessage Request(HttpMethod method, Guid userId)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUrl!, $"auth/v1/admin/users/{userId:D}"));
        request.Headers.Add("apikey", _key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        return request;
    }

    private static string EmailKey(Guid userId) => $"auth:email:{userId:N}";

    // Plain HTTP only for a local Supabase on this machine
    public static Uri? SafeBaseUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var url)) return null;

        return url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback) ? url : null;
    }
}
