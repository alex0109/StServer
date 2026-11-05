using Application.DTOs.Assessment;

namespace Application.Interfaces;

public interface IAssessmentService
{
    Task<Guid> StartAssessment(Guid id);
    Task<AssessmentResponseDto?> GetAssessment(Guid id);
    Task<AssessmentResponseDto?> UpdateSettings(Guid assessmentId, AssessmentSettingsDto settings);
}