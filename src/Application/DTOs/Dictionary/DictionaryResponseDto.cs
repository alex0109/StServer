using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Dictionary;

public class DictionaryResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    [MaxLength(70)]
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<DictionaryEntryResponseDto>? Entries { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}