namespace StServer.Application.DTOs.Question;

public class QuestionReducedDto
{
    public Guid Id { get; set; }

    public required string Title { get; set; }
}