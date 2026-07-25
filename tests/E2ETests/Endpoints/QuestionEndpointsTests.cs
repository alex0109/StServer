using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs.Material;
using Application.DTOs.Question;
using Domain.Utility.Material;
using Domain.Utility.Question;
using Shouldly;
using System.Text.Json.Serialization;
using Xunit;

namespace E2ETests.Endpoints;

public class QuestionEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public QuestionEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }
    
    private async Task<MaterialResponseDto> CreateMaterial()
    {
        var dto = new MaterialCreateDto
        {
            Title = "Question material",
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
    
    private async Task<QuestionResponseDto> CreateOpenQuestion(
        Guid materialId)
    {
        var dto = new OpenQuestionCreateDto
        {
            MaterialId = materialId,
            Title = "What is DDD?",
            Answer = "Domain Driven Design",
            Explanation = "Design approach",
            QuestionDifficulty = QuestionDifficulty.Medium
        };
        
        var response = await _client.PostAsJsonAsync(
            $"/api/materials/{materialId}/questions/open",
            dto,
            JsonOptions);
        
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        var question = await response.Content
            .ReadFromJsonAsync<QuestionResponseDto>(JsonOptions);
        
        question.ShouldNotBeNull();

        return question!;
    }

    [Fact]
    public async Task CreateOpenQuestion_ReturnsCreatedQuestion()
    {
        var material = await CreateMaterial();

        var question = await CreateOpenQuestion(material.Id);
        
        question.Title.ShouldBe("What is DDD?");
        question.Answer.ShouldBe("Domain Driven Design");
        question.MaterialId.ShouldBe(material.Id);
        question.QuestionType.ShouldBe(QuestionType.Open);
        question.QuestionDifficulty.ShouldBe(QuestionDifficulty.Medium);
        question.IsActive.ShouldBeTrue();
    }
    
    [Fact]
    public async Task GetQuestion_ReturnsQuestion()
    {
        var material = await CreateMaterial();

        var created = await CreateOpenQuestion(material.Id);

        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{created.Id}");
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var question = await response.Content
            .ReadFromJsonAsync<QuestionResponseDto>(JsonOptions);
        
        question.ShouldNotBeNull();

        question!.Id.ShouldBe(created.Id);
        question.Title.ShouldBe(created.Title);
    }

    [Fact]
    public async Task GetQuestion_WhenNotExists_ReturnsNotFound()
    {
        var material = await CreateMaterial();
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{Guid.NewGuid()}");
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllQuestions_ReturnsQuestions()
    {
        var material = await CreateMaterial();

        await CreateOpenQuestion(material.Id);
        await CreateOpenQuestion(material.Id);
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var questions = await response.Content
            .ReadFromJsonAsync<List<QuestionResponseDto>>(JsonOptions);
        
        questions.ShouldNotBeNull();

        questions.Count.ShouldBe(2);
    }
    
    [Fact]
    public async Task GetReducedQuestions_ReturnsReducedQuestions()
    {
        var material = await CreateMaterial();

        await CreateOpenQuestion(material.Id);
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/reduced");
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var questions = await response.Content
            .ReadFromJsonAsync<List<QuestionReducedDto>>(JsonOptions);

        questions.ShouldNotBeNull();

        questions.Count.ShouldBe(1);

        questions[0].Title.ShouldBe("What is DDD?");
    }
    
    [Fact]
    public async Task UpdateQuestion_ChangesQuestion()
    {
        var material = await CreateMaterial();

        var question = await CreateOpenQuestion(material.Id);
        
        var updateDto = new QuestionUpdateDto
        {
            Title = "Updated title",
            Answer = "Updated answer",
            Options = null
        };
        
        var response = await _client.PatchAsJsonAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}",
            updateDto,
            JsonOptions);
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var updated = await response.Content
            .ReadFromJsonAsync<QuestionResponseDto>(JsonOptions);
        
        updated.ShouldNotBeNull();

        updated!.Title.ShouldBe("Updated title");
        updated.Answer.ShouldBe("Updated answer");
    }

    [Fact]
    public async Task DeleteQuestion_SoftDeletesQuestion()
    {
        var material = await CreateMaterial();

        var question = await CreateOpenQuestion(material.Id);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}");

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleted = await getResponse.Content
            .ReadFromJsonAsync<QuestionResponseDto>(JsonOptions);

        deleted.ShouldNotBeNull();

        deleted!.Id.ShouldBe(question.Id);
        deleted.MaterialId.ShouldBe(material.Id);
        deleted.IsActive.ShouldBeFalse();
    }
    
    [Fact]
    public async Task GetQuestion_FromAnotherUser_ReturnsNotFound()
    {
        var material = await CreateMaterial();

        var question = await CreateOpenQuestion(material.Id);

        TestAuthHandler.SetUser(Guid.NewGuid());
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}");
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}