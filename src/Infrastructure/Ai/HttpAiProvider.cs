using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Ai;
using Application.Exceptions;
using Application.Interfaces;

namespace Infrastructure.Ai;

// Shared transport of providers called over plain HTTP: errors carry only the provider's
// error message, never the rest of the body, and the key travels only in a header
public abstract class HttpAiProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    protected HttpAiProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public abstract string Name { get; }

    public abstract bool IsConfigured { get; }

    public abstract Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken);

    protected async Task<JsonNode> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(Name);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderException($"{Name} connection error", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AiProviderException(AiProviderErrors.Describe(Name, (int)response.StatusCode, body));
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken)
                       ?? throw new AiProviderException($"{Name} returned an empty response");
            }
            catch (JsonException exception)
            {
                throw new AiProviderException($"{Name} returned invalid JSON", exception);
            }
        }
    }

    protected static JsonNode ToNode(JsonElement element) => JsonNode.Parse(element.GetRawText())!;
}
