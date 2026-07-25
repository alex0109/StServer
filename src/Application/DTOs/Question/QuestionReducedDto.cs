using Application.DTOs.Option;
using Domain.Utility.Question;

namespace Application.DTOs.Question;

public class QuestionReducedDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required QuestionType QuestionType { get; set; }
    public List<OptionResponseDto>? Options { get; set; }
}