using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Ai;
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
        services.AddScoped<IAiUsageRepository, AiUsageRepository>();
        services.AddScoped<ILimitsRepository, LimitsRepository>();

        // Scoped like the DbContext, so the lock transaction covers the repositories of the request
        services.AddScoped<ILimitLock, PostgresLimitLock>();

        return services;
    }

    // Every provider is registered, AiOptions.ProviderOrder decides which ones are used and in what order.
    // A new company is one more IAiProvider class here, nothing else changes
    public static IServiceCollection AddAiProviders(this IServiceCollection services, Action<AiProvidersOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton(AiProviderKeys.FromEnvironment());

        // AiClient owns timeouts, and pooled connections are renewed so DNS changes of providers are picked up
        foreach (var name in new[] { AnthropicAiProvider.ProviderName, OpenAiProvider.ProviderName, GeminiAiProvider.ProviderName })
        {
            services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                });
        }

        services.AddSingleton<IAiProvider, AnthropicAiProvider>();
        services.AddSingleton<IAiProvider, OpenAiProvider>();
        services.AddSingleton<IAiProvider, GeminiAiProvider>();

        return services;
    }
}