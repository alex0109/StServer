using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Infrastructure.Account;
using Infrastructure.Admin;
using Infrastructure.Auth;
using Infrastructure.Billing;
using Infrastructure.Ai;
using Infrastructure.Caching;
using Infrastructure.Import;
using Infrastructure.Observability;
using Application.Email;
using Infrastructure.Email;
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
        services.AddScoped<IQuestionReviewRepository, QuestionReviewRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IMaterialShareRepository, MaterialShareRepository>();
        services.AddScoped<IAiUsageRepository, AiUsageRepository>();
        services.AddScoped<ILimitsRepository, LimitsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IFeedbackLogRepository, FeedbackLogRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<IAdminStatsRepository, AdminStatsRepository>();
        services.AddScoped<IAdminAudit, AdminAudit>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();

        // Stripe stays off until its keys and prices are in the environment
        services.AddHttpClient(StripeBillingProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.stripe.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton(sp => StripeSettings.FromEnvironment(sp.GetRequiredService<EmailOptions>().AppUrl));
        services.AddSingleton<IBillingProvider, StripeBillingProvider>();

        // Article import: one long-lived client whose sockets connect only to checked public addresses
        services.AddSingleton<IAddressResolver, DnsAddressResolver>();
        services.AddSingleton(sp =>
        {
            var resolver = sp.GetRequiredService<IAddressResolver>();
            var site = sp.GetRequiredService<EmailOptions>().AppUrl.TrimEnd('/');
            return new SafeHttpFetcher(resolver, SafeHttpFetcher.CreateHandler(resolver), $"StudytrackerBot/1.0 (+{site})");
        });
        services.AddScoped<IArticleImporter, ArticleImporter>();

        // Monitoring sources for the admin panel; each one is off until its variables are set
        foreach (var name in new[] { PrometheusApi.HttpClientName, GrafanaApi.HttpClientName, SentryApi.HttpClientName, GoogleAnalyticsApi.HttpClientName })
        {
            services.AddHttpClient(name, client => client.Timeout = TimeSpan.FromSeconds(10));
        }
        services.AddSingleton<IPrometheusApi, PrometheusApi>();
        services.AddSingleton<IGrafanaApi, GrafanaApi>();
        services.AddSingleton<ISentryApi, SentryApi>();
        services.AddSingleton<IGoogleAnalyticsApi, GoogleAnalyticsApi>();
        services.AddScoped<IObservabilityService, ObservabilityService>();
        services.AddScoped<IAdminAlerts, AdminAlertService>();

        services.AddHttpClient(ResendEmailSender.HttpClientName);

        services.AddHttpClient(SupabaseAuthAdmin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<IAuthUserAdmin, SupabaseAuthAdmin>();
        services.AddSingleton<IEmailSender, ResendEmailSender>();

        // Scoped like the DbContext, so the lock transaction covers the repositories of the request
        services.AddScoped<ILimitLock, PostgresLimitLock>();

        services.AddMemoryCache();
        services.AddSingleton<IUserReadCache, UserReadCache>();
        services.AddSingleton<IDeletedAccounts, DeletedAccounts>();

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