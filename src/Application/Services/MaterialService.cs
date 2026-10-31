using Application.Constants;
using Application.DTOs.Material;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Application.Observability;

namespace Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repo;
    private readonly IContentLimits _limits;
    private readonly IAssessmentRepository _assessmentRepo;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;

    public MaterialService(
        IMaterialRepository repo,
        IAssessmentRepository assessmentRepo,
        IUserContext user,
        ILimitLock limitLock,
        IContentLimits? limits = null)
    {
        _repo = repo;
        _assessmentRepo = assessmentRepo;
        _user = user;
        _limitLock = limitLock;
        _limits = limits ?? ContentLimits.Free(repo);
    }

    public async Task<List<MaterialResponseDto>> GetAllAsync()
    {
        var materials = await _repo.GetAllAsync(_user.UserId);
        
        return materials.Select(MaterialMapper.ToDto).ToList();
    }

    public Task<List<MaterialResponseDto>> GetListAsync()
    {
        return _repo.GetListAsync(_user.UserId);
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
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Materials, _user.UserId);

        await _limits.EnsureMaterialPlaceAsync(_user.UserId, libraryCopy: false);
        
        var material = MaterialMapper.ToEntity(materialCreateDto, _user.UserId);
        await _repo.AddAsync(material);
        
        var assessment = AssessmentMapper.ToEntity(material.Id, _user.UserId);
        await _assessmentRepo.AddAssessmentAsync(assessment);
        
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();
        AppMetrics.MaterialCreated("manual");
        
        return MaterialMapper.ToDto(material);
    }

    public async Task<MaterialResponseDto?> UpdateAsync(Guid materialId, MaterialUpdateDto materialUpdateDto)
    {
        var material = await _repo.GetByIdAsync(
            materialId,
            _user.UserId);

        if (material is null)
            return null;

        // A link is shown as <a href>, so only web addresses: "javascript:" links would run code on click
        if (!string.IsNullOrEmpty(materialUpdateDto.Link) && !IsWebLink(materialUpdateDto.Link))
            throw new BadRequestException("Link must be a web address starting with http:// or https://");

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

    private static bool IsWebLink(string link) =>
        link.Length <= 2048
        && Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
