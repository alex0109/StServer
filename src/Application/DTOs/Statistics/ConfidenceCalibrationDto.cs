using Domain.Utility.Result;

namespace Application.DTOs.Statistics;

public class ConfidenceCalibrationDto
{
    public ConfidenceLevel ConfidenceLevel { get; set; }
    public int AnswersCount { get; set; }
    public double ActualAccuracy { get; set; }

    // ActualAccuracy - ExpectedAccuracyForThisConfidenceLevel
    // positive = user underestimates themselves, negative = overconfident
    public double CalibrationGap { get; set; }
}
