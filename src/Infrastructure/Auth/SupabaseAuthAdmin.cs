using System.Net;
using System.Net.Http.Headers;
using Application.Interfaces;

namespace Infrastructure.Auth;

// Supabase Auth admin API. The service role key bypasses every rule of the project,
// so it comes only from the environment and goes only over HTTPS
public sealed class SupabaseAuthAdmin : IAuthUserAdmin
{
    public const string HttpClientName = "SupabaseAdmin";
    public const string ServiceKeyVariable = "SUPABASE_SERVICE_ROLE_KEY";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri? _baseUrl;
    private readonly string? _key;

    public SupabaseAuthAdmin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        var key = Environment.GetEnvironmentVariable(ServiceKeyVariable)?.Trim();
        _key = string.IsNullOrEmpty(key) ? null : key;
        _baseUrl = SafeBaseUrl(Environment.GetEnvironmentVariable("SUPABASE_URL"));
    }

    public bool IsConfigured => _key is not null && _baseUrl is not null;

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_key is null || _baseUrl is null) throw new InvalidOperationException("Supabase admin is not configured");

        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_baseUrl, $"auth/v1/admin/users/{userId:D}"));
        request.Headers.Add("apikey", _key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

        // Already deleted, by an earlier run or by hand in the dashboard
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;

        // Only the status: the body may echo the request
        throw new HttpRequestException($"Supabase admin returned {(int)response.StatusCode}", null, response.StatusCode);
    }

    // Plain HTTP only for a local Supabase on this machine
    public static Uri? SafeBaseUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var url)) return null;

        return url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback) ? url : null;
    }
}
