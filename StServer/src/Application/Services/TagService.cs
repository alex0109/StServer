using StServer.Application.DTOs.Tag;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _repo;
    private readonly IUserContext _user;

    public TagService(ITagRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<List<TagResponseDto>> GetAllTagsAsync()
    {
        var tags = await _repo.GetAllTags(_user.UserId) ?? [];

        return tags.Select(x => TagMapper.ToDto(x)).ToList();
    }
    
    public async Task<TagResponseDto?> GetByIdAsync(Guid tagId)
    {
        var tag = await _repo.GetTagByIdAsync(tagId, _user.UserId);

        if (tag is not null)
            return null;
        
        return TagMapper.ToDto(tag);

        
    }
    
    public async Task<TagResponseDto> CreateAsync(TagCreateDto dto)
    {
        var entity = TagMapper.ToEntity(dto, _user.UserId);
        
        // TODO: ADD TAG CHECK FOR DUPLICATES
        
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