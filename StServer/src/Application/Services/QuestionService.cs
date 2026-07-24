using StServer.Application.Constants;
using StServer.Application.DTOs.Option;
using StServer.Application.DTOs.Question;
using StServer.Application.Exceptions;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;
using StServer.Domain.Entities;

namespace StServer.Application.Services;

public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _repo;
    private readonly IUserContext _user;
    private readonly IOptionService _optionService;

    public QuestionService(IQuestionRepository repo, IUserContext user, IOptionService optionService)
    {
        _repo = repo;
        _user = user;
        _optionService = optionService;
    }

    public async Task<List<QuestionResponseDto>> GetActiveQuestionsAsync(Guid materialId)
    {
        var questions = await _repo.GetActiveQuestionsAsync(materialId, _user.UserId);
        return questions.Select(QuestionMapper.ToDto).ToList();
    }
    
    public async Task<List<QuestionReducedDto>> GetAllReducedQuestionsAsync(Guid materialId)
    {
        var questions = await _repo.GetActiveQuestionsAsync(materialId, _user.UserId);
        
        return questions.Select(QuestionMapper.ToReducedDto).ToList();
    }

    public async Task<QuestionResponseDto?> GetByIdQuestionAsync(Guid materialId, Guid id)
    {
        var question = await _repo.GetByIdQuestionAsync(materialId, id, _user.UserId);

        if (question is null)
            return null;
        
        return QuestionMapper.ToDto(question);
    }

    public async Task<QuestionResponseDto> CreateOpenQuestionAsync(Guid materialId, OpenQuestionCreateDto questionDto)
    {
        var question = QuestionMapper.ToEntityOpenQuestion(questionDto, materialId, _user.UserId);

        var response = await _repo.AddQuestionAsync(question);
        
        await _repo.SaveChangesAsync();

        return QuestionMapper.ToDto(response);
    }
    
    public async Task<QuestionResponseDto> CreateQuestionWithOptionsAsync(Guid materialId, OptionQuestionCreateDto questionDto)
    {
        var count = await _repo.CountByMaterialIdAsync(materialId);

        if (count >= UserLimits.MaxQuestionsPerMaterial)
        {
            throw new LimitExceededException("Question limit reached");
        }
        
        var question = QuestionMapper.ToEntityOptionsQuestion(questionDto, materialId, _user.UserId);

        question.Options = new List<Option>();

        _optionService.SyncOptions(question, questionDto.Options);

        await _repo.AddQuestionAsync(question);
        await _repo.SaveChangesAsync();

        return QuestionMapper.ToDto(question);
    }

    public async Task<QuestionResponseDto?> UpdateQuestionAsync(Guid materialId, Guid id, QuestionUpdateDto questionDto)
    {
        var question = await _repo.GetByIdQuestionAsync(
            materialId,
            id,
            _user.UserId);

        if (question is null)
            return null;

        QuestionMapper.ApplyUpdate(question, questionDto);

        if (questionDto.Options is not null)
        {
            _optionService.SyncOptions(question, questionDto.Options);
        }

        await _repo.SaveChangesAsync();

        return QuestionMapper.ToDto(question);
    }

    public async Task<bool> DeleteQuestionAsync(Guid materialId, Guid id)
    {
        var question = await _repo.GetByIdQuestionAsync(materialId, id, _user.UserId);

        if (question is null)
            return false;
        
        question.IsActive = false;
        question.DeletedAt = DateTime.UtcNow;
        
        await _repo.SaveChangesAsync();

        return true;
    }
}