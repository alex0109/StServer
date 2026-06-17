using StServer.Application.Interfaces;
using StServer.Infrastructure.Authentication;
using StServer.Infrastructure.Repositories;

namespace StServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IMaterialRepository, MaterialRepository>();

        return services;
    }
}