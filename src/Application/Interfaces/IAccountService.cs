using Application.Account;
using Application.DTOs.Account;

namespace Application.Interfaces;

public interface IAccountService
{
    Task<AccountExportFile> ExportAsync();

    Task DeleteAsync(AccountDeleteDto dto);
}
