using Application.Ai.Prompts;
using Application.Ai.Text;
using Application.DTOs.Podcast;
using Application.Exceptions;
using Application.Interfaces;
using Application.Podcasts;
using Application.Profile;
using Domain.Entities;
using Domain.Utility.Note;
using Domain.Utility.Podcast;

namespace Application.Services;

public class PodcastService : IPodcastService
{
    private readonly IPodcastRepository _podcasts;
    private readonly IMaterialRepository _materials;
    private readonly IAiService _ai;
    private readonly ISpeechSynthesizer _speech;
    private readonly IAudioStorage _storage;
    private readonly IPodcastJobs _jobs;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public PodcastService(
        IPodcastRepository podcasts,
        IMaterialRepository materials,
        IAiService ai,
        ISpeechSynthesizer speech,
        IAudioStorage storage,
        IPodcastJobs jobs,
        IUserContext user,
        TimeProvider? time = null)
    {
        _podcasts = podcasts;
        _materials = materials;
        _ai = ai;
        _speech = speech;
        _storage = storage;
        _jobs = jobs;
        _user = user;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<PodcastDto?> GetAsync(Guid materialId)
    {
        var podcast = await _podcasts.GetByMaterialAsync(materialId, _user.UserId);
        return podcast is null ? null : PodcastMapper.ToDto(podcast, _storage, Now);
    }

    public async Task<PodcastDto?> GenerateAsync(Guid materialId, PodcastGenerateDto dto, CancellationToken cancellationToken = default)
    {
        if (!PodcastRules.IsLanguage(dto.Language))
            throw new BadRequestException("Unknown podcast language");

        var material = await _materials.GetByIdAsync(materialId, _user.UserId);
        if (material is null) return null;

        // Checked before the AI is asked: without voices or storage the quota must not be spent
        if (!_speech.IsConfigured || !_storage.IsConfigured)
            throw new AiUnavailableException("Podcasts are not set up");

        var existing = await _podcasts.GetByMaterialAsync(materialId, _user.UserId);
        if (existing is not null && PodcastMapper.StatusAt(existing, Now) == PodcastStatus.Generating)
            throw new ConflictException("This podcast is still being recorded");

        var sections = Sources(material);
        if (sections.Sum(x => x.Text.Length) < PodcastRules.MinSourceLength)
            throw new BadRequestException("Not enough text for a podcast, add text to the material or its notes first");

        var prompt = new PodcastScriptPrompt(material.Title, sections, ProfileLanguages.Explanation[dto.Language!]);
        var script = (await _ai.RunAsync(prompt, cancellationToken)).Value;

        var now = Now;
        // The new podcast replaces the old one; its file is removed once the new one is recorded
        var podcast = existing ?? new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = _user.UserId,
            MaterialId = materialId,
            Language = dto.Language!,
            Script = script.ToDocument(),
            CreatedAt = now,
        };

        if (existing is null) await _podcasts.AddAsync(podcast);

        podcast.Language = dto.Language!;
        podcast.Script = script.ToDocument();
        podcast.Status = PodcastStatus.Generating;
        podcast.ChapterDurations = [];
        podcast.Error = null;
        podcast.CreatedAt = now;
        podcast.UpdatedAt = now;
        podcast.CompletedAt = null;

        await _podcasts.SaveChangesAsync();
        _jobs.RecordAudio(podcast.Id);

        return PodcastMapper.ToDto(podcast, _storage, now, prompt.IsContentTruncated);
    }

    public async Task<PodcastDto?> RetryAsync(Guid materialId)
    {
        var podcast = await _podcasts.GetByMaterialAsync(materialId, _user.UserId);
        if (podcast is null) return null;

        if (PodcastMapper.StatusAt(podcast, Now) != PodcastStatus.Failed)
            throw new ConflictException("Only a failed podcast can be recorded again");

        if (!_speech.IsConfigured || !_storage.IsConfigured)
            throw new AiUnavailableException("Podcasts are not set up");

        podcast.Status = PodcastStatus.Generating;
        podcast.Error = null;
        podcast.UpdatedAt = Now;
        await _podcasts.SaveChangesAsync();
        _jobs.RecordAudio(podcast.Id);

        return PodcastMapper.ToDto(podcast, _storage, Now);
    }

    // The material first, then its notes with text in the order they were made; drawings have no words
    private static List<PodcastSource> Sources(Material material)
    {
        var sources = new List<PodcastSource>
        {
            new(material.Title, RichTextConverter.ToPlainText(material.Content), null),
        };

        sources.AddRange(material.Notes
            .Where(note => note.Type is NoteType.text or NoteType.summary or NoteType.essay)
            .OrderBy(note => note.CreatedAt)
            .ThenBy(note => note.Id)
            .Select(note => new PodcastSource(note.Title, RichTextConverter.ToPlainText(note.TextContent), note.Id))
            .Where(source => source.Text.Length > 0));

        return sources;
    }
}
