using Microsoft.Extensions.DependencyInjection;
using StServer.Application.Interfaces;
using StServer.Infrastructure.Repositories;

namespace StServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IAssessmentRepository, AssessmentRepository>();
        services.AddScoped<IAttemptRepository, AttemptRepository>();
        services.AddScoped<ITagRepository, TagRepository>();

        return services;
    }
}