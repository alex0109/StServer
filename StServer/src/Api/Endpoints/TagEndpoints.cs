using StServer.Application.DTOs.Tag;
using StServer.Application.Interfaces;

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

        static async Task<IResult> GetTags(ITagService service)
        {
            var result = await service.GetAllTagsAsync();

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetTagById(Guid tagId, ITagService service)
        {
            var result = await service.GetByIdAsync(tagId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetMaterialsByTag(Guid tagId, ITagService service)
        {
            var result = await service.GetMaterialsByTagAsync(tagId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> CreateTag(TagCreateDto tagDto, ITagService service)
        {
            var result = await service.CreateAsync(tagDto);

            return TypedResults.Created($"/api/tags/{result.Id}", result);
        }

        static async Task<IResult> UpdateTag(Guid tagId, TagUpdateDto tagDto, ITagService service)
        {
            var result = await service.UpdateTagAsync(tagId, tagDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> DeleteTag(Guid tagId, ITagService service)
        {
            var result = await service.DeleteTagAsync(tagId);

            if (result is false)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }
    }
}