using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StServer.Infrastructure.Data;
using StServer.Api.Endpoints;

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

String? connectionString = builder.Configuration.GetConnectionString("MyDB");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    await next();

    sw.Stop();
    Console.WriteLine($"{context.Request.Path} took {sw.ElapsedMilliseconds} ms");
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapMaterialEndpoints();
app.MapQuestionEndpoints();
app.MapAssessmentEndpoints();

app.Run();