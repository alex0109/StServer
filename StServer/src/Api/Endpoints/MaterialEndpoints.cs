using Microsoft.EntityFrameworkCore;
using StServer.Infrastructure.Data;
using StServer.Application.DTOs.Material;
using StServer.Application.Mappers;
using StServer.Domain.Entities;

namespace StServer.Api.Endpoints;

public static class MaterialEndpoints
{
    public static void MapMaterialEndpoints(this WebApplication app)
    {
        var materialGroup = app.MapGroup("api/materials").RequireAuthorization();

        materialGroup.MapGet("/", GetAllMaterials);
        materialGroup.MapGet("/{id}", GetMaterial);
        materialGroup.MapGet("/stats/data", GetStatisticalData);
        materialGroup.MapPost("", CreateMaterial);
        materialGroup.MapPatch("/{id}", UpdateMaterial);
        materialGroup.MapDelete("/{id}", DeleteMaterial);
        
        static async Task<IResult> GetAllMaterials(AppDbContext db)
        {
            var materials = await db.Materials.ToArrayAsync();

            var response = materials.Select(MaterialMapper.ToDto).ToArray();
            
            return TypedResults.Ok(response);
        };

        static async Task<IResult> GetMaterial(Guid id, AppDbContext db)
        {
            var item = await db.Materials.FindAsync(id);
            
            if (item is null) return TypedResults.NotFound();
            
            var response = MaterialMapper.ToDto(item);
            
            return TypedResults.Ok(response);
        };

        static async Task<IResult> CreateMaterial(MaterialCreateDto materialCreateDto, AppDbContext db)
        {
            var entity = MaterialMapper.ToEntity(materialCreateDto);
            
            db.Materials.Add(entity);
            await db.SaveChangesAsync();
            
            var response = MaterialMapper.ToDto(entity);
            
            return TypedResults.Created($"/materials/{entity.Id}", response);
        };

        static async Task<IResult> UpdateMaterial(Guid id, MaterialUpdateDto materialUpdateDto, AppDbContext db)
        {
            var material = await db.Materials.FindAsync(id);

            if (material is null) return TypedResults.NotFound();

            MaterialMapper.ApplyUpdate(material, materialUpdateDto);

            await db.SaveChangesAsync();

            var response = MaterialMapper.ToDto(material);

            return TypedResults.Ok(response);
        }

        static async Task<IResult> DeleteMaterial(Guid id, AppDbContext db)
        {
            if (await db.Materials.FindAsync(id) is Material material)
            {
                db.Materials.Remove(material);
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            }

            return TypedResults.NotFound();
        }

        static async Task<IResult> GetStatisticalData(AppDbContext db)
        {
            var materials = await db.Materials.ToArrayAsync();

            var statuses = materials.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count());
            
            var types = materials.GroupBy(m => m.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            var response = new { count = materials.Length, statuses, types };
            
            return TypedResults.Json(response);
        };
    }
}