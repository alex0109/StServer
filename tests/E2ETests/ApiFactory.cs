using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace E2ETests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_db.GetConnectionString()));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
    
    public HttpClient CreateClientWithoutAuth()
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();

                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "NoAuth";
                        options.DefaultChallengeScheme = "NoAuth";
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler.NoAuthHandler>(
                        "NoAuth",
                        _ => { });
            });
        }).CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        
        Environment.SetEnvironmentVariable("DATABASE_URL", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("SUPABASE_URL", "https://adpkgbqqwpzafwijluwx.supabase.co/auth/v1");

        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _db.DisposeAsync();
}