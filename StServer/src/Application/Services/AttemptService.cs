using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Question;
using StServer.Application.DTOs.Result;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;
using StServer.Domain.Utility.Attempt;
using StServer.Domain.Utility.Question;

namespace StServer.Application.Services;

public class AttemptService : IAttemptService
{
    private readonly IAttemptRepository _repo;
    private readonly IQuestionRepository _questionRepo;
    private readonly IAssessmentRepository _assessmentRepo;
    private readonly IUserContext _user;
    private readonly IAnswerEvaluationService _answerEvaluationService;
    private readonly IAttemptScoringService _attemptScoringService;

    public AttemptService(
        IAttemptRepository repo, 
        IQuestionRepository questionRepo, 
        IAssessmentRepository assessmentRepo, 
        IUserContext user,
        IAnswerEvaluationService answerEvaluationService,
        IAttemptScoringService attemptScoringService)
    {
        _repo = repo;
        _questionRepo = questionRepo;
        _assessmentRepo = assessmentRepo;
        _user = user;
        _answerEvaluationService = answerEvaluationService;
        _attemptScoringService = attemptScoringService;
    }
    
    public async Task<List<AttemptResponseDto>> GetFinishedAttempts(Guid materialId)
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
        var assessment = await _assessmentRepo.GetAssessmentByIdAsync(assessmentId, _user.UserId);
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
        
        if (attempt is not null)
        {
            Guid questionId = resultRequestDto.QuestionId;

            var question = await _questionRepo.GetByIdQuestionAsync(attempt.Assessment.MaterialId, questionId, _user.UserId);
            
            if (question is not null)
            {
                if (question.CorrectOptionId is not null && resultRequestDto.UserAnswerOptionId is not null)
                {
                    bool isIdCorrect = question.CorrectOptionId == resultRequestDto.UserAnswerOptionId;

                    result = new AnswerEvaluationResult
                    {
                        IsCorrect = isIdCorrect,
                        Score = 100,
                        Method = EvaluationMethod.Exact
                    };
                }
                else if (question.Answer is not null && resultRequestDto.UserAnswer is not null)
                {
                    result = _answerEvaluationService.EvaluateAnswer(
                        question.Answer,
                        resultRequestDto.UserAnswer
                    );
                }
                
            
                var resultEntity = ResultMapper.ToEntity(
                    resultRequestDto, 
                    id, 
                    _user.UserId, 
                    result.IsCorrect, 
                    result.Score,
                    (int)question.QuestionDifficulty);
                
                resultEntity.AttemptId = id;

                await _repo.AddResultAsync(resultEntity);
            
                await _repo.SaveChangesAsync();
            }

        }

        return result;
    }

    public async Task<bool> FinishAttempt(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is null)
            return false;
        
        attempt.AttemptStatus = AttemptStatus.Finished;
        attempt.FinishedAt = DateTime.UtcNow;
            
        await _repo.SaveChangesAsync();
            
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