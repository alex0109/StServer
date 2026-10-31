using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Observability;

// The scraper proves itself with METRICS_TOKEN: as a bearer token, or as the basic auth password
// (Grafana Cloud scrape jobs send basic auth). Compared in constant time
public static class MetricsToken
{
    public static bool Allowed(string header, string token)
    {
        string? given = null;

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            given = header["Bearer ".Length..].Trim();
        }
        else if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                var colon = pair.IndexOf(':');
                given = colon >= 0 ? pair[(colon + 1)..] : null;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return given is not null
               && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token));
    }
}
