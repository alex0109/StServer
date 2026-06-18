using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IMaterialTagService
{
    void SyncTags(Material material, List<Guid> tagIds);
}