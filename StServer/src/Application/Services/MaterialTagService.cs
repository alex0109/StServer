using StServer.Application.Interfaces;
using StServer.Domain.Entities;

namespace StServer.Application.Services;

public class MaterialTagService : IMaterialTagService
{
    public void SyncTags(
        Material material,
        List<Guid> tagIds)
    {
        var incoming = tagIds.ToHashSet();

        var toRemove = material.MaterialTags
            .Where(x => !incoming.Contains(x.TagId))
            .ToList();

        foreach (var item in toRemove)
        {
            material.MaterialTags.Remove(item);
        }

        var existing = material.MaterialTags
            .Select(x => x.TagId)
            .ToHashSet();

        var missing = incoming.Except(existing);

        foreach (var tagId in missing)
        {
            material.MaterialTags.Add(
                new MaterialTag
                {
                    MaterialId = material.Id,
                    TagId = tagId
                });
        }
    }
}