using Application.DTOs.Limits;
using Application.Interfaces;

namespace Application.Services;

public class LimitsService : ILimitsService
{
    private readonly ILimitsRepository _repo;
    private readonly IUserContext _user;
    private readonly IContentLimits _limits;

    public LimitsService(ILimitsRepository repo, IUserContext user, IContentLimits? limits = null)
    {
        _repo = repo;
        _user = user;
        _limits = limits ?? ContentLimits.Free();
    }

    public async Task<LimitsUsageDto> GetUsageAsync()
    {
        var usage = await _repo.GetUsageAsync(_user.UserId);

        // Counted like the check: library copies take material places only when configured so
        return _limits.CountLibraryImports
            ? usage
            : new LimitsUsageDto
            {
                Materials = usage.Materials - usage.LibraryCopies,
                LibraryCopies = usage.LibraryCopies,
                Tags = usage.Tags,
                MaterialsUsage = usage.MaterialsUsage,
            };
    }
}
