using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Ai.Pricing;

namespace UnitTests;

// The cost model as shipped in the API's appsettings.json, read the way the API binds it
public static class TestPricing
{
    private static readonly Lazy<string> Section = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StServer.sln"))) directory = directory.Parent;
        var path = Path.Combine(directory?.FullName ?? throw new FileNotFoundException("StServer.sln"), "src", "Api", "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty(PricingOptions.SectionName).GetRawText();
    });

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // A fresh copy each time, so a test may change it
    public static PricingOptions Shipped() => JsonSerializer.Deserialize<PricingOptions>(Section.Value, Json)!;
}
