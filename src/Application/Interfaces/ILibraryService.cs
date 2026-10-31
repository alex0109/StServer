using Application.DTOs.Library;
using Application.DTOs.Material;
using Domain.Utility.Material;

namespace Application.Interfaces;

public interface ILibraryService
{
    Task<LibraryPageDto> ListAsync(string? search, MaterialType? type, int page, int pageSize);

    Task<LibraryItemDto?> GetAsync(Guid itemId);

    // A copy among the user's materials, marked as imported from the library
    Task<MaterialResponseDto?> ImportAsync(Guid itemId);

    Task<List<LibraryRequestDto>> GetMyRequestsAsync();

    Task<LibraryRequestDto> CreateRequestAsync(LibraryRequestCreateDto dto);

    Task<bool> CancelRequestAsync(Guid requestId);
}

// Only for the library owner (admin): nobody else publishes or takes materials down
public interface ILibraryAdminService
{
    Task<List<LibraryRequestDetailsDto>> GetRequestsAsync(Domain.Utility.Library.LibraryRequestStatus? status);

    Task<LibraryRequestDetailsDto?> GetRequestAsync(Guid requestId);

    Task<LibraryRequestDto?> ApproveAsync(Guid requestId);

    Task<LibraryRequestDto?> RejectAsync(Guid requestId, Domain.Utility.Library.LibraryRejectionReason reason, string? comment);

    // The owner's own material goes to the library at once
    Task<LibraryItemSummaryDto?> PublishAsync(LibraryAdminPublishDto dto);

    Task<bool> RemoveItemAsync(Guid itemId);
}
