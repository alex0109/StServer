using StServer.Application.DTOs.Option;

namespace StServer.Application.DTOs.Question;

public class QuestionReducedDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public List<OptionDto>? Options { get; set; }
}