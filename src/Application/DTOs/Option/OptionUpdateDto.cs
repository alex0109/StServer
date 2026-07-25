namespace Application.DTOs.Option;

public class OptionUpdateDto
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public bool? IsCorrect { get; set; }
}