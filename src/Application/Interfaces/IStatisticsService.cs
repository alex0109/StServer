using Application.DTOs.Statistics;

namespace Application.Interfaces;

public interface IStatisticsService
{
    Task<StatisticsOverviewDto> GetOverviewAsync();
    Task<List<AttemptTrendPointDto>> GetAttemptTrendAsync(int days = 30);
    Task<List<DifficultyStatDto>> GetDifficultyBreakdownAsync();
    Task<List<ConfidenceCalibrationDto>> GetConfidenceCalibrationAsync();
    Task<List<TagPerformanceDto>> GetTagPerformanceAsync();
    Task<StudyStreakDto> GetStudyStreakAsync();
}
