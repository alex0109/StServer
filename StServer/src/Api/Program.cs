using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StServer.Infrastructure.Data;
using StServer.Api.Endpoints;
using System.Text.Json.Serialization;
using StServer.Api.Common;
using StServer.Application;
using StServer.Application.Interfaces;
using StServer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new Exception("Jwt:Secret missing");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
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
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = "authenticated"
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

String? connectionString = builder.Configuration.GetConnectionString("MyDB");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Підключаємо ендпоінти
app.MapMaterialEndpoints();
app.MapQuestionEndpoints();
app.MapAssessmentEndpoints();
app.MapAttemptEndpoints();

app.Run();