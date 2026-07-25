using Domain.Entities;

namespace Application.Interfaces;

public interface IAssessmentRepository
{

    Task<Assessment?> GetAssessmentByIdAsync(Guid id, Guid userId);
    
    Task AddAssessmentAsync(Assessment assessment);

    Task SaveChangesAsync();
}