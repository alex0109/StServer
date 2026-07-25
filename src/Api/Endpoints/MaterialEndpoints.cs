using Application.DTOs.Material;
using Application.Interfaces;

namespace Api.Endpoints;

public static class MaterialEndpoints
{
    public static void MapMaterialEndpoints(this WebApplication app)
    {
        var materialGroup = app.MapGroup("api/materials")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        materialGroup.MapGet("/", GetAllMaterials);
        materialGroup.MapGet("/{materialId}", GetMaterial);
        materialGroup.MapGet("/stats/data", GetStatisticalData);
        materialGroup.MapPost("/", CreateMaterial);
        materialGroup.MapPatch("/{materialId}", UpdateMaterial);
        materialGroup.MapPost("/{materialId}/tags/{tagId}", AddTagToMaterial);
        materialGroup.MapDelete("/{materialId}/tags/{tagId}", DeleteTagFromMaterial);
        materialGroup.MapDelete("/{materialId}", DeleteMaterial);
        
        static async Task<IResult> GetAllMaterials(IMaterialService service)
        {
            var result = await service.GetAllAsync();
            
            return TypedResults.Ok(result);
        };

        static async Task<IResult> GetMaterial(Guid materialId, IMaterialService service)
        {
            var result = await service.GetByIdAsync(materialId);
            
            if (result is null) return TypedResults.NotFound();
            
            return TypedResults.Ok(result);
        };

        static async Task<IResult> CreateMaterial(MaterialCreateDto materialCreateDto, IMaterialService service)
        {
            var result = await service.CreateAsync(materialCreateDto);
            
            return TypedResults.Created($"/api/materials/{result.Id}", result);
        };

        static async Task<IResult> UpdateMaterial(Guid materialId, MaterialUpdateDto materialUpdateDto, IMaterialService service)
        {
            var result = await service.UpdateAsync(materialId, materialUpdateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }
        
        static async Task<IResult> AddTagToMaterial(Guid materialId, Guid tagId, IMaterialService service)
        {
            var result = await service.AddTagToMaterialAsync(materialId, tagId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }
        
        static async Task<IResult> DeleteTagFromMaterial(Guid materialId, Guid tagId, IMaterialService service)
        {
            var result = await service.DeleteTagFromMaterialAsync(materialId, tagId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }
        
        static async Task<IResult> DeleteMaterial(Guid materialId, IMaterialService service)
        {
            bool result = await service.DeleteAsync(materialId);
            
            if (result) return TypedResults.NoContent();
            
            return TypedResults.NotFound();
        }

        static async Task<IResult> GetStatisticalData(IMaterialService service)
        {
            var result = await service.GetStatisticsAsync();
            
            return TypedResults.Json(result);
        };
    }
}