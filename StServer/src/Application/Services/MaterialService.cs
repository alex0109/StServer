using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Material;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repo;
    private readonly IUserContext _user;

    public MaterialService(IMaterialRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<List<MaterialResponseDto>> GetAllAsync()
    {
        var materials = await _repo.GetAllAsync(_user.UserId);
        return materials.Select(MaterialMapper.ToDto).ToList();
    }

    public async Task<MaterialResponseDto?> GetByIdAsync(Guid materialId)
    {
        var material = await _repo.GetByIdAsync(materialId, _user.UserId);

        if (material is null)
            return null;
        
        return MaterialMapper.ToDto(material);
    }

    public async Task<MaterialResponseDto> CreateAsync(MaterialCreateDto materialCreateDto)
    {
        var material = MaterialMapper.ToEntity(materialCreateDto, _user.UserId);

        var response = await _repo.AddAsync(material);
        
        await _repo.SaveChangesAsync();
        
        return MaterialMapper.ToDto(response);
    }

    public async Task<MaterialResponseDto?> UpdateAsync(Guid materialId, MaterialUpdateDto materialUpdateDto)
    {
        var material = await _repo.GetByIdAsync(
            materialId,
            _user.UserId);

        if (material is null)
            return null;

        MaterialMapper.ApplyUpdate(material, materialUpdateDto);

        await _repo.SaveChangesAsync();

        return MaterialMapper.ToDto(material);
    }

    public async Task SyncMaterialTags(Guid materialId, List<Guid> tagIds)
    {
        var material = await _repo.GetByIdAsync(materialId, _user.UserId);

        if (material is null)
            return;

        material.SyncTags(tagIds);

        await _repo.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid materialId)
    {
        var result = await _repo.DeleteAsync(materialId, _user.UserId);
        
        await _repo.SaveChangesAsync();

        return result;
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

    public async Task<List<AttemptResponseDto>> GetAttempts(Guid materialId)
    {
        var attempts = await _repo.GetAttemptsAsync(materialId, _user.UserId);
        
        return attempts
            .Select(AttemptMapper.ToDto)
            .ToList();
    }
}