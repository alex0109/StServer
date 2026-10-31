using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Application.Admin;
using Application.Ai;
using Application.Email;
using Application.Notifications;
using Application.Review;
using Application.Limits;
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
        AiOptions? aiOptions = null,
        EmailOptions? emailOptions = null,
        NotificationOptions? notificationOptions = null,
        AiPricingOptions? aiPricingOptions = null,
        ReviewOptions? reviewOptions = null,
        ContentLimitsOptions? contentLimitsOptions = null)
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
        services.AddSingleton(contentLimitsOptions ?? new ContentLimitsOptions());
        services.AddScoped<ISubscriptionTierProvider, SubscriptionTierProvider>();
        services.AddScoped<IContentLimits, ContentLimits>();
        services.AddSingleton(emailOptions ?? new EmailOptions());
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<ILibraryAdminService, LibraryAdminService>();
        services.AddSingleton(aiPricingOptions ?? new AiPricingOptions());
        services.AddScoped<IAdminStatsService, AdminStatsService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IBillingCatalog, BillingCatalog>();
        services.AddScoped<IMaterialImportService, MaterialImportService>();
        services.AddScoped<ISubscriptionAdminService, SubscriptionAdminService>();
        services.AddScoped<ILearningSettingsProvider, LearningSettingsProvider>();

        services.AddSingleton(notificationOptions ?? new NotificationOptions());
        services.AddScoped<IStudyActivityService, StudyActivityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationGenerator, NotificationGenerator>();
        services.AddScoped<IFlashcardProgressRebuilder, FlashcardProgressRebuilder>();
        services.AddScoped<IQuestionReviewService, QuestionReviewService>();
        services.AddScoped<IUserTimeZoneProvider, UserTimeZoneProvider>();
        services.AddSingleton(reviewOptions ?? new ReviewOptions());
        services.AddScoped<IReviewService, ReviewService>();

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
        services.AddScoped<IWordLookupService, WordLookupService>();

        return services;
    }
}