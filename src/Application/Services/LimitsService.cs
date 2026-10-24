using Application.DTOs.Limits;
using Application.Interfaces;

namespace Application.Services;

public class LimitsService : ILimitsService
{
    private readonly ILimitsRepository _repo;
    private readonly IUserContext _user;

    public LimitsService(ILimitsRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public Task<LimitsUsageDto> GetUsageAsync() => _repo.GetUsageAsync(_user.UserId);
}
