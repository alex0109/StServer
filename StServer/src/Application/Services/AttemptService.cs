using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Result;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;
using StServer.Domain.Utility.Attempt;

namespace StServer.Application.Services;

public class AttemptService : IAttemptService
{
    private readonly IAttemptRepository _repo;
    private readonly IQuestionRepository _questionRepo;
    private readonly IAssessmentRepository _assessmentRepo;
    private readonly IUserContext _user;

    public AttemptService(IAttemptRepository repo, IQuestionRepository questionRepo, IAssessmentRepository assessmentRepo, IUserContext user)
    {
        _repo = repo;
        _questionRepo = questionRepo;
        _assessmentRepo = assessmentRepo;
        _user = user;
    }

    public async Task<AttemptResponseDto?> GetAttempt(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is not null)
        {
            return AttemptMapper.ToDto(attempt);
        }

        return null;
    }

    public async Task<Guid?> StartAttempt(Guid assessmentId)
    {
        var assessment = await _assessmentRepo.GetAssessmentByIdAsync(assessmentId, _user.UserId);

        if (assessment is not null)
        {
            var attemptEntity = AttemptMapper.ToEntity(assessmentId);
        
            await _repo.AddAsync(attemptEntity);

            await _repo.SaveChangesAsync();

            return attemptEntity.Id;
        }

        return null;
    }

    public async Task<bool> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto)
    {
        var attempt = await _repo.GetFullAttemptByIdAsync(id, _user.UserId);
        
        if (attempt is not null && attempt.AttemptStatus == AttemptStatus.Finished)
        {
            return false;
        }
        
        if (attempt is not null)
        {
            Guid questionId = resultRequestDto.QuestionId;

            var question = await _questionRepo.GetByIdQuestionAsync(attempt.Assessment.MaterialId, questionId, _user.UserId);

            if (question is not null)
            {
                bool isAnswerCorrect = false;
                // TODO: IMPORTANT PLACE TO REPLACE IN THE FUTURE FOR AI CHECKING
                if (question.Answer is not null && resultRequestDto.UserAnswer is not null)
                {
                    isAnswerCorrect = question.Answer.Trim().ToLower() == resultRequestDto.UserAnswer.Trim().ToLower();
                }

                if (question.CorrectOptionId is not null && resultRequestDto.UserAnswerOptionId is not null)
                {
                    isAnswerCorrect = question.CorrectOptionId == resultRequestDto.UserAnswerOptionId;
                }
                // TODO: IMPORTANT PLACE TO REPLACE IN THE FUTURE FOR AI CHECKING
            
                var resultEntity = ResultMapper.ToEntity(resultRequestDto, id, isAnswerCorrect);
                resultEntity.AttemptId = id;
            
                attempt.Results.Add(resultEntity);
            
                await _repo.SaveChangesAsync();

                return true;
            }

        }

        return false;
    }

    public async Task<AttemptResponseDto?> SubmitAttempt(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is not null)
        {
            attempt.AttemptStatus = AttemptStatus.Finished;
            attempt.FinishedAt = DateTime.UtcNow;
            
            await _repo.SaveChangesAsync();
            
            return AttemptMapper.ToDto(attempt);
        }

        return null;
    }

    public async Task<AttemptResponseDto?> GetResults(Guid id)
    {
        var attempt = await _repo.GetAttemptWithResultsByIdAsync(id, _user.UserId);

        if (attempt is not null)
        {
            return AttemptMapper.ToDto(attempt);
        }

        return null;
    }
}