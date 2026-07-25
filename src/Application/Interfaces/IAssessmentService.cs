using StServer.Application.DTOs.Assessment;

namespace StServer.Application.Interfaces;

public interface IAssessmentService
{
    Task<Guid> StartAssessment(Guid id);
    Task<AssessmentResponseDto?> GetAssessment(Guid id);
}