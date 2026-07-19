using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Material;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repo;
    private readonly IAssessmentRepository _assessmentRepo;
    private readonly IUserContext _user;

    public MaterialService(IMaterialRepository repo, IAssessmentRepository assessmentRepo, IUserContext user)
    {
        _repo = repo;
        _assessmentRepo = assessmentRepo;
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
        await _repo.AddAsync(material);
        
        var assessment = AssessmentMapper.ToEntity(material.Id, _user.UserId);
        await _assessmentRepo.AddAssessmentAsync(assessment);
        
        await _repo.SaveChangesAsync();
        
        return MaterialMapper.ToDto(material);
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
    
    public async Task<MaterialResponseDto?> AddTagToMaterialAsync(Guid materialId, Guid tagId)
    {
        var material = await _repo.GetByIdAsync(materialId, _user.UserId);

        if (material is null)
            return null;

        material.AddTag(tagId);
        material.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        var updatedMaterial = await _repo.GetByIdAsync(
            materialId,
            _user.UserId
        );
        
        if (updatedMaterial is null)
            return null;


        return MaterialMapper.ToDto(updatedMaterial);
    }
    
    public async Task<MaterialResponseDto?> DeleteTagFromMaterialAsync(Guid materialId, Guid tagId)
    {
        var material = await _repo.GetByIdAsync(materialId, _user.UserId);

        if (material is null)
            return null;

        material.RemoveTag(tagId);
        material.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        var updatedMaterial = await _repo.GetByIdAsync(
            materialId,
            _user.UserId
        );
        
        if (updatedMaterial is null)
            return null;


        return MaterialMapper.ToDto(updatedMaterial);
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