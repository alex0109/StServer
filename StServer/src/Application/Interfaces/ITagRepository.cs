using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface ITagRepository
{
    Task<List<Tag>?> GetAllTags(Guid userId);
    Task<List<Material>?> GetMaterialsByTagAsync(Guid tagId, Guid userId);
    Task<Tag?> GetTagByIdAsync(Guid id, Guid userId);
    Task<Tag> AddTagAsync(Tag tag);
    Task<bool> DeleteTagAsync(Guid id, Guid userId);
    Task<int> CountByUserIdAsync(Guid userId);
    Task SaveChangesAsync();
}