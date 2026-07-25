using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Infrastructure.Data;
using Api.Endpoints;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.RateLimiting;
using Api.Common;
using Application;
using Application.Interfaces;
using Application.Jobs;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    DotNetEnv.Env.Load();
}

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("Jwt:Secret missing");

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

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

// Аутентифікуємо користувача по Berear токену
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? throw new Exception("SUPABASE_URL missing"),
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Дозволяє отримати HttpContext з будь якого місця в коді
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserContext, UserContext>();

// Підключаємо DI(scope) з Infrastructure та Application
builder.Services.AddInfrastructure();
builder.Services.AddApplication();

// Дозволяємо штуку яка може конвертувати назви enum в їх строкову версію замість цифр
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()
    );
});

String? connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? throw new Exception("DATABASE_URL missing");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Підключаємо hangfire
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(connectionString);
});

builder.Services.AddHangfireServer();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 100;
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Підключаємо dashboard для моніторингу
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard();
}

// Підключаємо ендпоінти
app.MapMaterialEndpoints();
app.MapQuestionEndpoints();
app.MapAssessmentEndpoints();
app.MapAttemptEndpoints();
app.MapTagEndpoints();

RecurringJob.AddOrUpdate<AttemptCleanupJob>(
    "cleanup-attempts",
    x => x.Cleanup(),
    Cron.Daily);

app.Run();