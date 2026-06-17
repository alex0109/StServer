using StServer.Application.Interfaces;
using StServer.Application.Services;

namespace StServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IMaterialService, MaterialService>();

        return services;
    }
}