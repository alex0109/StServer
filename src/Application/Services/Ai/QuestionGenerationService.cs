using Application.Ai.Prompts;
using Application.Constants;
using Application.DTOs.Ai;
using Application.DTOs.Option;
using Application.DTOs.Question;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Question;

namespace Application.Services.Ai;

public class QuestionGenerationService : IQuestionGenerationService
{
    private readonly IQuestionRepository _questionRepository;
    private readonly IContentLimits _limits;
    private readonly IMaterialRepository _materialRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IOptionService _optionService;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;

    public QuestionGenerationService(
        IQuestionRepository questionRepository,
        IMaterialRepository materialRepository,
        INoteRepository noteRepository,
        IOptionService optionService,
        IAiService ai,
        IUserContext user,
        ILimitLock limitLock,
        IContentLimits? limits = null)
    {
        _questionRepository = questionRepository;
        _materialRepository = materialRepository;
        _noteRepository = noteRepository;
        _optionService = optionService;
        _ai = ai;
        _user = user;
        _limitLock = limitLock;
        _limits = limits ?? ContentLimits.Free();
    }

    public async Task<List<QuestionResponseDto>?> GenerateAsync(
        Guid materialId,
        QuestionGenerateDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Count is < 1 or > AiLimits.MaxGeneratedQuestions)
            throw new BadRequestException($"Count must be from 1 to {AiLimits.MaxGeneratedQuestions}");

        var userId = _user.UserId;

        var material = await _materialRepository.GetByIdAsync(materialId, userId);
        if (material is null) return null;

        string content;
        if (dto.NoteId is { } noteId)
        {
            var note = await _noteRepository.GetByIdAsync(noteId, materialId, userId);
            if (note is null) return null;

            content = AiSourceText.FromNote(note);
        }
        else
        {
            content = AiSourceText.FromMaterial(material);
        }

        // Checked before the AI call too, so a full material does not waste a request
        var max = (await _limits.ForUserAsync(userId)).QuestionsPerMaterial;
        var free = (max ?? int.MaxValue) - await _questionRepository.CountActiveByMaterialIdAsync(materialId);
        if (free <= 0)
            throw new LimitExceededException($"Question limit reached: {max} per material");

        var existingTitles = (await _questionRepository.GetActiveQuestionsAsync(materialId, userId))
            .Select(x => x.Title)
            .ToList();

        var prompt = new QuestionGenerationPrompt(
            material.Title,
            content,
            Math.Min(dto.Count, free),
            dto.QuestionType,
            existingTitles);

        bool IsNew(string title) => !existingTitles.Contains(title, StringComparer.OrdinalIgnoreCase);

        var generated = (await _ai.RunAsync(
                prompt, x => x.Any(q => IsNew(q.Title)), "AI could not write questions for this text", cancellationToken))
            .Value
            .Where(x => IsNew(x.Title))
            .ToList();

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Questions, materialId);

        free = (max ?? int.MaxValue) - await _questionRepository.CountActiveByMaterialIdAsync(materialId);
        if (free <= 0)
            throw new LimitExceededException($"Question limit reached: {max} per material");

        var questions = generated.Take(free).Select(x => ToEntity(x, materialId, dto.NoteId, userId)).ToList();

        foreach (var question in questions)
        {
            await _questionRepository.AddQuestionAsync(question);
        }

        await _questionRepository.SaveChangesAsync();
        await limitLock.CommitAsync();

        return questions.Select(q => QuestionMapper.ToDto(q)).ToList();
    }

    // Same mappers and option rules as manual creation
    private Question ToEntity(GeneratedQuestion generated, Guid materialId, Guid? noteId, Guid userId)
    {
        switch (generated.Type)
        {
            case QuestionType.Options:
                var optionsQuestion = QuestionMapper.ToEntityOptionsQuestion(new OptionQuestionCreateDto
                {
                    MaterialId = materialId,
                    NoteId = noteId,
                    Title = generated.Title,
                    QuestionDifficulty = generated.Difficulty,
                    Explanation = generated.Explanation,
                    Options = [],
                }, materialId, userId);

                optionsQuestion.Options = new List<Option>();
                _optionService.SyncOptions(optionsQuestion, generated.Options
                    .Select(x => new OptionUpdateDto { Name = x.Name, IsCorrect = x.IsCorrect })
                    .ToList());

                return optionsQuestion;

            case QuestionType.TrueFalse:
                var trueFalseQuestion = QuestionMapper.ToEntityTrueFalseQuestion(new TrueFalseQuestionCreateDto
                {
                    MaterialId = materialId,
                    NoteId = noteId,
                    Title = generated.Title,
                    QuestionDifficulty = generated.Difficulty,
                    Explanation = generated.Explanation,
                    IsTrue = generated.IsTrue,
                }, materialId, userId);

                _optionService.SetTrueFalseOptions(trueFalseQuestion, generated.IsTrue);

                return trueFalseQuestion;

            default:
                return QuestionMapper.ToEntityOpenQuestion(new OpenQuestionCreateDto
                {
                    MaterialId = materialId,
                    NoteId = noteId,
                    Title = generated.Title,
                    Answer = generated.Answer!,
                    QuestionDifficulty = generated.Difficulty,
                    Explanation = generated.Explanation,
                }, materialId, userId);
        }
    }
}
