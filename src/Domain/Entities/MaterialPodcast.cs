using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Utility.Podcast;

namespace Domain.Entities;

// An AI podcast of a material and its notes; one per material, a new one replaces it
public class MaterialPodcast
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    // Language code the podcast is spoken in
    [MaxLength(10)]
    public required string Language { get; set; }
    public PodcastStatus Status { get; set; }

    // Chapters with their lines: the material first, then one per note
    public required JsonDocument Script { get; set; }
    // Length of every chapter in seconds, known once the audio is recorded
    public double[] ChapterDurations { get; set; } = [];

    // Where the audio file lives in storage
    [MaxLength(300)]
    public string? AudioPath { get; set; }
    public long AudioSize { get; set; }
    public double DurationSeconds { get; set; }

    [MaxLength(500)]
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
