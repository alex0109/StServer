using System.Net;

namespace UnitTests.Billing;

// Answers Stripe calls from a queue (or by path) and keeps what was sent. Safe for parallel requests
internal sealed class FakeStripeHandler : HttpMessageHandler
{
    private readonly Lock _lock = new();

    public Queue<string> Answers { get; } = new();
    public Queue<HttpStatusCode> Statuses { get; } = new();
    public Func<string, string>? Respond { get; set; }
    // A status chosen by the request's path and form, for answers that don't depend on order
    public Func<string, Dictionary<string, string>, HttpStatusCode?>? StatusFor { get; set; }
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<Dictionary<string, string>> Forms { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>();
        if (request.Content is not null)
        {
            var text = await request.Content.ReadAsStringAsync(cancellationToken);
            form = text.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
        }

        lock (_lock)
        {
            Requests.Add(request);
            if (request.Content is not null) Forms.Add(form);

            var path = request.RequestUri!.AbsolutePath;
            var body = Respond?.Invoke(path) ?? (Answers.Count > 0 ? Answers.Dequeue() : "{}");
            var status = StatusFor?.Invoke(path, form) ?? (Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
