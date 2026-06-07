namespace StServer.Application.DTOs.Question;

public class QuestionUpdateDto
{
    public string? Title { get; set; }

    public string? Answer { get; set; }
    
    public int? Difficulty { get; set; }
}