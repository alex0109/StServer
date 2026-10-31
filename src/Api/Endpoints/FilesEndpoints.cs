namespace Api.Endpoints;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var fileGroup = app.MapGroup("api/materials/{materialId:guid}/files")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        fileGroup.MapGet("/", GetAllFiles);
        fileGroup.MapGet("/{fileId:guid}", GetOneFile);
        fileGroup.MapPost("/", CreateFile);
        fileGroup.MapDelete("/{fileId:guid}", DeleteFile);
        
        static async Task<IResult> GetAllFiles()
        {
            return TypedResults.Ok();
        };
        
        static async Task<IResult> GetOneFile()
        {
            return TypedResults.Ok();
        };
        
        static async Task<IResult> CreateFile()
        {
            return TypedResults.Ok();
        };
        
        static async Task<IResult> DeleteFile()
        {
            return TypedResults.Ok();
        };
    }
}