namespace StServer.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this WebApplication app)
    {
        var tagGroup = app.MapGroup("api/tags").RequireAuthorization();

        tagGroup.MapGet("/", GetTags);
        tagGroup.MapGet("/{tagId}", GetTagById);
        tagGroup.MapGet("/{tagId}/materials", GetMaterialsByTag);
        tagGroup.MapPost("/", CreateTag);
        tagGroup.MapPatch("/{tagId}", UpdateTag);
        tagGroup.MapDelete("/{tagId}", DeleteTag);

        static async Task<IResult> GetTags(){}
        static async Task<IResult> GetTagById(){}
        static async Task<IResult> GetMaterialsByTag(){}
        static async Task<IResult> CreateTag(){}
        static async Task<IResult> UpdateTag(){}
        static async Task<IResult> DeleteTag(){}
}