using Domain.Entities;

namespace Application.Interfaces;

public interface IAssessmentRepository
{

    Task<Assessment?> GetAssessmentByMaterialIdAsync(Guid materialId, Guid userId);
    
    Task AddAssessmentAsync(Assessment assessment);

    Task SaveChangesAsync();
}