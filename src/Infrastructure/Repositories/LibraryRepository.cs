using Application.DTOs.Library;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Library;
using Domain.Utility.Material;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class LibraryRepository : ILibraryRepository
{
    private readonly AppDbContext _db;

    public LibraryRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Material?> GetMaterialForSnapshotAsync(Guid materialId, Guid userId) =>
        _db.Materials
            .AsNoTracking()
            .Include(m => m.Notes)
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .Include(m => m.Dictionaries)
                .ThenInclude(d => d.Entries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == materialId && m.UserId == userId);

    // The list never loads snapshots: only the columns cards show
    public async Task<(List<LibraryItemSummaryDto> Items, int Total)> ListPublishedAsync(
        Guid userId, string? search, MaterialType? type, int skip, int take)
    {
        var query = _db.LibraryItems.Where(x => x.Status == LibraryItemStatus.Published);

        if (search is not null)
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.AuthorNickname, pattern, "\\"));
        }

        if (type is not null)
            query = query.Where(x => x.Type == type);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new LibraryItemSummaryDto
            {
                Id = x.Id,
                Title = x.Title,
                Type = x.Type,
                AuthorNickname = x.AuthorNickname,
                NotesCount = x.NotesCount,
                QuestionsCount = x.QuestionsCount,
                DictionariesCount = x.DictionariesCount,
                WordsCount = x.WordsCount,
                ImportsCount = x.ImportsCount,
                PublishedAt = x.PublishedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = x.AuthorUserId == userId,
                IsImported = _db.Materials.Any(m => m.UserId == userId && m.ImportedFromLibraryItemId == x.Id),
            })
            .ToListAsync();

        return (items, total);
    }

    public Task<LibraryItem?> GetItemAsync(Guid itemId) =>
        _db.LibraryItems.FirstOrDefaultAsync(x => x.Id == itemId);

    public Task<bool> HasImportedAsync(Guid itemId, Guid userId) =>
        _db.Materials.AnyAsync(m => m.UserId == userId && m.ImportedFromLibraryItemId == itemId);

    public async Task AddItemAsync(LibraryItem item)
    {
        await _db.LibraryItems.AddAsync(item);
    }

    public async Task AddRequestAsync(LibraryRequest request)
    {
        await _db.LibraryRequests.AddAsync(request);
    }

    public Task<LibraryRequest?> GetRequestAsync(Guid requestId) =>
        _db.LibraryRequests.FirstOrDefaultAsync(x => x.Id == requestId);

    public Task<List<LibraryRequestDto>> GetUserRequestsAsync(Guid userId, int take) =>
        _db.LibraryRequests
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new LibraryRequestDto
            {
                Id = x.Id,
                Type = x.Type,
                Status = x.Status,
                MaterialId = x.MaterialId,
                LibraryItemId = x.LibraryItemId,
                Title = x.Title,
                MaterialType = x.MaterialType,
                AuthorNickname = x.AuthorNickname,
                Comment = x.Comment,
                RejectionReason = x.RejectionReason,
                ReviewComment = x.ReviewComment,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt,
            })
            .ToListAsync();

    // Oldest first: the queue is reviewed in order. Snapshots are loaded per request, not here
    public Task<List<LibraryRequestDetailsDto>> GetRequestsAsync(LibraryRequestStatus? status, int take) =>
        _db.LibraryRequests
            .Where(x => status == null || x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .Select(x => new LibraryRequestDetailsDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Type = x.Type,
                Status = x.Status,
                MaterialId = x.MaterialId,
                LibraryItemId = x.LibraryItemId,
                Title = x.Title,
                MaterialType = x.MaterialType,
                AuthorNickname = x.AuthorNickname,
                Comment = x.Comment,
                RejectionReason = x.RejectionReason,
                ReviewComment = x.ReviewComment,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt,
            })
            .ToListAsync();

    public Task<int> CountPendingAsync(Guid userId) =>
        _db.LibraryRequests.CountAsync(x => x.UserId == userId && x.Status == LibraryRequestStatus.Pending);

    public Task<bool> HasPendingForMaterialAsync(Guid materialId) =>
        _db.LibraryRequests.AnyAsync(x => x.MaterialId == materialId
                                          && x.Status == LibraryRequestStatus.Pending
                                          && x.Type != LibraryRequestType.Remove);

    public Task<LibraryRequestType?> GetPendingTypeForItemAsync(Guid itemId) =>
        _db.LibraryRequests
            .Where(x => x.LibraryItemId == itemId && x.Status == LibraryRequestStatus.Pending)
            .Select(x => (LibraryRequestType?)x.Type)
            .FirstOrDefaultAsync();

    public Task SaveChangesAsync() => _db.SaveChangesAsync();

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
