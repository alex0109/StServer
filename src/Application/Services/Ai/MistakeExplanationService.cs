using Application.Ai.Prompts;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Application.Profile;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Question;

namespace Application.Services.Ai;

public class MistakeExplanationService : IMistakeExplanationService
{
    private const int MaxExplanationLength = 2000;
    private const string NoAnswer = "(no answer)";

    private readonly IAttemptRepository _attemptRepository;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly IUserProfileService? _profiles;

    public MistakeExplanationService(
        IAttemptRepository attemptRepository,
        IAiService ai,
        IUserContext user,
        IUserProfileService? profiles = null)
    {
        _attemptRepository = attemptRepository;
        _ai = ai;
        _user = user;
        _profiles = profiles;
    }

    public async Task<List<ResultExplanationDto>?> ExplainAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await _attemptRepository.GetFullAttemptByIdAsync(attemptId, _user.UserId);
        if (attempt is null) return null;

        // Explanations show the right answers, so they are not available while answering
        if (attempt.AttemptStatus is AttemptStatus.InProgress or AttemptStatus.NotStarted)
            throw new BadRequestException("Finish the attempt to see explanations");

        var mistakes = attempt.Results
            .Where(x => !x.IsCorrect)
            .OrderBy(x => x.AnsweredAt)
            .ToList();

        var language = _profiles is null
            ? ProfileLanguages.QuestionLanguage
            : await _profiles.GetExplanationLanguageAsync(_user.UserId);

        // Explanations are cached on results: a repeated click costs nothing. A text in another
        // language than the one chosen now is written again; old texts were in the question's language
        var missing = mistakes
            .Where(x => x.AiExplanation is null
                        || (x.AiExplanationLanguage ?? ProfileLanguages.QuestionLanguage) != language)
            .Take(AiLimits.MaxExplainedMistakes)
            .ToList();

        if (missing.Count > 0)
        {
            var items = missing
                .Select((result, index) => new MistakeItem(
                    index + 1,
                    result.Question.Title,
                    CorrectAnswer(result.Question),
                    StudentAnswer(result),
                    result.Question.Explanation))
                .ToList();

            var prompt = new MistakeExplanationPrompt(items, ProfileLanguages.PromptName(language));
            var explanations = (await _ai.RunAsync(prompt, cancellationToken)).Value;

            for (var i = 0; i < missing.Count; i++)
            {
                if (explanations.TryGetValue(i + 1, out var text))
                {
                    missing[i].AiExplanation = text.Length <= MaxExplanationLength ? text : text[..MaxExplanationLength];
                    missing[i].AiExplanationLanguage = language;
                }
            }

            await _attemptRepository.SaveChangesAsync();
        }

        // Only texts in the chosen language: one the model skipped is not shown in the old language
        return mistakes
            .Where(x => x.AiExplanation is not null
                        && (x.AiExplanationLanguage ?? ProfileLanguages.QuestionLanguage) == language)
            .Select(x => new ResultExplanationDto
            {
                ResultId = x.Id,
                QuestionId = x.QuestionId,
                Explanation = x.AiExplanation!,
            })
            .ToList();
    }

    private static string CorrectAnswer(Question question)
    {
        if (question.QuestionType == QuestionType.Open) return question.Answer ?? NoAnswer;

        var names = question.Options
            .Where(x => x.IsCorrect)
            .OrderBy(x => x.Position)
            .Select(x => x.Name)
            .ToList();

        return names.Count > 0 ? string.Join("; ", names) : NoAnswer;
    }

    private static string StudentAnswer(Result result)
    {
        if (result.Question.QuestionType == QuestionType.Open)
            return string.IsNullOrWhiteSpace(result.UserAnswer) ? NoAnswer : result.UserAnswer;

        var names = result.Question.Options
            .Where(x => result.UserAnswerOptionIds.Contains(x.Id))
            .OrderBy(x => x.Position)
            .Select(x => x.Name)
            .ToList();

        return names.Count > 0 ? string.Join("; ", names) : NoAnswer;
    }
}
