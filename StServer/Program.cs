using StServer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

String? connectionString = configuration.GetConnectionString("MyDB");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// app.Urls.Add("http://localhost:3000");

var materialGroup = app.MapGroup("api/materials");

materialGroup.MapGet("/", GetAllMaterials);
materialGroup.MapGet("/{id}", GetMaterial);
materialGroup.MapGet("/stats/data", GetStatisticalData);
materialGroup.MapPost("", CreateMaterial);
materialGroup.MapPatch("/{id}", UpdateMaterial);
materialGroup.MapDelete("/{id}", DeleteMaterial);


app.Run();

static async Task<IResult> GetAllMaterials(AppDbContext db)
{
    Console.WriteLine("///////////");
    return TypedResults.Ok(await db.Materials.Select(x => new MaterialItemDto(x)).ToArrayAsync());
};

static async Task<IResult> GetMaterial(int id, AppDbContext db)
{
    return await db.Materials.FindAsync(id)
        is Material material 
            ? TypedResults.Ok(new MaterialItemDto(material))
            : TypedResults.NotFound();
};

static async Task<IResult> CreateMaterial(MaterialItemDto materialItemDto, AppDbContext db)
{
    var materialItem = new Material
    {
        Title = materialItemDto.Title,
        Type = materialItemDto.Type,
        Tags = materialItemDto.Tags,
        Link = materialItemDto.Link,
        Description = materialItemDto.Description,
        Status = materialItemDto.Status
    };
    
    db.Materials.Add(materialItem);
    await db.SaveChangesAsync();

    materialItemDto = new MaterialItemDto(materialItem);
    
    return TypedResults.Created($"/todo/{materialItem.Id}", materialItemDto);
};

static async Task<IResult> UpdateMaterial(int id, MaterialUpdateDto materialItemDto, AppDbContext db)
{
    var material = await db.Materials.FindAsync(id);

    if (material is null) return TypedResults.NotFound();

    if (materialItemDto.Title is not null) material.Title = materialItemDto.Title;
    if (materialItemDto.Type is not null) material.Type = materialItemDto.Type;
    if (materialItemDto.Tags is not null) material.Tags = materialItemDto.Tags;
    if (materialItemDto.Link is not null) material.Link = materialItemDto.Link;
    if (materialItemDto.Description is not null) material.Description = materialItemDto.Description;
    if (materialItemDto.Status is not null) material.Status = materialItemDto.Status;

    await db.SaveChangesAsync();

    return TypedResults.Ok(material);
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
    return TypedResults.Ok();
};