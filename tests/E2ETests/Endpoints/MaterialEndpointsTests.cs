using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Material;
using Domain.Utility.Material;
using Shouldly;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Constants;
using Application.Limits;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace E2ETests.Endpoints;

public class MaterialEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _noAuthClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MaterialEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        _noAuthClient = factory.CreateClientWithoutAuth();
    }
    
    private async Task<MaterialResponseDto> CreateMaterial()
    {
        var dto = new MaterialCreateDto
        {
            Title = "Test material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };

        var response = await _client.PostAsJsonAsync(
            "/api/materials",
            dto,
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var material = await response.Content
            .ReadFromJsonAsync<MaterialResponseDto>(JsonOptions);

        material.ShouldNotBeNull();

        return material!;
    }

    [Fact]
    public async Task CreateThenGet_ReturnsCreatedMaterial()
    {
        var createDto = new MaterialCreateDto
        {
            Title = "E2E test material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/materials",
            createDto,
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        created.ShouldNotBeNull();

        created!.Title.ShouldBe(createDto.Title);
        created.Type.ShouldBe(createDto.Type);
        created.Status.ShouldBe(createDto.Status);
        
        var getResponse = await _client.GetAsync($"/api/materials/{created.Id}", TestContext.Current.CancellationToken);
        
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);

        fetched.ShouldNotBeNull();

        fetched!.Id.ShouldBe(created.Id);
        fetched.Title.ShouldBe(createDto.Title);
    }
    
    [Fact]
    public async Task CreateMaterial_WhenLimitExceeded_ReturnsConflictWithProblemDetails()
    {
        // The cap is far above real use, so the materials go straight into the database
        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Materials.AddRange(Enumerable.Range(0, ContentLimitDefaults.Everyone.Materials!.Value).Select(i => new Material
            {
                UserId = userId,
                Title = $"Material {i}",
                Type = MaterialType.article,
                Status = MaterialStatus.tolearn,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateAuthenticatedClient(userId).PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "One too many",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem!.Title.ShouldBe("Limit exceeded");
    }

    [Fact]
    public async Task CreateMaterial_NewType_IsStoredAndReturnedByName()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/materials", new StringContent(
            """{"title":"Lecture 3","type":"lecture","status":"tolearn"}""", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"type\":\"lecture\"");
    }

    [Fact]
    public async Task CreateMaterial_OldSummaryNumber_IsRefused()
    {
        var response = await _factory.CreateAuthenticatedClient().PostAsync("/api/materials", new StringContent(
            """{"title":"Old","type":2,"status":"tolearn"}""", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMaterial_WhenNotExists_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/materials/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllMaterials_ReturnsCreatedMaterials()
    {
        var dto = new MaterialCreateDto
        {
            Title = "Material 1",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };
        
        var createResponse = await _client.PostAsJsonAsync(
            "/api/materials",
            dto,
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        
        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        var response = await _client.GetAsync("/api/materials", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var materials = await response.Content
            .ReadFromJsonAsync<List<MaterialResponseDto>>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        materials.ShouldNotBeNull();
        
        materials.ShouldContain(x =>
            x.Id == created!.Id);
    }

    [Fact]
    public async Task GetMaterial_FromAnotherUser_ReturnsNotFound()
    {
        var createDto = new MaterialCreateDto
        {
            Title = "Private material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };
        
        var createResponse = await _client.PostAsJsonAsync(
            "/api/materials",
            createDto,
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        var otherUserClient = _factory.CreateAuthenticatedClient();
        
        var response = await otherUserClient.GetAsync(
            $"/api/materials/{created!.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task GetMaterials_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _noAuthClient.GetAsync("/api/materials", TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}