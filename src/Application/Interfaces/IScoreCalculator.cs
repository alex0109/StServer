using Application.DTOs.Result;

namespace Application.Interfaces;

public interface IScoreCalculator
{
    double MaxScore { get; }
    
    double Calculate(IEnumerable<ResultResponseDto> results);
}