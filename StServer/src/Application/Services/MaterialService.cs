using StServer.Application.DTOs.Material;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;
using StServer.Infrastructure.Repositories;

namespace StServer.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repo;
    private readonly IUserContext _user;

    public MaterialService(MaterialRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<List<MaterialResponseDto>> GetAllAsync()
    {
        var materials = await _repo.GetAllAsync();
        return materials.Select(x => MaterialMapper.ToDto(x)).ToList();
    }

    public async Task<MaterialResponseDto?> GetByIdAsync(Guid id)
    {
        
    }

    public async Task<MaterialResponseDto> CreateAsync(MaterialCreateDto materialCreateDto)
    {
        
    }

    public async Task<MaterialResponseDto> UpdateAsync(Guid id, MaterialUpdateDto materialUpdateDto)
    {
        
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        
    }

    public async Task<object> GetStatisticsAsync()
    {
        
    }
}