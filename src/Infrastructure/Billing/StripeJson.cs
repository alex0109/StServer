using System.Text.Json;

namespace Infrastructure.Billing;

// Reading Stripe's JSON: absent, null and mistyped fields come back as null instead of throwing
internal static class StripeJson
{
    public static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static long? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    public static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    public static string? Metadata(JsonElement element, string key) =>
        element.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? Text(metadata, key)
            : null;

    // A field that is an id, or the object itself when it was expanded
    public static string? IdOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Object => Text(value, "id"),
                _ => null,
            }
            : null;

    public static DateTime? Time(JsonElement element, string name) =>
        Number(element, name) is { } seconds ? FromUnix(seconds) : null;

    public static DateTime? FromUnix(long seconds) =>
        seconds is >= 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;

    public static long ToUnix(DateTime time) => new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)).ToUnixTimeSeconds();

    // The single price of a subscription and its item; since API 2025-03-31 the period lives on the item
    public static JsonElement? FirstItem(JsonElement subscription) =>
        subscription.TryGetProperty("items", out var items)
        && items.TryGetProperty("data", out var data)
        && data.ValueKind == JsonValueKind.Array
        && data.GetArrayLength() > 0
            ? data[0]
            : null;

    // A price's amount in each of its currencies (needs expand[]=currency_options), its own currency included
    public static Dictionary<string, long> CurrencyAmounts(JsonElement price, string currency, long amount)
    {
        var amounts = new Dictionary<string, long> { [currency] = amount };
        if (!price.TryGetProperty("currency_options", out var options) || options.ValueKind != JsonValueKind.Object) return amounts;

        foreach (var option in options.EnumerateObject())
        {
            if (option.Value.TryGetProperty("unit_amount", out var value) && value.ValueKind == JsonValueKind.Number)
                amounts[option.Name.ToLowerInvariant()] = value.GetInt64();
        }

        return amounts;
    }

    public static (string? Code, string? Message) ErrorOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? (Text(error, "code") ?? Text(error, "type"), Text(error, "message"))
                : (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
