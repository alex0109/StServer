using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StServer.Data;
using StServer.DTOs;
using StServer.Mappers;
using StServer.Entities;

namespace StServer.Endpoints;

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

        app.Run();

        static async Task<IResult> GetAllMaterials(AppDbContext db)
        {
            
            var materials = await db.Materials.ToArrayAsync();
            
            return TypedResults.Ok(materials.Select(MaterialMapper.ToDto).ToList());
        };

        static async Task<IResult> GetMaterial(int id, AppDbContext db)
        {
            return await db.Materials.FindAsync(id)
                is Material material 
                    ? TypedResults.Ok(MaterialMapper.ToDto(material))
                    : TypedResults.NotFound();
        };

        static async Task<IResult> CreateMaterial(MaterialItemDto materialItemDto, AppDbContext db)
        {
            var entity = MaterialMapper.ToEntity(materialItemDto);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            
            db.Materials.Add(entity);
            await db.SaveChangesAsync();
            
            return TypedResults.Created($"/todo/{entity.Id}", MaterialMapper.ToDto(entity));
        };

        static async Task<IResult> UpdateMaterial(int id, MaterialUpdateDto materialItemDto, AppDbContext db)
        {
            var material = await db.Materials.FindAsync(id);

            if (material is null) return TypedResults.NotFound();

            if (materialItemDto.Title is not null) material.Title = materialItemDto.Title;
            if (materialItemDto.Type is not null) material.Type = materialItemDto.Type;
            if (materialItemDto.Tags is not null) material.Tags = materialItemDto.Tags;
            if (materialItemDto.Link is not null) material.Link = materialItemDto.Link;
            if (materialItemDto.Description is not null)
            {
                material.Description = JsonSerializer.Serialize(materialItemDto.Description);
            }
            if (materialItemDto.Status is not null) material.Status = materialItemDto.Status;
            material.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return TypedResults.Ok(MaterialMapper.ToDto(material));
        }

        static async Task<IResult> DeleteMaterial(int id, AppDbContext db)
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

            return TypedResults.Json(new {count = materials.Length, statuses, types});
        };
    }
}