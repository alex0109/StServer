using Application.Ai.Prompts;
using Application.Ai.Text;
using Application.DTOs.Ai;
using Application.Essays;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Application.Profile;
using Domain.Utility.Note;

namespace Application.Services.Ai;

public class EssayReviewService : IEssayReviewService
{
    private readonly INoteRepository _notes;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;
    private readonly IUserProfileService? _profiles;

    public EssayReviewService(
        INoteRepository notes,
        IAiService ai,
        IUserContext user,
        TimeProvider? time = null,
        IUserProfileService? profiles = null)
    {
        _notes = notes;
        _ai = ai;
        _user = user;
        _time = time ?? TimeProvider.System;
        _profiles = profiles;
    }

    public async Task<EssayReviewResponseDto?> ReviewAsync(
        Guid materialId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var note = await _notes.GetByIdAsync(noteId, materialId, _user.UserId);
        if (note is null) return null;

        if (note.Type != NoteType.essay)
            throw new BadRequestException("Only an essay can be checked");

        // Checked before the AI is asked, so a too short text costs nothing from the quota
        var text = RichTextConverter.ToPlainText(note.TextContent);
        var wordCount = EssayRules.CountWords(text);
        if (wordCount < EssayRules.MinWords)
            throw new BadRequestException($"The essay is too short to check, write at least {EssayRules.MinWords} words");

        // Feedback in the language chosen for explanations, like mistake explanations
        var language = _profiles is null
            ? ProfileLanguages.QuestionLanguage
            : await _profiles.GetExplanationLanguageAsync(_user.UserId);

        var prompt = new EssayReviewPrompt(text, note.EssayTask, ProfileLanguages.PromptName(language));
        var draft = (await _ai.RunAsync(prompt, cancellationToken)).Value;

        var review = new EssayReview
        {
            Score = EssayRules.Overall(draft.Criteria),
            Criteria = draft.Criteria.ToList(),
            Strengths = draft.Strengths.ToList(),
            Improvements = draft.Improvements.ToList(),
            Corrections = draft.Corrections.ToList(),
            WordCount = wordCount,
            Language = language,
            IsTextTruncated = prompt.IsTextTruncated,
            CheckedAt = _time.GetUtcNow().UtcDateTime,
        };

        // The text is not changed, so the note keeps its UpdatedAt
        note.EssayReview = EssayRules.ToDocument(review);
        await _notes.SaveChangesAsync();

        return new EssayReviewResponseDto { Note = NoteMapper.ToDto(note) };
    }
}
