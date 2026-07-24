using StServer.Application.DTOs.Question;

namespace StServer.Application.Interfaces;

public interface IQuestionService
{
    Task<List<QuestionResponseDto>> GetActiveQuestionsAsync(Guid materialId);
    
    Task<List<QuestionReducedDto>> GetActiveReducedQuestionsAsync(Guid materialId);
    
    Task<List<QuestionReducedDto>> GetAllReducedQuestionsAsync(Guid materialId);
    
    Task<QuestionResponseDto?> GetByIdQuestionAsync(Guid materialId, Guid id);

    Task<QuestionResponseDto> CreateOpenQuestionAsync(Guid materialId, OpenQuestionCreateDto questionDto);
    
    Task<QuestionResponseDto> CreateQuestionWithOptionsAsync(Guid materialId, OptionQuestionCreateDto questionDto);

    Task<QuestionResponseDto?> UpdateQuestionAsync(Guid materialId, Guid id, QuestionUpdateDto questionDto);

    Task<bool> DeleteQuestionAsync(Guid materialId, Guid id);
}