using Application.DTOs.Option;
using Domain.Utility.Question;

namespace Application.DTOs.Review;

// A question to answer in review: no correct answer, it comes back only after answering
public class ReviewQuestionDto
{
    public required Guid Id { get; init; }
    public required Guid MaterialId { get; init; }
    public required string MaterialTitle { get; init; }
    public required string Title { get; init; }
    public required QuestionType QuestionType { get; init; }
    public List<OptionResponseDto> Options { get; init; } = [];
}
