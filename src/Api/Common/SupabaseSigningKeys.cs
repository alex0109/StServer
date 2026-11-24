using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Api.Common;

// Supabase's token signing keys, read from its JWKS and kept fresh while the app runs: re-read every
// 10 minutes, and early (at most once a minute) when a token fails on its key. A key revoked in Supabase
// stops being trusted within minutes, and a rotation needs no restart. The early refresh is short on
// purpose: forged tokens use it up too, and the next real one must not wait long
public static class SupabaseSigningKeys
{
    // Supabase's asymmetric keys are ECC P-256 or RSA; no shared-secret algorithm is ever accepted
    public static readonly string[] Algorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256];

    public static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan UnknownKeyRefreshAtMostEvery = TimeSpan.FromMinutes(1);

    public static ConfigurationManager<OpenIdConnectConfiguration> Manager(string jwksUrl, string issuer)
    {
        var url = CheckedUrl(jwksUrl);

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            url.ToString(),
            new JwksRetriever(issuer),
            new HttpDocumentRetriever { RequireHttps = url.Scheme == Uri.UriSchemeHttps })
        {
            AutomaticRefreshInterval = RefreshEvery,
            RefreshInterval = UnknownKeyRefreshAtMostEvery,
            // By default a key that left the JWKS is still tried for an hour ("last known good"). Supabase
            // keeps a rotated key in its JWKS until it is revoked, so one that is gone was revoked: refuse it
            UseLastKnownGoodConfiguration = false,
        };
    }

    // The keys decide which tokens are genuine, so they never come over plain HTTP, except from a
    // local Supabase on this machine
    public static Uri CheckedUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url))
            throw new InvalidOperationException("JWKS_URL is not an absolute address");

        if (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback))
            return url;

        throw new InvalidOperationException("JWKS_URL must use https");
    }

    // The JWKS alone is the whole configuration: its keys, and the issuer tokens must name
    private sealed class JwksRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var keys = new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel));

            var configuration = new OpenIdConnectConfiguration { Issuer = issuer, JsonWebKeySet = keys };
            foreach (var key in keys.GetSigningKeys()) configuration.SigningKeys.Add(key);

            // An empty set would refuse every token until the next refresh: keep the last good one instead
            if (configuration.SigningKeys.Count == 0)
                throw new InvalidOperationException("The JWKS has no signing keys");

            return configuration;
        }
    }
}
