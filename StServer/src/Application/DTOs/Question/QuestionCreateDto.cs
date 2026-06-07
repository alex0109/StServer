namespace StServer.Application.DTOs.Question;

public class QuestionCreateDto
{
    public Guid MaterialId { get; set; }

    public required string Title { get; set; }

    public int Difficulty { get; set; } = 0;

    public required string Answer { get; set; }
}