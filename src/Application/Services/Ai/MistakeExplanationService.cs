using Application.Ai.Prompts;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
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

    public MistakeExplanationService(IAttemptRepository attemptRepository, IAiService ai, IUserContext user)
    {
        _attemptRepository = attemptRepository;
        _ai = ai;
        _user = user;
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

        // Explanations are cached on results: a repeated click costs nothing
        var missing = mistakes
            .Where(x => x.AiExplanation is null)
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

            var explanations = (await _ai.RunAsync(new MistakeExplanationPrompt(items), cancellationToken)).Value;

            for (var i = 0; i < missing.Count; i++)
            {
                if (explanations.TryGetValue(i + 1, out var text))
                {
                    missing[i].AiExplanation = text.Length <= MaxExplanationLength ? text : text[..MaxExplanationLength];
                }
            }

            await _attemptRepository.SaveChangesAsync();
        }

        return mistakes
            .Where(x => x.AiExplanation is not null)
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
