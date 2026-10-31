using Application.DTOs.Ai;
using Application.DTOs.Dictionary;
using Application.Interfaces;

namespace Api.Endpoints;

public static class DictionaryEndpoints
{
    public static void MapDictionaryEndpoints(this WebApplication app)
    {
        var dictionaryGroup = app.MapGroup("api/dictionaries")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        dictionaryGroup.MapGet("/materials/{materialId:guid}", GetAllDictionaries);
        dictionaryGroup.MapPost("/materials/{materialId:guid}", CreateDictionary);
        dictionaryGroup.MapGet("/{dictionaryId:guid}", GetDictionary);
        dictionaryGroup.MapPut("/{dictionaryId:guid}", UpdateDictionary);
        dictionaryGroup.MapDelete("/{dictionaryId:guid}", DeleteDictionary);
        dictionaryGroup.MapPost("/{dictionaryId:guid}/entries", CreateDictionaryEntry);
        // AI suggestion for a word selected while reading; nothing is saved
        dictionaryGroup.MapPost("/{dictionaryId:guid}/entries/lookup", LookupEntry);
        dictionaryGroup.MapGet("/entries/{entryId:guid}", GetDictionaryEntry);
        dictionaryGroup.MapPut("/entries/{entryId:guid}", UpdateDictionaryEntry);
        dictionaryGroup.MapDelete("/entries/{entryId:guid}", DeleteDictionaryEntry);
        dictionaryGroup.MapPatch("/{dictionaryId:guid}/entries/reorder", EntriesReorder);
        dictionaryGroup.MapGet("/{dictionaryId:guid}/questions", GetDictionaryQuestions);

        static async Task<IResult> LookupEntry(
            Guid dictionaryId,
            WordLookupRequestDto dto,
            IWordLookupService service,
            CancellationToken cancellationToken)
        {
            var result = await service.LookupAsync(dictionaryId, dto, cancellationToken);

            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }

        static async Task<IResult> GetAllDictionaries(Guid materialId, IDictionaryService service)
        {
            var result = await service.GetAllByMaterialIdAsync(materialId);

            return TypedResults.Ok(result);
        }

        static async Task<IResult> CreateDictionary(Guid materialId, DictionaryCreateDto dictionaryCreateDto, IDictionaryService service)
        {
            var result = await service.CreateAsync(materialId, dictionaryCreateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Created($"/api/dictionaries/{result.Id}", result);
        }

        static async Task<IResult> GetDictionary(Guid dictionaryId, IDictionaryService service)
        {
            var result = await service.GetByIdAsync(dictionaryId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> UpdateDictionary(Guid dictionaryId, DictionaryUpdateDto dictionaryUpdateDto, IDictionaryService service)
        {
            var result = await service.UpdateAsync(dictionaryId, dictionaryUpdateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> DeleteDictionary(Guid dictionaryId, IDictionaryService service)
        {
            bool result = await service.DeleteAsync(dictionaryId);

            if (result) return TypedResults.NoContent();

            return TypedResults.NotFound();
        }

        static async Task<IResult> CreateDictionaryEntry(Guid dictionaryId, DictionaryEntryCreateDto entryCreateDto, IDictionaryService service)
        {
            var result = await service.CreateEntryAsync(dictionaryId, entryCreateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Created($"/api/dictionaries/entries/{result.Id}", result);
        }

        static async Task<IResult> GetDictionaryEntry(Guid entryId, IDictionaryService service)
        {
            var result = await service.GetEntryByIdAsync(entryId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> UpdateDictionaryEntry(Guid entryId, DictionaryEntryUpdateDto entryUpdateDto, IDictionaryService service)
        {
            var result = await service.UpdateEntryAsync(entryId, entryUpdateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> DeleteDictionaryEntry(Guid entryId, IDictionaryService service)
        {
            bool result = await service.DeleteEntryAsync(entryId);

            if (result) return TypedResults.NoContent();

            return TypedResults.NotFound();
        }

        static async Task<IResult> EntriesReorder(Guid dictionaryId, DictionaryEntriesReorderDto reorderDto, IDictionaryService service)
        {
            var result = await service.ReorderEntriesAsync(dictionaryId, reorderDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetDictionaryQuestions(Guid dictionaryId, IDictionaryService service)
        {
            var result = await service.GetQuestionsAsync(dictionaryId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }
    }
}