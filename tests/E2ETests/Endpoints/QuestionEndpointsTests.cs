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
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public QuestionEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
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
            $"/api/materials/{material.Id}/questions/{created.Id}",
            TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var question = await response.Content
            .ReadFromJsonAsync<QuestionResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        question.ShouldNotBeNull();

        question!.Id.ShouldBe(created.Id);
        question.Title.ShouldBe(created.Title);
    }

    [Fact]
    public async Task GetQuestion_WhenNotExists_ReturnsNotFound()
    {
        var material = await CreateMaterial();
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllQuestions_ReturnsQuestions()
    {
        var material = await CreateMaterial();

        await CreateOpenQuestion(material.Id);
        await CreateOpenQuestion(material.Id);
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var questions = await response.Content
            .ReadFromJsonAsync<List<QuestionResponseDto>>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        questions.ShouldNotBeNull();

        questions.Count.ShouldBe(2);
    }
    
    [Fact]
    public async Task GetReducedQuestions_ReturnsReducedQuestions()
    {
        var material = await CreateMaterial();

        await CreateOpenQuestion(material.Id);
        
        var response = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/reduced",
            TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var questions = await response.Content
            .ReadFromJsonAsync<List<QuestionReducedDto>>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);

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
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var updated = await response.Content
            .ReadFromJsonAsync<QuestionResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);
        
        updated.ShouldNotBeNull();

        updated!.Title.ShouldBe("Updated title");
        updated.Answer.ShouldBe("Updated answer");
    }

    [Fact]
    public async Task UpdateOptions_ASavedQuestionGetsNewOptions_UpToSix()
    {
        var material = await CreateMaterial();
        var created = await _client.PostAsJsonAsync(
            $"/api/materials/{material.Id}/questions/options",
            new OptionQuestionCreateDto
            {
                MaterialId = material.Id,
                Title = "Which are planets?",
                QuestionDifficulty = QuestionDifficulty.Easy,
                Options = new[] { "Mars", "Venus", "Moon", "Sun" }.Select((name, i) => new Application.DTOs.Option.OptionCreateDto { Name = name, IsCorrect = i < 2 }).ToList(),
            },
            JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var question = (await created.Content.ReadFromJsonAsync<QuestionResponseDto>(JsonOptions))!;

        // The existing four keep their ids, the new ones come without
        async Task<HttpResponseMessage> SaveWith(params string[] newNames) =>
            await _client.PatchAsJsonAsync(
                $"/api/materials/{material.Id}/questions/{question.Id}",
                new QuestionUpdateDto
                {
                    Options = question.Options
                        .Select(x => new Application.DTOs.Option.OptionUpdateDto { Id = x.Id, Name = x.Name, IsCorrect = x.IsCorrect })
                        .Concat(newNames.Select(name => new Application.DTOs.Option.OptionUpdateDto { Name = name, IsCorrect = false }))
                        .ToList(),
                },
                JsonOptions);

        var fifth = await SaveWith("Jupiter");
        fifth.StatusCode.ShouldBe(HttpStatusCode.OK);
        question = (await fifth.Content.ReadFromJsonAsync<QuestionResponseDto>(JsonOptions))!;
        question.Options.Select(x => x.Name).ShouldBe(["Mars", "Venus", "Moon", "Sun", "Jupiter"]);

        (await SaveWith("Saturn")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SaveWith("Saturn", "Pluto")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var saved = await _client.GetFromJsonAsync<QuestionResponseDto>(
            $"/api/materials/{material.Id}/questions/{question.Id}", JsonOptions);
        saved!.Options.Count.ShouldBe(6);
    }

    [Fact]
    public async Task DeleteQuestion_SoftDeletesQuestion()
    {
        var material = await CreateMaterial();

        var question = await CreateOpenQuestion(material.Id);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}",
            TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}",
            TestContext.Current.CancellationToken);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleted = await getResponse.Content
            .ReadFromJsonAsync<QuestionResponseDto>(
                JsonOptions,
                cancellationToken: TestContext.Current.CancellationToken);

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

        var otherUserClient = _factory.CreateAuthenticatedClient();
        
        var response = await otherUserClient.GetAsync(
            $"/api/materials/{material.Id}/questions/{question.Id}",
            TestContext.Current.CancellationToken);
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}