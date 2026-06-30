using StServer.Application.DTOs.Assessment;
using StServer.Application.Interfaces;
using StServer.Application.Mappers;

namespace StServer.Application.Services;

public class AssessmentService : IAssessmentService
{
    private readonly IAssessmentRepository _repo;
    private readonly IUserContext _user;
    
    public AssessmentService(IAssessmentRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }
    
    public async Task<Guid> StartAssessment(Guid id)
    {
        var assessment = AssessmentMapper.ToEntity(id);
        
        await _repo.AddAssessmentAsync(assessment);

        await _repo.SaveChangesAsync();

        return assessment.Id;
    }
    
    public async Task<AssessmentResponseDto?> GetAssessment(Guid id)
    {
        var result = await _repo.GetAssessmentByIdAsync(id, _user.UserId);

        if(result is null)
            return null;
        
        return AssessmentMapper.ToDto(result); 
    }
}