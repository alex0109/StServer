using Domain.Utility.Result;
using Application.DTOs.Attempt;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Question;
using Application.Observability;

namespace Application.Services;

public class AttemptService : IAttemptService
{
    private readonly IAttemptRepository _repo;
    private readonly IQuestionRepository _questionRepo;
    private readonly IAssessmentRepository _assessmentRepo;
    private readonly IUserContext _user;
    private readonly IAnswerEvaluationService _answerEvaluationService;
    private readonly IAttemptScoringService _attemptScoringService;
    private readonly IReviewRatingResolver _reviewRatingResolver;
    private readonly IQuestionReviewService? _questionReviews;

    public AttemptService(
        IAttemptRepository repo, 
        IQuestionRepository questionRepo, 
        IAssessmentRepository assessmentRepo, 
        IUserContext user,
        IAnswerEvaluationService answerEvaluationService,
        IAttemptScoringService attemptScoringService,
        IReviewRatingResolver reviewRatingResolver,
        IQuestionReviewService? questionReviews = null)
    {
        _repo = repo;
        _questionRepo = questionRepo;
        _assessmentRepo = assessmentRepo;
        _user = user;
        _answerEvaluationService = answerEvaluationService;
        _attemptScoringService = attemptScoringService;
        _reviewRatingResolver = reviewRatingResolver;
        _questionReviews = questionReviews;
    }
    
    public async Task<List<AttemptResponseDto>?> GetFinishedAttempts(Guid materialId)
    {
        var attempts = await _repo.GetFinishedAttemptsAsync(materialId, _user.UserId);

        if (attempts is null || attempts.Count == 0)
            return null;
        
        var results = attempts.Select(x => AttemptMapper.ToDto(x)).ToList();

        foreach (var attempt in results)
        {
            _attemptScoringService.AverageScoreAttempt(attempt);
        }

        return results;
    }

    public async Task<AttemptResponseDto?> GetAttempt(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is null)
            return null;
        
        var result = AttemptMapper.ToDto(attempt);

        _attemptScoringService.AverageScoreAttempt(result);
        
        return result;
    }

    public async Task<Guid?> StartAttempt(Guid assessmentId)
    {
        var assessment = await _assessmentRepo.GetAssessmentByMaterialIdAsync(assessmentId, _user.UserId);
        if (assessment is null) return null;
        
        var attemptEntity = AttemptMapper.ToEntity(assessmentId, _user.UserId);
        await _repo.AddAttemptAsync(attemptEntity);
        await _repo.SaveChangesAsync();

        return attemptEntity.Id;
    }

    public async Task<AnswerEvaluationResult> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto)
    {
        var attempt = await _repo.GetFullAttemptByIdAsync(id, _user.UserId);

        AnswerEvaluationResult result = new AnswerEvaluationResult
        {
            IsCorrect = false,
            Score = 0,
            Method = EvaluationMethod.None
        };

        if (attempt is null)
            return result;

        var questionId = resultRequestDto.QuestionId;
        var question = await _questionRepo.GetByIdQuestionAsync(attempt.Assessment.MaterialId, questionId, _user.UserId);

        if (question is null)
            return result;

        if (question.QuestionType is QuestionType.Options or QuestionType.TrueFalse)
        {
            var correctOptionIds = question.Options
                .Where(o => o.IsCorrect)
                .Select(o => o.Id)
                .ToList();

            result = _answerEvaluationService.EvaluateOptions(
                correctOptionIds,
                resultRequestDto.UserAnswerOptionIds
            );
        }
        else if (question.Answer is not null && resultRequestDto.UserAnswer is not null)
        {
            result = await _answerEvaluationService.EvaluateAnswerAsync(
                question.Title,
                question.Answer,
                resultRequestDto.UserAnswer
            );
        }

        var rating = _reviewRatingResolver.Resolve(result.IsCorrect, resultRequestDto.ConfidenceLevel);

        var now = DateTime.UtcNow;
        var existing = attempt.Results.FirstOrDefault(r => r.QuestionId == questionId);

        if (existing is null)
        {
            var checkpoint = attempt.Results
                .Where(r => r.QuestionId != questionId)
                .Select(r => r.LastModifiedAt ?? r.AnsweredAt)
                .DefaultIfEmpty(attempt.StartedAt)
                .Max();

            var timeSpent = now - checkpoint;
            if (timeSpent < TimeSpan.Zero)
                timeSpent = TimeSpan.Zero;

            var resultEntity = ResultMapper.ToEntity(
                resultRequestDto,
                id,
                _user.UserId,
                result.IsCorrect,
                result.Score,
                (int)question.QuestionDifficulty,
                timeSpent);

            resultEntity.AnsweredAt = now;
            resultEntity.LastModifiedAt = now;
            resultEntity.Rating = rating;
            resultEntity.Method = result.Method;

            await _repo.AddResultAsync(resultEntity);
        }
        else
        {
            ResultMapper.ApplyUpdate(existing, resultRequestDto, result.IsCorrect, result.Score);
            existing.LastModifiedAt = now;
            existing.TimeSpent = now - existing.AnsweredAt;
            existing.Rating = rating;
            existing.Method = result.Method;
        }

        await _repo.SaveChangesAsync();

        return result;
    }

    public async Task<bool> FinishAttempt(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is null)
            return false;

        // Finishing twice keeps the first time, and the answers are scheduled only once
        if (attempt.AttemptStatus == AttemptStatus.Finished)
            return true;

        attempt.AttemptStatus = AttemptStatus.Finished;
        attempt.FinishedAt = DateTime.UtcNow;

        // Every answered question becomes one spaced repetition review, saved with the attempt
        if (_questionReviews is not null)
            await _questionReviews.ScheduleFinishedAttemptAsync(attempt);

        await _repo.SaveChangesAsync();
        AppMetrics.AttemptsFinished.Add(1);

        return true;
    }
    
    public async Task MarkAbandonedAttempts()
    {
        var attempts = await _repo.GetAbandonedAttemptsAsync();
        
        if (attempts is null || attempts.Count == 0)
            return;
        
        foreach (var attempt in attempts)
        {
            attempt.AttemptStatus = AttemptStatus.Abandoned;
        }
        
        await _repo.SaveChangesAsync();
    }

    public static readonly TimeSpan AcceptUndoWindow = TimeSpan.FromMinutes(10);

    public async Task<AttemptResponseDto?> AcceptAnswerAsync(Guid attemptId, Guid resultId)
    {
        var attempt = await _repo.GetFullAttemptByIdAsync(attemptId, _user.UserId);
        if (attempt is null) return null;

        var result = attempt.Results.FirstOrDefault(r => r.Id == resultId);
        if (result is null) return null;

        // Already accepted: nothing to do, same answer as the first time
        if (result.Method == EvaluationMethod.Manual && result.OverriddenAt is not null)
            return await GetResults(attemptId);

        if (attempt.AttemptStatus != AttemptStatus.Finished)
            throw new BadRequestException("Answers can be accepted only in a finished attempt");

        if (!ResultMapper.CanAccept(result))
            throw new BadRequestException("Only open answers rejected by similarity or AI can be accepted");

        result.OriginalIsCorrect = result.IsCorrect;
        result.OriginalScore = result.Score;
        result.OriginalMethod = result.Method;
        result.OverriddenAt = DateTime.UtcNow;

        result.IsCorrect = true;
        result.Score = 100;
        result.Method = EvaluationMethod.Manual;
        // The explanation was about a mistake that, as it turns out, was not one
        result.AiExplanation = null;
        result.AiExplanationLanguage = null;

        await ReRateAsync(attempt.Id, result);
        await _repo.SaveChangesAsync();

        return await GetResults(attemptId);
    }

    public async Task<AttemptResponseDto?> UndoAcceptAsync(Guid attemptId, Guid resultId)
    {
        var attempt = await _repo.GetFullAttemptByIdAsync(attemptId, _user.UserId);
        if (attempt is null) return null;

        var result = attempt.Results.FirstOrDefault(r => r.Id == resultId);
        if (result is null) return null;

        if (result.OverriddenAt is null || result.OriginalMethod is null)
            return await GetResults(attemptId);

        if (DateTime.UtcNow - result.OverriddenAt > AcceptUndoWindow)
            throw new ConflictException("The acceptance can no longer be undone");

        result.IsCorrect = result.OriginalIsCorrect ?? false;
        result.Score = result.OriginalScore ?? 0;
        result.Method = result.OriginalMethod.Value;
        result.OriginalIsCorrect = null;
        result.OriginalScore = null;
        result.OriginalMethod = null;
        result.OverriddenAt = null;

        await ReRateAsync(attempt.Id, result);
        await _repo.SaveChangesAsync();

        return await GetResults(attemptId);
    }

    // The repetition rating follows the grade, and the question's review history is replayed with it
    private async Task ReRateAsync(Guid attemptId, Result result)
    {
        result.Rating = _reviewRatingResolver.Resolve(result.IsCorrect, result.ConfidenceLevel);

        if (_questionReviews is not null)
            await _questionReviews.ReRateAttemptAnswerAsync(attemptId, result.QuestionId, result.UserId, result.Rating);
    }

    public async Task<AttemptResponseDto?> GetResults(Guid id)
    {
        var attempt = await _repo.GetFullAttemptByIdAsync(id, _user.UserId);

        if (attempt is null)
            return null;
        
        var result = AttemptMapper.ToDto(attempt);

        _attemptScoringService.AverageScoreAttempt(result);
        
        return result;
    }
}