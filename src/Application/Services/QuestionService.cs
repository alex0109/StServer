using Application.Constants;
using Application.DTOs.Option;
using Application.DTOs.Question;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Question;

namespace Application.Services;

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
    
    public async Task<List<QuestionReducedDto>> GetActiveReducedQuestionsAsync(Guid materialId)
    {
        var questions = await _repo.GetActiveQuestionsAsync(materialId, _user.UserId);
        
        return questions.Select(QuestionMapper.ToReducedDto).ToList();
    }
    
    public async Task<List<QuestionReducedDto>> GetAllReducedQuestionsAsync(Guid materialId)
    {
        var questions = await _repo.GetAllQuestionsAsync(materialId, _user.UserId);
        
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
        var count = await _repo.CountByMaterialIdAsync(materialId);

        if (count >= UserLimits.MaxQuestionsPerMaterial)
        {
            throw new LimitExceededException("Question limit reached");
        }
        
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

        var options = questionDto.Options
            .Select(x => new OptionUpdateDto { Name = x.Name, IsCorrect = x.IsCorrect })
            .ToList();

        _optionService.SyncOptions(question, options);

        await _repo.AddQuestionAsync(question);
        await _repo.SaveChangesAsync();

        return QuestionMapper.ToDto(question);
    }

    public async Task<QuestionResponseDto> CreateTrueFalseQuestionAsync(Guid materialId, TrueFalseQuestionCreateDto questionDto)
    {
        var count = await _repo.CountByMaterialIdAsync(materialId);

        if (count >= UserLimits.MaxQuestionsPerMaterial)
        {
            throw new LimitExceededException("Question limit reached");
        }

        var question = QuestionMapper.ToEntityTrueFalseQuestion(questionDto, materialId, _user.UserId);

        _optionService.SetTrueFalseOptions(question, questionDto.IsTrue);

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

        ValidateUpdate(question.QuestionType, questionDto);

        QuestionMapper.ApplyUpdate(question, questionDto);

        if (questionDto.Options is not null)
        {
            _optionService.SyncOptions(question, questionDto.Options);
        }
        else if (questionDto.CorrectOptionId is Guid correctOptionId)
        {
            _optionService.SetCorrectOption(question, correctOptionId);
        }

        await _repo.SaveChangesAsync();

        return QuestionMapper.ToDto(question);
    }

    private static void ValidateUpdate(QuestionType questionType, QuestionUpdateDto questionDto)
    {
        switch (questionType)
        {
            case QuestionType.Open when questionDto.Options is not null || questionDto.CorrectOptionId is not null:
                throw new BadRequestException("Open question has no options");
            case QuestionType.Options or QuestionType.TrueFalse when questionDto.Answer is not null:
                throw new BadRequestException("Question with options has no text answer");
            case QuestionType.TrueFalse when questionDto.Options is not null:
                throw new BadRequestException("True/False options can't be changed, update CorrectOptionId instead");
        }
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