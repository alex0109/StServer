using Application.Constants;
using Application.DTOs.Material;
using Application.DTOs.Tag;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;

namespace Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _repo;
    private readonly IUserContext _user;

    public TagService(ITagRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<List<TagResponseDto>?> GetAllTagsAsync()
    {
        var tags = await _repo.GetAllTags(_user.UserId);

        if (tags is null)
        {
            return null;
        }

        return tags.Select(x => TagMapper.ToDto(x)).ToList();
    }

    public async Task<List<MaterialResponseDto>?> GetMaterialsByTagAsync(Guid tagId)
    {
        var materials = await _repo.GetMaterialsByTagAsync(tagId, _user.UserId);

        if (materials is null)
        {
            return null;
        }

        return materials.Select(MaterialMapper.ToDto).ToList();
    }
    
    public async Task<TagResponseDto?> GetByIdAsync(Guid tagId)
    {
        var tag = await _repo.GetTagByIdAsync(tagId, _user.UserId);

        if (tag is null)
            return null;
        
        return TagMapper.ToDto(tag);
    }
    
    public async Task<TagResponseDto> CreateAsync(TagCreateDto dto)
    {
        var count = await _repo.CountByUserIdAsync(_user.UserId);

        if (count >= UserLimits.MaxTags)
        {
            throw new LimitExceededException("Tag limit reached");
        }
        
        var entity = TagMapper.ToEntity(dto, _user.UserId);
        
        var response = await _repo.AddTagAsync(entity);

        await _repo.SaveChangesAsync();

        return TagMapper.ToDto(response);
    }
    
    public async Task<TagResponseDto?> UpdateTagAsync(Guid tagId, TagUpdateDto tagDto)
    {
        var tag = await _repo.GetTagByIdAsync(tagId, _user.UserId);

        if (tag is null)
            return null;
        
        TagMapper.ApplyUpdate(tag, tagDto);

        await _repo.SaveChangesAsync();

        return TagMapper.ToDto(tag);

        
    }
    
    public async Task<bool> DeleteTagAsync(Guid tagId)
    {
        var result = await _repo.DeleteTagAsync(tagId, _user.UserId);
        
        await _repo.SaveChangesAsync();

        return result;
    }
}