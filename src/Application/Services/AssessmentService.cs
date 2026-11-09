using Application.DTOs.Assessment;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Utility.Question;

namespace Application.Services;

public class AssessmentService : IAssessmentService
{
    private const int MinTimerMinutes = 1;
    private const int MaxTimerMinutes = 600;
    private const int MinQuestionCount = 1;
    // Far above the questions a material can have, only keeps the number sane
    private const int MaxQuestionCount = 1000;

    private readonly IAssessmentRepository _repo;
    private readonly IUserContext _user;
    
    public AssessmentService(IAssessmentRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }
    
    public async Task<Guid> StartAssessment(Guid materialId)
    {
        var existing = await _repo.GetAssessmentByMaterialIdAsync(materialId, _user.UserId);
        
        if (existing is not null)
            return existing.Id;

        var assessment = AssessmentMapper.ToEntity(materialId, _user.UserId);
        await _repo.AddAssessmentAsync(assessment);
        await _repo.SaveChangesAsync();

        return assessment.Id;
    }
    
    public async Task<AssessmentResponseDto?> UpdateSettings(Guid assessmentId, AssessmentSettingsDto settings)
    {
        if (settings.TimerMinutes is < MinTimerMinutes or > MaxTimerMinutes)
            throw new BadRequestException($"The timer must be between {MinTimerMinutes} and {MaxTimerMinutes} minutes");

        if (settings.QuestionCount is < MinQuestionCount or > MaxQuestionCount)
            throw new BadRequestException($"The number of questions must be between {MinQuestionCount} and {MaxQuestionCount}");

        var difficulties = NormalizeDifficulties(settings.Difficulties);

        var assessment = await _repo.GetAssessmentByMaterialIdAsync(assessmentId, _user.UserId);
        if (assessment is null) return null;

        assessment.TimerMinutes = settings.TimerMinutes;
        assessment.Difficulties = difficulties;
        assessment.QuestionCount = settings.QuestionCount;
        await _repo.SaveChangesAsync();

        return AssessmentMapper.ToDto(assessment);
    }

    public async Task<AssessmentResponseDto?> GetAssessment(Guid assessmentId)
    {
        var result = await _repo.GetAssessmentByMaterialIdAsync(assessmentId, _user.UserId);

        if(result is null)
            return null;
        
        return AssessmentMapper.ToDto(result); 
    }

    // Stored empty when every level is chosen, so "all" stays all if a level is ever added
    private static List<QuestionDifficulty> NormalizeDifficulties(IReadOnlyCollection<QuestionDifficulty>? chosen)
    {
        if (chosen is null) return [];

        if (chosen.Any(d => !Enum.IsDefined(d)))
            throw new BadRequestException("Unknown question difficulty");

        var distinct = chosen.Distinct().Order().ToList();

        return distinct.Count == Enum.GetValues<QuestionDifficulty>().Length ? [] : distinct;
    }
}
