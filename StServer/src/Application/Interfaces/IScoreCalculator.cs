using StServer.Application.DTOs.Result;

namespace StServer.Application.Interfaces;

public interface IScoreCalculator
{
    double MaxScore { get; }
    
    double Calculate(IEnumerable<ResultResponseDto> results);
}