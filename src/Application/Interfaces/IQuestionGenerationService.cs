using Application.DTOs.Ai;
using Application.DTOs.Question;

namespace Application.Interfaces;

public interface IQuestionGenerationService
{
    Task<List<QuestionResponseDto>?> GenerateAsync(Guid materialId, QuestionGenerateDto dto, CancellationToken cancellationToken = default);
}
