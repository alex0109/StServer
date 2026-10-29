using Domain.Entities;

namespace Application.Interfaces;

public interface IQuestionRepository
{
    Task<List<Question>> GetAllQuestionsAsync(Guid materialId, Guid userId);
    Task<List<Question>> GetActiveQuestionsAsync(Guid materialId, Guid userId);
    Task<Question?> GetByIdQuestionAsync(Guid materialId, Guid id, Guid userId);
    Task<Question> AddQuestionAsync(Question question);
    Task<bool> DeleteQuestionAsync(Guid materialId, Guid id, Guid userId);
    // Deleted questions do not take a place in the limit
    Task<int> CountActiveByMaterialIdAsync(Guid materialId);
    Task<bool> MaterialExistsAsync(Guid materialId, Guid userId);
    // Removes deleted questions for good. Questions answered in attempts stay: they are part of the results
    Task<int> PurgeDeletedAsync(DateTime deletedBefore);
    Task SaveChangesAsync();
}