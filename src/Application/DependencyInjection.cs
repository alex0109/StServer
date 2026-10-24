using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Application.Ai;
using Application.Evaluators;
using Application.Interfaces;
using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Application.ScoreCalculators;
using Application.Services;
using Application.Services.Ai;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        FlashcardSchedulerOptions? flashcardSchedulerOptions = null,
        AiOptions? aiOptions = null)
    {
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IAttemptService, AttemptService>();
        services.AddScoped<IOptionService, OptionService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IAnswerEvaluationService, AnswerEvaluationService>();
        services.AddScoped<IAnswerNormalizer, AnswerNormalizer>();
        services.AddScoped<IAnswerValidationStep, ExactValidationStep>();
        services.AddScoped<IAnswerValidationStep, FuzzyValidationStep>();
        services.AddScoped<IAverageScoreCalculator, AverageScoreCalculator>();
        services.AddScoped<IDifficultyWeightProvider, ExponentialDifficultyWeightProvider>();
        services.AddScoped<INonLinearDifficultyScoreCalculator, NonLinearDifficultyScoreCalculator>();
        services.AddScoped<ISuccessBonusScoreCalculator>(sp => new SuccessBonusScoreCalculator(bonusFactor: 0.5));
        services.AddScoped<IAttemptScoringService, AttemptScoringService>();
        services.AddScoped<IReviewRatingResolver, ConfidenceReviewRatingResolver>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<IMaterialShareService, MaterialShareService>();
        services.AddScoped<ILimitsService, LimitsService>();
        services.AddScoped<IFlashcardProgressRebuilder, FlashcardProgressRebuilder>();

        // Every algorithm is registered, configuration picks the current one
        services.AddSingleton(flashcardSchedulerOptions ?? new FlashcardSchedulerOptions());
        services.AddSingleton<IFlashcardScheduler, BasicFlashcardScheduler>();
        services.AddSingleton<IFlashcardScheduler, FsrsFlashcardScheduler>();
        services.AddSingleton<IFlashcardSchedulerProvider, FlashcardSchedulerProvider>();

        // AI rules live here, providers are registered by Infrastructure
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(aiOptions ?? new AiOptions());
        services.AddScoped<IAiClient, AiClient>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IAiUsageService, AiUsageService>();
        services.AddScoped<IAnswerSemanticGrader, AiAnswerGrader>();
        services.AddScoped<IQuestionGenerationService, QuestionGenerationService>();
        services.AddScoped<IDictionaryGenerationService, DictionaryGenerationService>();
        services.AddScoped<ISummaryGenerationService, SummaryGenerationService>();
        services.AddScoped<IMistakeExplanationService, MistakeExplanationService>();

        return services;
    }
}