using Application.Ai;
using Application.Ai.Prompts;
using Application.Constants;
using Application.DTOs.Ai;
using Application.DTOs.Dictionary;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;

namespace Application.Services.Ai;

public class DictionaryGenerationService : IDictionaryGenerationService
{
    private readonly IDictionaryRepository _dictionaryRepository;
    private readonly IContentLimits _limits;
    private readonly IMaterialRepository _materialRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;

    public DictionaryGenerationService(
        IDictionaryRepository dictionaryRepository,
        IMaterialRepository materialRepository,
        INoteRepository noteRepository,
        IAiService ai,
        IUserContext user,
        ILimitLock limitLock,
        IContentLimits? limits = null)
    {
        _dictionaryRepository = dictionaryRepository;
        _materialRepository = materialRepository;
        _noteRepository = noteRepository;
        _ai = ai;
        _user = user;
        _limitLock = limitLock;
        _limits = limits ?? ContentLimits.Free();
    }


    public async Task<List<DictionaryEntryResponseDto>?> GenerateAsync(
        Guid dictionaryId,
        DictionaryEntriesGenerateDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Count is < 1 or > AiLimits.MaxGeneratedEntries)
            throw new BadRequestException($"Count must be from 1 to {AiLimits.MaxGeneratedEntries}");

        var language = dto.Language?.Trim() ?? string.Empty;
        if (!AiLanguageName.IsValid(language))
            throw new BadRequestException("Language must be a language name, e.g. \"English\"");

        var userId = _user.UserId;

        var dictionary = await _dictionaryRepository.GetByIdAsync(dictionaryId, userId);
        if (dictionary is null) return null;

        // Sources are limited to the dictionary's own material: its content or one of its notes
        string content;
        if (dto.NoteId is { } noteId)
        {
            var note = await _noteRepository.GetByIdAsync(noteId, dictionary.MaterialId, userId);
            if (note is null) return null;

            content = AiSourceText.FromNote(note);
        }
        else
        {
            var material = await _materialRepository.GetByIdAsync(dictionary.MaterialId, userId);
            if (material is null) return null;

            content = AiSourceText.FromMaterial(material);
        }

        var max = (await _limits.ForUserAsync(userId)).EntriesPerDictionary;
        var free = (max ?? int.MaxValue) - await _dictionaryRepository.CountEntriesAsync(dictionaryId);
        if (free <= 0)
            throw new LimitExceededException($"Word limit reached: {max} per dictionary");

        var existingTerms = dictionary.Entries.Select(x => x.Term).ToList();

        var prompt = new DictionaryGenerationPrompt(content, Math.Min(dto.Count, free), language, existingTerms);
        var generated = (await _ai.RunAsync(prompt, cancellationToken)).Value;

        if (generated.Count == 0)
            throw new AiOutputException("AI found no new words in this text");

        // The lock also keeps positions unique when words are added in parallel
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.DictionaryEntries, dictionaryId);

        free = (max ?? int.MaxValue) - await _dictionaryRepository.CountEntriesAsync(dictionaryId);
        if (free <= 0)
            throw new LimitExceededException($"Word limit reached: {max} per dictionary");

        var position = await _dictionaryRepository.GetMaxEntryPositionAsync(dictionaryId);
        var entries = new List<DictionaryEntry>();

        foreach (var item in generated.Take(free))
        {
            var entry = DictionaryMapper.ToEntryEntity(new DictionaryEntryCreateDto
            {
                Term = item.Term,
                Translation = item.Translation,
                Transcription = item.Transcription,
                PartOfSpeech = item.PartOfSpeech,
                Example = item.Example,
                ExampleTranslation = item.ExampleTranslation,
            }, dictionaryId, ++position);

            await _dictionaryRepository.AddEntryAsync(entry);
            entries.Add(entry);
        }

        await _dictionaryRepository.SaveChangesAsync();
        await limitLock.CommitAsync();

        return entries.Select(DictionaryMapper.ToEntryDto).ToList();
    }
}
