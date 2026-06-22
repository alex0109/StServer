using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Result;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class AttemptService : IAttemptService
{
    private readonly IAttemptRepository _repo;
    private readonly IQuestionRepository _questionRepo;
    private readonly IUserContext _user;

    public AttemptService(IAttemptRepository repo, IQuestionRepository questionRepo, IUserContext user)
    {
        _repo = repo;
        _questionRepo = questionRepo;
        _user = user;
    }

    public async Task<AttemptResponseDto?> GetAttempt(Guid id)
    {
        var result = await _repo.GetAttemptByIdAsync(id, _user.UserId);

        if (result is null)
        {
            return null;
        }

        return AttemptMapper.ToDto(result);
    }

    public async Task<Guid> StartAttempt(Guid assessmentId)
    {
        var attemptEntity = AttemptMapper.ToEntity(assessmentId);
        
        await _repo.AddAsync(attemptEntity);

        await _repo.SaveChangesAsync();

        return attemptEntity.Id;
    }

    public async Task<bool> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto)
    {
        var attempt = await _repo.GetAttemptByIdAsync(id, _user.UserId);
            
        if (attempt is not null)
        {
            Guid questionId = resultRequestDto.QuestionId;

            var question = await _questionRepo.GetByIdQuestionAsync(attempt.Assessment.MaterialId, questionId, _user.UserId);

            //IMPORTANT PLACE TO REPLACE IN THE FUTURE FOR AI CHECKING
            bool isAnswerCorrect = question.Answer == resultRequestDto.UserAnswer;
            //IMPORTANT PLACE TO REPLACE IN THE FUTURE FOR AI CHECKING
            
            var resultEntity = ResultMapper.ToEntity(resultRequestDto, isAnswerCorrect);
            
            attempt.Results.Add(resultEntity);
            
            await _repo.SaveChangesAsync();

            return true;
        }

        return false;
    }

    public async Task<AttemptResponseDto?> SubmitAttempt(Guid id)
    {
        await _repo.
    }

    public async Task<AttemptResponseDto?> GetResults(Guid id)
    {
        var attempt = await _repo.GetAttemptByIdAsync(id, _user.UserId);

        if (attempt is not null)
        {
            var resultsList = attempt.Results.Select(x => ResultMapper.ToDto(x)).ToList();

            return AttemptMapper.ToDto(attempt, resultsList);
        }

        return null;
    }
}