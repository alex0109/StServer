namespace StServer.Application.DTOs.Result;

public class ResultUpdateDto
{
    public string? UserAnswer { get; set; }
    
    public bool? IsCorrect  { get; set; }
}