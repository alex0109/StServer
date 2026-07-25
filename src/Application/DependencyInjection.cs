using Microsoft.Extensions.DependencyInjection;
using Application.Evaluators;
using Application.Interfaces;
using Application.ScoreCalculators;
using Application.Services;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IAttemptService, AttemptService>();
        services.AddScoped<IOptionService, OptionService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IAnswerEvaluationService, AnswerEvaluationService>();
        services.AddScoped<IAnswerNormalizer, AnswerNormalizer>();
        services.AddScoped<IAnswerValidationStep, ExactValidationStep>();
        services.AddScoped<IAnswerValidationStep, FuzzyValidationStep>();
        services.AddScoped<IAverageScoreCalculator, AverageScoreCalculator>();
        services.AddScoped<IDifficultyWeightProvider, ExponentialDifficultyWeightProvider>();
        services.AddScoped<INonLinearDifficultyScoreCalculator, NonLinearDifficultyScoreCalculator>();
        services.AddScoped<ISuccessBonusScoreCalculator>(sp => new SuccessBonusScoreCalculator(bonusFactor: 0.5));
        services.AddScoped<IAttemptScoringService, AttemptScoringService>();

        return services;
    }
}