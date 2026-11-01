using System.Text.Json;
using Application.Ai.Prompts;
using Application.Ai.Text;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Utility.Note;

namespace Application.Services.Ai;

public class SummaryGenerationService : ISummaryGenerationService
{
    private readonly IMaterialRepository _materialRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IDictionaryRepository _dictionaryRepository;
    private readonly IAiService _ai;
    private readonly IUserContext _user;

    public SummaryGenerationService(
        IMaterialRepository materialRepository,
        INoteRepository noteRepository,
        IDictionaryRepository dictionaryRepository,
        IAiService ai,
        IUserContext user)
    {
        _materialRepository = materialRepository;
        _noteRepository = noteRepository;
        _dictionaryRepository = dictionaryRepository;
        _ai = ai;
        _user = user;
    }

    public async Task<SummaryGenerateResponseDto?> GenerateAsync(
        Guid materialId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var userId = _user.UserId;

        var material = await _materialRepository.GetByIdAsync(materialId, userId);
        if (material is null) return null;

        var summary = await _noteRepository.GetByIdAsync(noteId, materialId, userId);
        if (summary is null) return null;

        if (summary.Type != NoteType.summary)
            throw new BadRequestException("Only a summary note can be generated");

        var notes = (await _noteRepository.GetAllAsync(materialId, userId))
            .Where(x => x.Id != noteId && x.Type == NoteType.text)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new SummarySource(x.Title, RichTextConverter.ToPlainText(x.TextContent)))
            .Where(x => x.Text.Length > 0)
            .ToList();

        var dictionaries = (await _dictionaryRepository.GetAllByMaterialIdAsync(materialId, userId))
            .Select(x => new SummaryDictionarySource(
                x.Title,
                x.Entries
                    .Where(e => !e.IsArchived)
                    .OrderBy(e => e.Position)
                    .Select(e => (e.Term, e.Translation))
                    .ToList()))
            .Where(x => x.Entries.Count > 0)
            .ToList();

        var materialText = RichTextConverter.ToPlainText(material.Content);

        var sourceLength = materialText.Length
                           + notes.Sum(x => x.Text.Length)
                           + dictionaries.Sum(x => x.Entries.Count * 10);
        if (sourceLength < AiLimits.MinSourceLength)
            throw new BadRequestException("Not enough content to summarize, add text to the material or notes first");

        var prompt = new SummaryPrompt(material.Title, materialText, notes, dictionaries);
        var sections = (await _ai.RunAsync(
            prompt, x => x.Count > 0, "AI could not write a summary for this material", cancellationToken)).Value;

        // Stored in the same shape as notes saved from the editor
        summary.TextContent = JsonSerializer.SerializeToDocument(RichTextConverter.FromSections(sections));
        summary.UpdatedAt = DateTime.UtcNow;

        await _noteRepository.SaveChangesAsync();

        return new SummaryGenerateResponseDto
        {
            Note = NoteMapper.ToDto(summary),
            IsContentTruncated = prompt.IsContentTruncated,
        };
    }
}
