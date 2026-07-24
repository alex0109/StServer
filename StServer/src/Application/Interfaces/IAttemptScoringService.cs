using StServer.Application.DTOs.Attempt;

namespace StServer.Application.Interfaces;

public interface IAttemptScoringService
{
    void AverageScoreAttempt(AttemptResponseDto attempt);
    void NonLinearScoreAttempt(AttemptResponseDto attempt);
    void SuccessBonusScoreAttempt(AttemptResponseDto attempt);
}