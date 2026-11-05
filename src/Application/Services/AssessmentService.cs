using Application.DTOs.Assessment;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;

namespace Application.Services;

public class AssessmentService : IAssessmentService
{
    private const int MinTimerMinutes = 1;
    private const int MaxTimerMinutes = 600;

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

        var assessment = await _repo.GetAssessmentByMaterialIdAsync(assessmentId, _user.UserId);
        if (assessment is null) return null;

        assessment.TimerMinutes = settings.TimerMinutes;
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
}