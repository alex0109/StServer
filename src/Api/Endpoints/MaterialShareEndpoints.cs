using Application.Interfaces;

namespace Api.Endpoints;

public static class MaterialShareEndpoints
{
    public static void MapMaterialShareEndpoints(this WebApplication app)
    {
        var materialShareGroup = app.MapGroup("api/materials")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        materialShareGroup.MapGet("/{materialId:guid}/share", GetShare);
        materialShareGroup.MapPost("/{materialId:guid}/share", CreateShare);
        materialShareGroup.MapDelete("/{materialId:guid}/share", RevokeShare);

        var shareGroup = app.MapGroup("api/shares")
            .RequireRateLimiting("api");

        // Preview is public, so a link can be opened before signing in
        shareGroup.MapGet("/{token}", GetPreview).AllowAnonymous();
        shareGroup.MapPost("/{token}/import", Import).RequireAuthorization();

        static async Task<IResult> GetShare(Guid materialId, IMaterialShareService service)
        {
            var result = await service.GetShareAsync(materialId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> CreateShare(Guid materialId, IMaterialShareService service)
        {
            var result = await service.CreateShareAsync(materialId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> RevokeShare(Guid materialId, IMaterialShareService service)
        {
            bool result = await service.RevokeShareAsync(materialId);

            if (result) return TypedResults.NoContent();

            return TypedResults.NotFound();
        }

        static async Task<IResult> GetPreview(string token, IMaterialShareService service)
        {
            var result = await service.GetPreviewAsync(token);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> Import(string token, IMaterialShareService service)
        {
            var result = await service.ImportAsync(token);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Created($"/api/materials/{result.Id}", result);
        }
    }
}
