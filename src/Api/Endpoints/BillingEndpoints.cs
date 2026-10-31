using Application.Billing;
using Application.DTOs.Billing;
using Application.Interfaces;

namespace Api.Endpoints;

public static class BillingEndpoints
{
    // Provider webhooks are small JSON documents
    private const int MaxWebhookBytes = 256 * 1024;

    public static void MapBillingEndpoints(this WebApplication app)
    {
        var billingGroup = app.MapGroup("api/billing")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        billingGroup.MapGet("/status", GetStatus);
        billingGroup.MapPost("/checkout", Checkout);
        billingGroup.MapPost("/portal", Portal);

        // The plan cards are shown before signing in too: prices and limits only, nothing personal
        app.MapGet("api/billing/plans", Plans)
            .AllowAnonymous()
            .RequireRateLimiting("api");

        // Called by the payment provider: no user, the signature is the authentication
        app.MapPost("api/billing/webhook/{provider}", Webhook)
            .AllowAnonymous()
            .RequireRateLimiting("webhook");

        static async Task<IResult> GetStatus(IBillingService service)
        {
            return TypedResults.Ok(await service.GetStatusAsync());
        }

        static async Task<IResult> Checkout(CheckoutCreateDto dto, IBillingService service)
        {
            return TypedResults.Ok(new CheckoutDto(await service.CreateCheckoutAsync(dto.Plan, dto.Interval, dto.Language)));
        }

        static async Task<IResult> Portal(PortalCreateDto? dto, IBillingService service)
        {
            return TypedResults.Ok(new CheckoutDto(await service.CreatePortalAsync(dto?.Language)));
        }

        static async Task<IResult> Plans(IBillingCatalog catalog, CancellationToken cancellationToken)
        {
            return TypedResults.Ok(await catalog.GetPlansAsync(cancellationToken));
        }

        static async Task<IResult> Webhook(string provider, HttpRequest request, IBillingService service)
        {
            var header = service.SignatureHeaderOf(provider);
            if (header is null) return TypedResults.NotFound();

            if (request.ContentLength > MaxWebhookBytes) return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);

            // The signature is over the exact bytes, so the body is read raw, never re-serialised
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);
            if (buffer.Length > MaxWebhookBytes) return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var outcome = await service.ReceiveWebhookAsync(provider, request.Headers[header].ToString(), buffer.ToArray());

            return outcome switch
            {
                WebhookOutcome.UnknownProvider => TypedResults.NotFound(),
                WebhookOutcome.BadSignature => TypedResults.Unauthorized(),
                // Duplicates and events the app doesn't use are answered too, or the provider keeps retrying
                _ => TypedResults.Ok(),
            };
        }
    }
}
