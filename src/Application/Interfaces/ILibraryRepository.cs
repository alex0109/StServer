using Application.DTOs.Library;
using Domain.Entities;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Application.Interfaces;

public interface ILibraryRepository
{
    // The user's material with notes, questions (with options) and dictionaries (with words), read only
    Task<Material?> GetMaterialForSnapshotAsync(Guid materialId, Guid userId);

    Task<(List<LibraryItemSummaryDto> Items, int Total)> ListPublishedAsync(
        Guid? userId, string? search, MaterialType? type, int skip, int take);

    Task<LibraryItem?> GetItemAsync(Guid itemId);

    Task<LibraryItem?> GetPublishedItemAsync(Guid authorUserId, string title);

    Task<bool> HasImportedAsync(Guid itemId, Guid userId);

    // The material the user already learns this item from: their own original if they wrote it, else their latest copy
    Task<Guid?> FindCopyAsync(Guid itemId, Guid userId, Guid? sourceMaterialId);

    Task AddItemAsync(LibraryItem item);

    Task AddRequestAsync(LibraryRequest request);

    Task<LibraryRequest?> GetRequestAsync(Guid requestId);

    Task<List<LibraryRequestDto>> GetUserRequestsAsync(Guid userId, int take);

    Task<List<LibraryRequestDetailsDto>> GetRequestsAsync(LibraryRequestStatus? status, int take);

    Task<int> CountPendingAsync(Guid userId);

    Task<bool> HasPendingForMaterialAsync(Guid materialId);

    Task<LibraryRequestType?> GetPendingTypeForItemAsync(Guid itemId);

    Task SaveChangesAsync();
}
