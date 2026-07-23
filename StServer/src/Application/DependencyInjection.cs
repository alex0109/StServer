using Microsoft.Extensions.DependencyInjection;
using StServer.Application.Evaluators;
using StServer.Application.Interfaces;
using StServer.Application.Services;

namespace StServer.Application;

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

        return services;
    }
}