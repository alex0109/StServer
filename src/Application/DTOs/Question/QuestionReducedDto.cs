using StServer.Application.DTOs.Option;
using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class QuestionReducedDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required QuestionType QuestionType { get; set; }
    public List<OptionResponseDto>? Options { get; set; }
}