using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Locks;
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
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IDictionaryRepository, DictionaryRepository>();
        services.AddScoped<IFlashcardRepository, FlashcardRepository>();
        services.AddScoped<IMaterialShareRepository, MaterialShareRepository>();

        // Scoped like the DbContext, so the lock transaction covers the repositories of the request
        services.AddScoped<ILimitLock, PostgresLimitLock>();

        return services;
    }
}