using StServer.Application.DTOs.Material;
using StServer.Application.Interfaces;

namespace StServer.Api.Endpoints;

public static class MaterialEndpoints
{
    public static void MapMaterialEndpoints(this WebApplication app)
    {
        var materialGroup = app.MapGroup("api/materials").RequireAuthorization();

        materialGroup.MapGet("/", GetAllMaterials);
        materialGroup.MapGet("/{id}", GetMaterial);
        materialGroup.MapGet("/{id}/attempts", GetAttempts);
        materialGroup.MapGet("/stats/data", GetStatisticalData);
        materialGroup.MapPost("", CreateMaterial);
        materialGroup.MapPatch("/{id}", UpdateMaterial);
        materialGroup.MapPatch("/{id}/tags", SyncMaterialTags);
        materialGroup.MapDelete("/{id}", DeleteMaterial);
        
        static async Task<IResult> GetAllMaterials(IMaterialService service)
        {
            var result = await service.GetAllAsync();
            
            return TypedResults.Ok(result);
        };

        static async Task<IResult> GetMaterial(Guid id, IMaterialService service)
        {
            var result = await service.GetByIdAsync(id);
            
            if (result is null) return TypedResults.NotFound();
            
            return TypedResults.Ok(result);
        };

        static async Task<IResult> CreateMaterial(MaterialCreateDto materialCreateDto, IMaterialService service)
        {
            var result = await service.CreateAsync(materialCreateDto);
            
            return TypedResults.Created($"/api/materials/{result.Id}", result);
        };

        static async Task<IResult> UpdateMaterial(Guid id, MaterialUpdateDto materialUpdateDto, IMaterialService service)
        {
            var result = await service.UpdateAsync(id, materialUpdateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> SyncMaterialTags(Guid id, List<Guid> tagIds, IMaterialService service)
        {
            var result = await service.SyncMaterialTags(id, tagIds);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> DeleteMaterial(Guid id, IMaterialService service)
        {
            bool result = await service.DeleteAsync(id);
            
            if (result) return TypedResults.NoContent();
            
            return TypedResults.NotFound();
        }

        static async Task<IResult> GetStatisticalData(IMaterialService service)
        {
            var result = await service.GetStatisticsAsync();
            
            return TypedResults.Json(result);
        };

        static async Task<IResult> GetAttempts(Guid id, IMaterialService service)
        {
            var result = await service.GetAttempts(id);
                
            return TypedResults.Ok(result);
        }
    }
}