using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Repositories;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IAssessmentRepository, AssessmentRepository>();
        services.AddScoped<IAttemptRepository, AttemptRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();

        return services;
    }
}