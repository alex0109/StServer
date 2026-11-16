using Application.Ai.Prompts;
using Application.DTOs.Video;
using Application.Exceptions;
using Application.Interfaces;
using Application.Videos;
using Domain.Entities;

namespace Application.Services;

public class VideoService : IVideoService
{
    private readonly IMaterialVideoRepository _videos;
    private readonly IMaterialRepository _materials;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public VideoService(
        IMaterialVideoRepository videos,
        IMaterialRepository materials,
        IAiService ai,
        IUserContext user,
        TimeProvider? time = null)
    {
        _videos = videos;
        _materials = materials;
        _ai = ai;
        _user = user;
        _time = time ?? TimeProvider.System;
    }

    public async Task<MaterialVideoDto?> GetAsync(Guid materialId)
    {
        var material = await _materials.GetByIdAsync(materialId, _user.UserId);
        if (material is null) return null;

        var video = await _videos.GetByMaterialAsync(materialId, _user.UserId);
        // Subtitles of a video the material no longer links to are not shown
        return video is not null && video.VideoId == YouTubeLink.VideoId(material.Link) ? VideoMapper.ToDto(video) : null;
    }

    public async Task<MaterialVideoDto?> ProcessAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        var material = await _materials.GetByIdAsync(materialId, _user.UserId);
        if (material is null) return null;

        var videoId = YouTubeLink.VideoId(material.Link)
            ?? throw new BadRequestException("The material links no YouTube video");

        var result = (await _ai.RunAsync(
                new VideoTranscriptPrompt(videoId),
                x => x.Segments.Count > 0,
                "No speech was found in this video",
                cancellationToken))
            .Value;

        var now = _time.GetUtcNow().UtcDateTime;
        var transcript = new VideoTranscript(result.Segments).ToDocument();

        // One per material: processing again, or another video, replaces the subtitles
        var video = await _videos.GetByMaterialAsync(materialId, _user.UserId);
        if (video is null)
        {
            video = new MaterialVideo
            {
                Id = Guid.NewGuid(),
                UserId = _user.UserId,
                MaterialId = materialId,
                VideoId = videoId,
                Language = result.Language,
                Transcript = transcript,
                CreatedAt = now,
            };
            await _videos.AddAsync(video);
        }
        else
        {
            video.VideoId = videoId;
            video.Language = result.Language;
            video.Transcript = transcript;
        }

        video.IsClipped = result.IsClipped;
        video.UpdatedAt = now;
        await _videos.SaveChangesAsync();

        return VideoMapper.ToDto(video);
    }
}
