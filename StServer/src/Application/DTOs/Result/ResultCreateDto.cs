namespace StServer.Application.DTOs.Result;

public class ResultCreateDto
{
    public Guid QuestionId { get; set; }
    public required string UserAnswer { get; set; }
}