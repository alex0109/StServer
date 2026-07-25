using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Material;
using Domain.Utility.Material;
using Shouldly;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace E2ETests.Endpoints;

public class MaterialEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _noAuthClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MaterialEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
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
            JsonOptions);
        
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(JsonOptions);
        
        created.ShouldNotBeNull();

        created!.Title.ShouldBe(createDto.Title);
        created.Type.ShouldBe(createDto.Type);
        created.Status.ShouldBe(createDto.Status);
        
        var getResponse = await _client.GetAsync(
            $"/api/materials/{created.Id}");
        
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(JsonOptions);

        fetched.ShouldNotBeNull();

        fetched!.Id.ShouldBe(created.Id);
        fetched.Title.ShouldBe(createDto.Title);
    }

    [Fact]
    public async Task GetMaterial_WhenNotExists_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/materials/{Guid.NewGuid()}");
        
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
            JsonOptions);
        
        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(JsonOptions);
        
        var response = await _client.GetAsync("/api/materials");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var materials = await response.Content
            .ReadFromJsonAsync<List<MaterialResponseDto>>(JsonOptions);
        
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
            JsonOptions);

        var created = await createResponse.Content
            .ReadFromJsonAsync<MaterialResponseDto>(JsonOptions);
        
        TestAuthHandler.CurrentUserId = Guid.NewGuid();
        
        var response = await _client.GetAsync(
            $"/api/materials/{created!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task GetMaterials_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _noAuthClient.GetAsync("/api/materials");
        
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}