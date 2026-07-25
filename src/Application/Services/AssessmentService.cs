using Application.DTOs.Assessment;
using Application.Interfaces;
using Application.Mappers;

namespace Application.Services;

public class AssessmentService : IAssessmentService
{
    private readonly IAssessmentRepository _repo;
    private readonly IUserContext _user;
    
    public AssessmentService(IAssessmentRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }
    
    public async Task<Guid> StartAssessment(Guid materialId)
    {
        var existing = await _repo.GetAssessmentByIdAsync(materialId, _user.UserId);

        if (existing is not null)
            return existing.Id;

        var assessment = AssessmentMapper.ToEntity(materialId, _user.UserId);
        await _repo.AddAssessmentAsync(assessment);
        await _repo.SaveChangesAsync();

        return assessment.Id;
    }
    
    public async Task<AssessmentResponseDto?> GetAssessment(Guid assessmentId)
    {
        var result = await _repo.GetAssessmentByIdAsync(assessmentId, _user.UserId);

        if(result is null)
            return null;
        
        return AssessmentMapper.ToDto(result); 
    }
}