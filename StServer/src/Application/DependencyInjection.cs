using StServer.Application.Interfaces;

namespace StServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IMaterialService, IMaterialService>();

        return services;
    }
}