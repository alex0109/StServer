using Application.DTOs.Note;
using Application.Interfaces;

namespace Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        var noteGroup = app.MapGroup("api/materials/{materialId}/notes")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        noteGroup.MapGet("/", GetAllNotes);
        noteGroup.MapGet("/{noteId}", GetOneNote);
        noteGroup.MapPost("/", CreateNote);
        noteGroup.MapPatch("/{noteId}", UpdateNote);
        noteGroup.MapDelete("/{noteId}", DeleteNote);
        
        static async Task<IResult> GetAllNotes(Guid materialId, INoteService service)
        {
            var result = await service.GetAllAsync(materialId);
            
            if (result is null) return TypedResults.NotFound();
            
            return TypedResults.Ok(result);
        };
        
        static async Task<IResult> GetOneNote(Guid noteId, Guid materialId, INoteService service)
        {
            var result = await service.GetByIdAsync(noteId, materialId);
            
            if (result is null) return TypedResults.NotFound();
            
            return TypedResults.Ok(result);
        };
        
        static async Task<IResult> CreateNote(Guid materialId, NoteCreateDto noteCreateDto, INoteService service)
        {
            var result = await service.CreateAsync(materialId, noteCreateDto);
            
            return TypedResults.Created($"/api/materials/{materialId}/notes/{result.Id}", result);
        };
        
        static async Task<IResult> UpdateNote(Guid noteId, Guid materialId, NoteUpdateDto dto, INoteService service)
        {
            var result = await service.UpdateAsync(noteId, materialId, dto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        };
        
        static async Task<IResult> DeleteNote(Guid noteId, Guid materialId, INoteService service)
        {
            bool result = await service.DeleteAsync(noteId, materialId);
            
            if (result) return TypedResults.NoContent();
            
            return TypedResults.NotFound();
        };
    }
}