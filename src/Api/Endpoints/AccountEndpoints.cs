using Application.DTOs.Account;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var accountGroup = app.MapGroup("api/account")
            .RequireAuthorization();

        // A zip with everything of the user, built on request and not kept anywhere
        accountGroup.MapGet("/export", Export)
            .RequireRateLimiting("account-export");
        // Needs the account email and a sign in from the last minutes
        accountGroup.MapDelete("/", Delete)
            .RequireRateLimiting("account-delete");

        static async Task<IResult> Export(IAccountService service, HttpContext http)
        {
            var file = await service.ExportAsync();

            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.File(file.Content, "application/zip", file.FileName);
        }

        static async Task<IResult> Delete([FromBody] AccountDeleteDto dto, IAccountService service)
        {
            await service.DeleteAsync(dto);

            return TypedResults.NoContent();
        }
    }
}
