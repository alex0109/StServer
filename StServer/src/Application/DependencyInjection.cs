using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<IMaterialTagService, MaterialTagService>();

        return services;
    }
}