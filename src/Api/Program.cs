using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Infrastructure.Data;
using Api.Endpoints;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using System.Security.Claims;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Api.Common;
using Api.Middleware;
using Application;
using Application.Admin;
using Application.Ai;
using Application.Email;
using Application.Notifications;
using Application.Interfaces;
using Application.Jobs;
using Application.Schedulers;
using Infrastructure;
using Infrastructure.Ai;

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
// The admin panel: listed accounts with a second factor only
builder.Services.AddAdminAccess();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserContext, UserContext>();

builder.Services.AddInfrastructure();
builder.Services.AddApplication(
    builder.Configuration
        .GetSection(FlashcardSchedulerOptions.SectionName)
        .Get<FlashcardSchedulerOptions>(),
    builder.Configuration
        .GetSection(AiOptions.SectionName)
        .Get<AiOptions>(),
    builder.Configuration
        .GetSection(EmailOptions.SectionName)
        .Get<EmailOptions>(),
    builder.Configuration
        .GetSection(NotificationOptions.SectionName)
        .Get<NotificationOptions>(),
    builder.Configuration
        .GetSection(AiPricingOptions.SectionName)
        .Get<AiPricingOptions>());

// Models and endpoints come from configuration, API keys only from environment variables
builder.Services.AddAiProviders(options =>
    builder.Configuration.GetSection(AiProvidersOptions.SectionName).Bind(options));

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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Separate window for every user, requests without a user are limited by IP
    options.AddPolicy("api", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = userId is not null
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 100,
        });
    });

    // Feedback can be sent without an account, so it has its own small window
    options.AddPolicy("feedback", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = userId is not null
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(10),
            PermitLimit = 3,
        });
    });
});

// Lists of materials, words and questions are JSON that compresses several times over
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Shows at start which AI providers can answer, a missing key or model is the usual reason for "AI unavailable"
app.Logger.LogInformation(
    "AI providers: {Providers}",
    string.Join(", ", app.Services.GetServices<IAiProvider>()
        .Select(x => $"{x.Name} {(x.IsConfigured ? "on" : "off (needs API key and model)")}")));

app.UseResponseCompression();

app.UseExceptionHandler();

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// A user's cached reads are dropped by any change they make, before the response leaves,
// so the next request already sees it
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
    {
        context.Response.OnStarting(() =>
        {
            var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (context.Response.StatusCode < 400 && Guid.TryParse(sub, out var userId))
            {
                context.RequestServices.GetRequiredService<IUserReadCache>().Invalidate(userId);
            }

            return Task.CompletedTask;
        });
    }

    await next();
});

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
app.MapStatisticsEndpoints();
app.MapNoteEndpoints();
app.MapDictionaryEndpoints();
app.MapFlashcardEndpoints();
app.MapMaterialShareEndpoints();
app.MapLimitEndpoints();
app.MapAiEndpoints();
app.MapFeedbackEndpoints();
app.MapNotificationEndpoints();
app.MapLibraryEndpoints();
app.MapAdminEndpoints();

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

        recurringJobManager.AddOrUpdate<FlashcardRescheduleJob>(
            "reschedule-flashcards",
            x => x.RescheduleOutdated(),
            Cron.Daily);

        // Run once on start as well, so cards move to a newly configured algorithm right away
        recurringJobManager.Trigger("reschedule-flashcards");

        recurringJobManager.AddOrUpdate<AiUsageCleanupJob>(
            "cleanup-ai-usage",
            x => x.Cleanup(),
            Cron.Hourly);

        // Reminders and digests: every user at their own local hour
        recurringJobManager.AddOrUpdate<NotificationJob>(
            "notifications",
            x => x.Run(),
            Cron.Hourly(5));

        recurringJobManager.AddOrUpdate<FlashcardSessionCleanupJob>(
            "cleanup-flashcard-sessions",
            x => x.Cleanup(),
            Cron.Daily(3));

        recurringJobManager.AddOrUpdate<DeletedQuestionsPurgeJob>(
            "purge-deleted-questions",
            x => x.Purge(),
            Cron.Daily(3, 30));

        recurringJobManager.AddOrUpdate<NotificationCleanupJob>(
            "cleanup-notifications",
            x => x.Cleanup(),
            Cron.Daily(4));

        recurringJobManager.AddOrUpdate<FeedbackLogCleanupJob>(
            "cleanup-feedback-log",
            x => x.Cleanup(),
            Cron.Daily(4, 30));

        recurringJobManager.AddOrUpdate<AiUsageRollupJob>(
            "rollup-ai-usage",
            x => x.RollUp(),
            Cron.Monthly(1, 4));
    }
}

app.Run();

public partial class Program { }