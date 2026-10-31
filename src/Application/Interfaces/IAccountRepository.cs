using Application.Account;
using Application.DTOs.Account;

namespace Application.Interfaces;

public interface IAccountRepository
{
    Task<AccountExportDto> GetExportAsync(Guid userId, string? email, DateTime exportedAt);

    // One transaction: everything of the user goes or nothing does
    Task<AccountDeletionSummary> DeleteAsync(Guid userId, DateTime deletedAt);
}
