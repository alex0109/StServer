namespace StServer.Application.DTOs;

public class QuestionItemDto
{
    public Guid Id { get; set; }
    
    public Guid MaterialId { get; set; }

    public string Title { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}