using StServer.Application.DTOs.Material;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repo;
    private readonly IUserContext _user;
    private readonly IMaterialTagService _tags;

    public MaterialService(IMaterialRepository repo, IUserContext user, IMaterialTagService materialTagService)
    {
        _repo = repo;
        _user = user;
        _tags = materialTagService;
    }

    public async Task<List<MaterialResponseDto>> GetAllAsync()
    {
        var materials = await _repo.GetAllAsync(_user.UserId);
        return materials.Select(MaterialMapper.ToDto).ToList();
    }

    public async Task<MaterialResponseDto?> GetByIdAsync(Guid id)
    {
        var material = await _repo.GetByIdAsync(_user.UserId, id);

        return MaterialMapper.ToDto(material);
    }

    public async Task<MaterialResponseDto> CreateAsync(MaterialCreateDto materialCreateDto)
    {
        var material = MaterialMapper.ToEntity(materialCreateDto);

        var response = await _repo.AddAsync(material);
        
        await _repo.SaveChangesAsync();
        
        return MaterialMapper.ToDto(response);
    }

    public async Task<MaterialResponseDto?> UpdateAsync(Guid id, MaterialUpdateDto materialUpdateDto)
    {
        var material = await _repo.GetByIdAsync(
            id,
            _user.UserId);

        if (material is null)
            return null;

        MaterialMapper.ApplyUpdate(material, materialUpdateDto);

        if (materialUpdateDto.TagIds is not null)
        {
            _tags.SyncTags(
                material,
                materialUpdateDto.TagIds);
        }

        await _repo.SaveChangesAsync();

        return MaterialMapper.ToDto(material);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _repo.DeleteAsync(id, _user.UserId);
    }

    public async Task<MaterialStatisticsDto> GetStatisticsAsync()
    {
        var materials = await _repo.GetAllAsync(_user.UserId);

        var statuses = materials.GroupBy(m => m.Status)
            .ToDictionary(g => g.Key, g => g.Count());
            
        var types = materials.GroupBy(m => m.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var response = new { count = materials.Count, statuses, types };

        return new MaterialStatisticsDto
        {
            Count = materials.Count,
            Statuses = statuses,
            Types = types
        };
    }
}