using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IMaterialTagService
{
    void SyncTags(Domain.Entities.Material material, List<Guid> tagIds);
}