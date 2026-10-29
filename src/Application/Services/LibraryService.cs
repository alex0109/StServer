using Application.Constants;
using Application.DTOs.Library;
using Application.DTOs.Material;
using Application.Exceptions;
using Application.Interfaces;
using Application.Library;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Application.Services;

public class LibraryService : ILibraryService
{
    private const int MaxPageSize = 48;
    private const int RequestsShown = 50;

    private readonly ILibraryRepository _library;
    private readonly IMaterialRepository _materials;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;
    private readonly TimeProvider _time;

    public LibraryService(
        ILibraryRepository library,
        IMaterialRepository materials,
        IUserContext user,
        ILimitLock limitLock,
        TimeProvider time)
    {
        _library = library;
        _materials = materials;
        _user = user;
        _limitLock = limitLock;
        _time = time;
    }

    public async Task<LibraryPageDto> ListAsync(string? search, MaterialType? type, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()[..Math.Min(search.Trim().Length, 100)];

        var (items, total) = await _library.ListPublishedAsync(
            _user.UserId, search, type, (page - 1) * pageSize, pageSize);

        return new LibraryPageDto { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<LibraryItemDto?> GetAsync(Guid itemId)
    {
        var item = await _library.GetItemAsync(itemId);
        if (item is null || item.Status != LibraryItemStatus.Published) return null;

        var userId = _user.UserId;
        var summary = LibraryRules.ToSummaryDto(item, userId, await _library.HasImportedAsync(item.Id, userId));

        return new LibraryItemDto
        {
            Id = summary.Id,
            Title = summary.Title,
            Type = summary.Type,
            AuthorNickname = summary.AuthorNickname,
            NotesCount = summary.NotesCount,
            QuestionsCount = summary.QuestionsCount,
            DictionariesCount = summary.DictionariesCount,
            WordsCount = summary.WordsCount,
            ImportsCount = summary.ImportsCount,
            PublishedAt = summary.PublishedAt,
            UpdatedAt = summary.UpdatedAt,
            IsMine = summary.IsMine,
            IsImported = summary.IsImported,
            Content = LibrarySnapshotJson.FromDocument(item.Snapshot),
            // Only the author sees that their request is waiting
            PendingRequest = summary.IsMine ? await _library.GetPendingTypeForItemAsync(item.Id) : null,
        };
    }

    public async Task<MaterialResponseDto?> ImportAsync(Guid itemId)
    {
        var userId = _user.UserId;

        var item = await _library.GetItemAsync(itemId);
        if (item is null || item.Status != LibraryItemStatus.Published) return null;

        // Same lock and limit as creating a material: an import takes a material place
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Materials, userId);

        if (await _materials.CountByUserIdAsync(userId) >= UserLimits.MaxMaterials)
            throw new LimitExceededException($"Material limit reached: {UserLimits.MaxMaterials}");

        var copy = LibrarySnapshotMapper.ToMaterial(
            LibrarySnapshotJson.FromDocument(item.Snapshot), item.Id, userId, _time.GetUtcNow().UtcDateTime);
        item.ImportsCount++;

        await _materials.AddAsync(copy);
        await _materials.SaveChangesAsync();
        await limitLock.CommitAsync();

        return MaterialMapper.ToDto(copy);
    }

    public Task<List<LibraryRequestDto>> GetMyRequestsAsync() =>
        _library.GetUserRequestsAsync(_user.UserId, RequestsShown);

    public async Task<LibraryRequestDto> CreateRequestAsync(LibraryRequestCreateDto dto)
    {
        var userId = _user.UserId;
        var comment = LibraryRules.NormalizeComment(dto.Comment);

        // One author at a time: counting and adding can't interleave
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.LibraryRequests, userId);

        if (await _library.CountPendingAsync(userId) >= LibraryRules.MaxPendingRequests)
            throw new LimitExceededException($"At most {LibraryRules.MaxPendingRequests} library requests can wait for review");

        var request = dto.Type switch
        {
            LibraryRequestType.Publish => await PublishRequestAsync(dto, userId),
            LibraryRequestType.Update => await UpdateRequestAsync(dto, userId),
            LibraryRequestType.Remove => await RemoveRequestAsync(dto, userId),
            _ => throw new BadRequestException("Unknown request type"),
        };

        request.Comment = comment;

        await _library.AddRequestAsync(request);
        await _library.SaveChangesAsync();
        await limitLock.CommitAsync();

        return LibraryRules.ToDto(request);
    }

    public async Task<bool> CancelRequestAsync(Guid requestId)
    {
        var request = await _library.GetRequestAsync(requestId);
        if (request is null || request.UserId != _user.UserId || request.Status != LibraryRequestStatus.Pending)
            return false;

        request.Status = LibraryRequestStatus.Cancelled;
        request.ReviewedAt = _time.GetUtcNow().UtcDateTime;
        await _library.SaveChangesAsync();

        return true;
    }

    private async Task<LibraryRequest> PublishRequestAsync(LibraryRequestCreateDto dto, Guid userId)
    {
        var nickname = LibraryRules.NormalizeNickname(dto.AuthorNickname);
        var (material, snapshot, document) = await SnapshotAsync(dto.MaterialId, userId);

        return NewRequest(userId, LibraryRequestType.Publish, nickname, snapshot.Title, snapshot.Type, material.Id, null, document);
    }

    private async Task<LibraryRequest> UpdateRequestAsync(LibraryRequestCreateDto dto, Guid userId)
    {
        var nickname = LibraryRules.NormalizeNickname(dto.AuthorNickname);
        var item = await GetOwnPublishedItemAsync(dto.LibraryItemId, userId);
        var (material, snapshot, document) = await SnapshotAsync(dto.MaterialId, userId);

        return NewRequest(userId, LibraryRequestType.Update, nickname, snapshot.Title, snapshot.Type, material.Id, item.Id, document);
    }

    private async Task<LibraryRequest> RemoveRequestAsync(LibraryRequestCreateDto dto, Guid userId)
    {
        var item = await GetOwnPublishedItemAsync(dto.LibraryItemId, userId);

        return NewRequest(userId, LibraryRequestType.Remove, item.AuthorNickname, item.Title, item.Type, item.SourceMaterialId, item.Id, null);
    }

    private async Task<(Material Material, LibrarySnapshot Snapshot, System.Text.Json.JsonDocument Document)> SnapshotAsync(
        Guid? materialId, Guid userId)
    {
        if (materialId is not Guid id) throw new BadRequestException("Choose a material");

        var material = await _library.GetMaterialForSnapshotAsync(id, userId)
                       ?? throw new KeyNotFoundException("Material not found");

        // A library copy goes back only through its own library material
        if (material.ImportedFromLibraryItemId is not null)
            throw new BadRequestException("Materials imported from the library can't be sent to it");

        if (await _library.HasPendingForMaterialAsync(material.Id))
            throw new ConflictException("This material already waits for review");

        var snapshot = LibrarySnapshotMapper.FromMaterial(material);

        return (material, snapshot, LibraryRules.ToCheckedDocument(snapshot));
    }

    private async Task<LibraryItem> GetOwnPublishedItemAsync(Guid? itemId, Guid userId)
    {
        if (itemId is not Guid id) throw new BadRequestException("Choose a library material");

        var item = await _library.GetItemAsync(id);
        if (item is null || item.Status != LibraryItemStatus.Published || item.AuthorUserId != userId)
            throw new KeyNotFoundException("Library material not found");

        if (await _library.GetPendingTypeForItemAsync(item.Id) is not null)
            throw new ConflictException("A request about this library material already waits for review");

        return item;
    }

    private LibraryRequest NewRequest(
        Guid userId,
        LibraryRequestType type,
        string nickname,
        string title,
        MaterialType materialType,
        Guid? materialId,
        Guid? itemId,
        System.Text.Json.JsonDocument? snapshot) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        Status = LibraryRequestStatus.Pending,
        MaterialId = materialId,
        LibraryItemId = itemId,
        Title = title,
        MaterialType = materialType,
        AuthorNickname = nickname,
        Snapshot = snapshot,
        CreatedAt = _time.GetUtcNow().UtcDateTime,
    };
}
