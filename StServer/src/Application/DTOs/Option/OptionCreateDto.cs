namespace StServer.Application.DTOs.Option;

public class OptionCreateDto
{
    public required string Name { get; set; }
    public bool IsCorrect { get; set; }
}