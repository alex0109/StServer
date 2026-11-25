using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Material;
using Application.DTOs.Note;
using Application.DTOs.Question;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Question;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class NoteEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NoteEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> CreateMaterialAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "Biology",
            Type = MaterialType.article,
            Status = MaterialStatus.inprocess,
        }, JsonOptions, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<MaterialResponseDto>(JsonOptions, Ct))!.Id;
    }

    private static async Task<NoteResponseDto> CreateNoteAsync(HttpClient client, Guid materialId, NoteType type)
    {
        var response = await client.PostAsJsonAsync($"/api/materials/{materialId}/notes",
            new NoteCreateDto { Title = $"{type} note", Type = type }, JsonOptions, Ct);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<NoteResponseDto>(JsonOptions, Ct))!;
    }

    private static async Task<QuestionResponseDto> CreateQuestionAsync(HttpClient client, Guid materialId, Guid noteId)
    {
        var response = await client.PostAsJsonAsync($"/api/materials/{materialId}/questions/open", new OpenQuestionCreateDto
        {
            MaterialId = materialId,
            NoteId = noteId,
            Title = "What do mitochondria do?",
            Answer = "Make energy for the cell",
            QuestionDifficulty = QuestionDifficulty.Medium,
        }, JsonOptions, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<QuestionResponseDto>(JsonOptions, Ct))!;
    }

    [Theory]
    [InlineData(NoteType.text)]
    [InlineData(NoteType.drawing)]
    [InlineData(NoteType.essay)]
    public async Task Delete_RemovesTheNoteWithItsQuestions(NoteType type)
    {
        var client = _factory.CreateAuthenticatedClient();
        var materialId = await CreateMaterialAsync(client);
        var note = await CreateNoteAsync(client, materialId, type);
        var other = await CreateNoteAsync(client, materialId, NoteType.text);
        var question = await CreateQuestionAsync(client, materialId, note.Id);
        var otherQuestion = await CreateQuestionAsync(client, materialId, other.Id);

        var deleted = await client.DeleteAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The rest of the material stays as it was
        var notes = await client.GetFromJsonAsync<List<NoteResponseDto>>($"/api/materials/{materialId}/notes", JsonOptions, Ct);
        notes!.Select(n => n.Id).ShouldBe([other.Id]);

        var questions = await client.GetFromJsonAsync<List<QuestionResponseDto>>($"/api/materials/{materialId}/questions", JsonOptions, Ct);
        questions!.Select(q => q.Id).ShouldBe([otherQuestion.Id]);
        questions.ShouldNotContain(q => q.Id == question.Id);
    }

    [Fact]
    public async Task Delete_SomeoneElsesNote_IsNotFoundAndKeepsIt()
    {
        var owner = _factory.CreateAuthenticatedClient();
        var stranger = _factory.CreateAuthenticatedClient();
        var materialId = await CreateMaterialAsync(owner);
        var note = await CreateNoteAsync(owner, materialId, NoteType.drawing);

        var response = await stranger.DeleteAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.GetAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_TwiceIsNotFound()
    {
        var client = _factory.CreateAuthenticatedClient();
        var materialId = await CreateMaterialAsync(client);
        var note = await CreateNoteAsync(client, materialId, NoteType.essay);

        (await client.DeleteAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/materials/{materialId}/notes/{note.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
