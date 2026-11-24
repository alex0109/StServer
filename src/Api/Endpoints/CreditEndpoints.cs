using Application.DTOs.Billing;
using Application.Interfaces;

namespace Api.Endpoints;

// Prepaid AI credits: balance, history, auto-use and buying packs
public static class CreditEndpoints
{
    public static void MapCreditEndpoints(this WebApplication app)
    {
        var credits = app.MapGroup("api/credits")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        credits.MapGet("/", Wallet);
        credits.MapGet("/history", History);
        credits.MapPut("/settings", Settings);
        credits.MapPost("/checkout", Checkout);

        // Packs and what each feature costs: shown before signing in too, nothing personal
        app.MapGet("api/credits/pricing", (ICreditService service) => TypedResults.Ok(service.GetPricing()))
            .AllowAnonymous()
            .RequireRateLimiting("api");

        static async Task<IResult> Wallet(ICreditService service) => TypedResults.Ok(await service.GetWalletAsync());

        static async Task<IResult> History(ICreditService service, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await service.GetHistoryAsync(page, pageSize));

        static async Task<IResult> Settings(CreditSettingsDto dto, ICreditService service) =>
            TypedResults.Ok(await service.SetAutoUseAsync(dto.AutoUseForStudy));

        static async Task<IResult> Checkout(CreditCheckoutCreateDto dto, ICreditService service) =>
            TypedResults.Ok(new CheckoutDto(await service.CreateCheckoutAsync(dto.PackId, dto.Language)));
    }
}
