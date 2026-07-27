using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using Infrastructure.Data;
using Api.Endpoints;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.RateLimiting;
using Api.Common;
using Api.Middleware;
using Application;
using Application.Interfaces;
using Application.Jobs;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    DotNetEnv.Env.Load();
    
    Console.WriteLine(Environment.GetEnvironmentVariable("JWKS_URL"));
}

JsonWebKeySet jwks;

if (builder.Environment.EnvironmentName == "Testing")
{
    jwks = new JsonWebKeySet();
}
else
{
    var jwksUrl = Environment.GetEnvironmentVariable("JWKS_URL") ?? throw new Exception("JWKS_URL missing");
    using var httpClient = new HttpClient();
    var jwksJson = await httpClient.GetStringAsync(jwksUrl);
    jwks = new JsonWebKeySet(jwksJson);
}

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? throw new Exception("SUPABASE_URL missing");

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins!)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserContext, UserContext>();

builder.Services.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

String? connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? throw new Exception("DATABASE_URL missing");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(config =>
    {
        config.UsePostgreSqlStorage(connectionString);
    });

    builder.Services.AddHangfireServer();
}

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 100;
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    Status = "OK",
    Name = "StServer",
    Environment = app.Environment.EnvironmentName
}));
app.MapMaterialEndpoints();
app.MapQuestionEndpoints();
app.MapAssessmentEndpoints();
app.MapAttemptEndpoints();
app.MapTagEndpoints();

if (!app.Environment.IsEnvironment("Testing"))
{
    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard();
    }

    using (var scope = app.Services.CreateScope())
    {
        var recurringJobManager =
            scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<AttemptCleanupJob>(
            "cleanup-attempts",
            x => x.Cleanup(),
            Cron.Daily);
    }
}

app.Run();

public partial class Program { }