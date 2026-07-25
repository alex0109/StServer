using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IQuestionRepository
{
    Task<List<Question>> GetAllQuestionsAsync(Guid materialId, Guid userId);
    Task<List<Question>> GetActiveQuestionsAsync(Guid materialId, Guid userId);
    Task<Question?> GetByIdQuestionAsync(Guid materialId, Guid id, Guid userId);
    Task<Question> AddQuestionAsync(Question question);
    Task<bool> DeleteQuestionAsync(Guid materialId, Guid id, Guid userId);
    Task<int> CountByMaterialIdAsync(Guid userId);
    Task SaveChangesAsync();
}