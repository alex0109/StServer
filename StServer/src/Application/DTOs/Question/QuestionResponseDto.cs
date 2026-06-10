namespace StServer.Application.DTOs.Question;

public class QuestionResponseDto
{
    public Guid Id { get; set; }
    
    public Guid MaterialId { get; set; }

    public required string Title { get; set; }

    public required string Answer { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}