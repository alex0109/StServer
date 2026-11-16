using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Domain.Entities;

// Subtitles of the YouTube video a material links to, written by the AI from the video itself.
// One per material: processing the video again replaces them
public class MaterialVideo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    // YouTube's id of the video the subtitles belong to; a changed link makes them stale
    [MaxLength(20)]
    public required string VideoId { get; set; }

    // Language the video is spoken in, as a code
    [MaxLength(20)]
    public required string Language { get; set; }

    // Lines with the second they start at
    public required JsonDocument Transcript { get; set; }

    // Only the start of a long video is processed
    public bool IsClipped { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
