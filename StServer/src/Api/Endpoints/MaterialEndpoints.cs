using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StServer.Infrastructure.Data;
using StServer.Application.DTOs;
using StServer.Application.Mappers;
using StServer.Domain.Entities;
using System.Diagnostics;

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
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            var materials = await db.Materials.ToArrayAsync();
            serviceSw.Stop();

            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");
            
            return TypedResults.Ok(materials.Select(MaterialMapper.ToDto).ToArray());
        };

        static async Task<IResult> GetMaterial(Guid id, AppDbContext db)
        {
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            var item = await db.Materials.FindAsync(id);
            serviceSw.Stop();

            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");
            
            return item is Material material ? TypedResults.Ok(MaterialMapper.ToDto(material)) : TypedResults.NotFound();
        };

        static async Task<IResult> CreateMaterial(MaterialItemDto materialItemDto, AppDbContext db)
        {
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            var entity = MaterialMapper.ToEntity(materialItemDto);
            serviceSw.Stop();
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            
            db.Materials.Add(entity);
            await db.SaveChangesAsync();
            
            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");
            
            return TypedResults.Created($"/materials/{entity.Id}", MaterialMapper.ToDto(entity));
        };

        static async Task<IResult> UpdateMaterial(Guid id, MaterialUpdateDto materialItemDto, AppDbContext db)
        {
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            var material = await db.Materials.FindAsync(id);
            serviceSw.Stop();

            if (material is null) return TypedResults.NotFound();

            if (materialItemDto.Title is not null) material.Title = materialItemDto.Title;
            if (materialItemDto.Type is not null) material.Type = materialItemDto.Type;
            if (materialItemDto.Tags is not null) material.Tags = materialItemDto.Tags;
            if (materialItemDto.Link is not null) material.Link = materialItemDto.Link;
            if (materialItemDto.Description is not null)
            {
                material.Description = JsonSerializer.SerializeToDocument(materialItemDto.Description);
            }
            if (materialItemDto.Status is not null) material.Status = materialItemDto.Status;
            material.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            
            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");

            return TypedResults.Ok(MaterialMapper.ToDto(material));
        }

        static async Task<IResult> DeleteMaterial(Guid id, AppDbContext db)
        {
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            if (await db.Materials.FindAsync(id) is Material material)
            {
                db.Materials.Remove(material);
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            }
            serviceSw.Stop();
            
            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");

            return TypedResults.NotFound();
        }

        static async Task<IResult> GetStatisticalData(AppDbContext db)
        {
            var sw = Stopwatch.StartNew();

            var serviceSw = Stopwatch.StartNew();
            var materials = await db.Materials.ToArrayAsync();

            var statuses = materials.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count());
            
            var types = materials.GroupBy(m => m.Status)
                .ToDictionary(g => g.Key, g => g.Count());
            
            sw.Stop();
            
            Console.WriteLine($"SERVICE: {serviceSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");

            return TypedResults.Json(new {count = materials.Length, statuses, types});
        };
    }
}