using Application.DTOs.Dictionary;
using Application.DTOs.Question;
using Application.Constants;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;

namespace Application.Services;

public class DictionaryService : IDictionaryService
{
    private readonly IDictionaryRepository _dictionaryRepository;
    private readonly IContentLimits _limits;
    private readonly IMaterialRepository _materialRepository;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;

    public DictionaryService(
        IDictionaryRepository dictionaryRepository,
        IMaterialRepository materialRepository,
        IUserContext user,
        ILimitLock limitLock,
        IContentLimits? limits = null)
    {
        _dictionaryRepository = dictionaryRepository;
        _materialRepository = materialRepository;
        _user = user;
        _limitLock = limitLock;
        _limits = limits ?? ContentLimits.Free();
    }

    public async Task<List<DictionaryResponseDto>> GetAllByMaterialIdAsync(Guid materialId)
    {
        var dictionaries = await _dictionaryRepository.GetAllByMaterialIdAsync(materialId, _user.UserId);

        return dictionaries.Select(DictionaryMapper.ToDto).ToList();
    }

    public async Task<DictionaryResponseDto?> GetByIdAsync(Guid dictionaryId)
    {
        var dictionary = await _dictionaryRepository.GetByIdAsync(dictionaryId, _user.UserId);

        return dictionary is null ? null : DictionaryMapper.ToDto(dictionary);
    }

    public async Task<DictionaryResponseDto?> CreateAsync(Guid materialId, DictionaryCreateDto dto)
    {
        var userId = _user.UserId;

        var material = await _materialRepository.GetByIdAsync(materialId, userId);
        if (material is null) return null;

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Dictionaries, materialId);

        var max = (await _limits.ForUserAsync(userId)).DictionariesPerMaterial;
        var count = await _dictionaryRepository.CountByMaterialIdAsync(materialId, userId);
        if (count >= max)
        {
            throw new LimitExceededException($"Dictionary limit reached: {max} per material");
        }

        var dictionary = DictionaryMapper.ToEntity(dto, materialId, userId);

        await _dictionaryRepository.AddAsync(dictionary);
        await _dictionaryRepository.SaveChangesAsync();
        await limitLock.CommitAsync();

        return DictionaryMapper.ToDto(dictionary);
    }

    public async Task<DictionaryResponseDto?> UpdateAsync(Guid dictionaryId, DictionaryUpdateDto dto)
    {
        var dictionary = await _dictionaryRepository.GetByIdAsync(dictionaryId, _user.UserId);
        if (dictionary is null) return null;

        DictionaryMapper.ApplyUpdate(dictionary, dto);
        await _dictionaryRepository.SaveChangesAsync();

        return DictionaryMapper.ToDto(dictionary);
    }

    public async Task<bool> DeleteAsync(Guid dictionaryId)
    {
        return await _dictionaryRepository.DeleteAsync(dictionaryId, _user.UserId);
    }

    public async Task<DictionaryEntryResponseDto?> CreateEntryAsync(Guid dictionaryId, DictionaryEntryCreateDto dto)
    {
        var dictionary = await _dictionaryRepository.GetByIdAsync(dictionaryId, _user.UserId);
        if (dictionary is null) return null;

        // The lock also keeps positions unique when words are added in parallel
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.DictionaryEntries, dictionaryId);

        var maxEntries = (await _limits.ForUserAsync(_user.UserId)).EntriesPerDictionary;
        var count = await _dictionaryRepository.CountEntriesAsync(dictionaryId);
        if (count >= maxEntries)
        {
            throw new LimitExceededException($"Word limit reached: {maxEntries} per dictionary");
        }

        // Checked under the lock, so two quick saves of the same word can't both pass
        if (await _dictionaryRepository.FindEntryIdByTermAsync(dictionaryId, dto.Term) is Guid existingId)
        {
            throw new DuplicateEntryException(existingId);
        }

        var position = await _dictionaryRepository.GetMaxEntryPositionAsync(dictionaryId) + 1;
        var entry = DictionaryMapper.ToEntryEntity(dto, dictionaryId, position);

        await _dictionaryRepository.AddEntryAsync(entry);
        await _dictionaryRepository.SaveChangesAsync();
        await limitLock.CommitAsync();

        return DictionaryMapper.ToEntryDto(entry);
    }

    public async Task<DictionaryEntryResponseDto?> GetEntryByIdAsync(Guid entryId)
    {
        var entry = await _dictionaryRepository.GetEntryByIdAsync(entryId, _user.UserId);

        return entry is null ? null : DictionaryMapper.ToEntryDto(entry);
    }

    public async Task<DictionaryEntryResponseDto?> UpdateEntryAsync(Guid entryId, DictionaryEntryUpdateDto dto)
    {
        var entry = await _dictionaryRepository.GetEntryByIdAsync(entryId, _user.UserId);
        if (entry is null) return null;

        DictionaryMapper.ApplyEntryUpdate(entry, dto);
        await _dictionaryRepository.SaveChangesAsync();

        return DictionaryMapper.ToEntryDto(entry);
    }

    public async Task<bool> DeleteEntryAsync(Guid entryId)
    {
        return await _dictionaryRepository.DeleteEntryAsync(entryId, _user.UserId);
    }

    public async Task<List<DictionaryEntryResponseDto>?> ReorderEntriesAsync(Guid dictionaryId, DictionaryEntriesReorderDto dto)
    {
        var entries = await _dictionaryRepository.GetEntriesByDictionaryIdAsync(dictionaryId, _user.UserId);
        if (entries.Count == 0) return null;

        var ids = dto.EntryIds.Distinct().ToList();
        var entriesById = entries.ToDictionary(e => e.Id);

        // Порядок має містити рівно всі записи словника, без чужих і без пропусків
        if (ids.Count != entries.Count || ids.Any(id => !entriesById.ContainsKey(id)))
            return null;

        var now = DateTime.UtcNow;
        for (var i = 0; i < ids.Count; i++)
        {
            var entry = entriesById[ids[i]];
            if (entry.Position == i) continue;

            entry.Position = i;
            entry.UpdatedAt = now;
        }

        await _dictionaryRepository.SaveChangesAsync();

        return entries
            .OrderBy(e => e.Position)
            .Select(DictionaryMapper.ToEntryDto)
            .ToList();
    }

    public async Task<List<QuestionResponseDto>?> GetQuestionsAsync(Guid dictionaryId)
    {
        var dictionary = await _dictionaryRepository.GetByIdAsync(dictionaryId, _user.UserId);
        if (dictionary is null) return null;

        var entries = await _dictionaryRepository.GetEntriesByDictionaryIdAsync(dictionaryId, _user.UserId);

        return entries
            .Where(e => !e.IsArchived && e.Question is { IsActive: true, DeletedAt: null })
            .Select(e => QuestionMapper.ToDto(e.Question!))
            .ToList();
    }
}