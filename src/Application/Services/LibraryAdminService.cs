using Application.DTOs.Library;
using Application.Exceptions;
using Application.Interfaces;
using Application.Library;
using Domain.Entities;
using Domain.Utility.Library;

namespace Application.Services;

// The library owner's side. Content is never edited here either: an approval only moves the
// snapshot the author sent into the library
public class LibraryAdminService : ILibraryAdminService
{
    private const int RequestsShown = 200;

    private readonly ILibraryRepository _library;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public LibraryAdminService(ILibraryRepository library, IUserContext user, TimeProvider time)
    {
        _library = library;
        _user = user;
        _time = time;
    }

    public Task<List<LibraryRequestDetailsDto>> GetRequestsAsync(LibraryRequestStatus? status) =>
        _library.GetRequestsAsync(status, RequestsShown);

    public async Task<LibraryRequestDetailsDto?> GetRequestAsync(Guid requestId)
    {
        var request = await _library.GetRequestAsync(requestId);
        if (request is null) return null;

        var dto = LibraryRules.ToDto(request);

        return new LibraryRequestDetailsDto
        {
            Id = dto.Id,
            Type = dto.Type,
            Status = dto.Status,
            MaterialId = dto.MaterialId,
            LibraryItemId = dto.LibraryItemId,
            Title = dto.Title,
            MaterialType = dto.MaterialType,
            AuthorNickname = dto.AuthorNickname,
            Comment = dto.Comment,
            RejectionReason = dto.RejectionReason,
            ReviewComment = dto.ReviewComment,
            CreatedAt = dto.CreatedAt,
            ReviewedAt = dto.ReviewedAt,
            UserId = request.UserId,
            Content = request.Snapshot is null ? null : LibrarySnapshotJson.FromDocument(request.Snapshot),
        };
    }

    public async Task<LibraryRequestDto?> ApproveAsync(Guid requestId)
    {
        var request = await GetPendingAsync(requestId);
        if (request is null) return null;

        var now = _time.GetUtcNow().UtcDateTime;

        switch (request.Type)
        {
            case LibraryRequestType.Publish:
            {
                var item = NewItem(request.Snapshot!, request.AuthorNickname, request.UserId, request.MaterialId, now);
                await _library.AddItemAsync(item);
                request.LibraryItemId = item.Id;
                break;
            }
            case LibraryRequestType.Update:
            {
                var item = await GetPublishedAsync(request.LibraryItemId);
                var snapshot = LibrarySnapshotJson.FromDocument(request.Snapshot!);

                // A copy of the request's snapshot: the request keeps its own for history
                LibraryRules.Apply(item, snapshot, LibrarySnapshotJson.ToDocument(snapshot));
                item.AuthorNickname = request.AuthorNickname;
                item.SourceMaterialId = request.MaterialId;
                item.UpdatedAt = now;
                item.Version++;
                break;
            }
            case LibraryRequestType.Remove:
            {
                var item = await GetPublishedAsync(request.LibraryItemId);
                item.Status = LibraryItemStatus.Removed;
                item.RemovedAt = now;
                break;
            }
        }

        request.Status = LibraryRequestStatus.Approved;
        request.ReviewedAt = now;
        request.ReviewedBy = _user.UserId;
        await _library.SaveChangesAsync();

        return LibraryRules.ToDto(request);
    }

    public async Task<LibraryRequestDto?> RejectAsync(Guid requestId, LibraryRejectionReason reason, string? comment)
    {
        if (!Enum.IsDefined(reason)) throw new BadRequestException("Unknown rejection reason");

        var request = await GetPendingAsync(requestId);
        if (request is null) return null;

        request.Status = LibraryRequestStatus.Rejected;
        request.RejectionReason = reason;
        request.ReviewComment = LibraryRules.NormalizeComment(comment);
        request.ReviewedAt = _time.GetUtcNow().UtcDateTime;
        request.ReviewedBy = _user.UserId;
        await _library.SaveChangesAsync();

        return LibraryRules.ToDto(request);
    }

    public async Task<LibraryItemSummaryDto?> PublishAsync(LibraryAdminPublishDto dto)
    {
        var userId = _user.UserId;
        var nickname = LibraryRules.NormalizeNickname(dto.AuthorNickname);

        var material = await _library.GetMaterialForSnapshotAsync(dto.MaterialId, userId);
        if (material is null) return null;

        var snapshot = LibrarySnapshotMapper.FromMaterial(material);
        var item = NewItem(LibraryRules.ToCheckedDocument(snapshot), nickname, userId, material.Id, _time.GetUtcNow().UtcDateTime);

        await _library.AddItemAsync(item);
        await _library.SaveChangesAsync();

        return LibraryRules.ToSummaryDto(item, userId, isImported: false);
    }

    public async Task<bool> RemoveItemAsync(Guid itemId)
    {
        var item = await _library.GetItemAsync(itemId);
        if (item is null || item.Status != LibraryItemStatus.Published) return false;

        item.Status = LibraryItemStatus.Removed;
        item.RemovedAt = _time.GetUtcNow().UtcDateTime;
        await _library.SaveChangesAsync();

        return true;
    }

    private async Task<LibraryRequest?> GetPendingAsync(Guid requestId)
    {
        var request = await _library.GetRequestAsync(requestId);
        if (request is null) return null;

        if (request.Status != LibraryRequestStatus.Pending)
            throw new ConflictException("This request was already handled");

        return request;
    }

    private async Task<LibraryItem> GetPublishedAsync(Guid? itemId)
    {
        var item = itemId is Guid id ? await _library.GetItemAsync(id) : null;

        return item is { Status: LibraryItemStatus.Published }
            ? item
            : throw new ConflictException("The library material is no longer published");
    }

    private static LibraryItem NewItem(
        System.Text.Json.JsonDocument snapshotDocument, string nickname, Guid authorId, Guid? materialId, DateTime now)
    {
        var snapshot = LibrarySnapshotJson.FromDocument(snapshotDocument);

        var item = new LibraryItem
        {
            Id = Guid.NewGuid(),
            Title = snapshot.Title,
            Type = snapshot.Type,
            AuthorNickname = nickname,
            AuthorUserId = authorId,
            SourceMaterialId = materialId,
            // Own copy, independent from the request row
            Snapshot = LibrarySnapshotJson.ToDocument(snapshot),
            Status = LibraryItemStatus.Published,
            PublishedAt = now,
            UpdatedAt = now,
        };

        LibraryRules.Apply(item, snapshot, item.Snapshot);
        return item;
    }
}
