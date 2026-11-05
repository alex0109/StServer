using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.DTOs.RichTextDocument;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class LibraryImportEndpointsTests : IClassFixture<ApiFactory>
{
    private const string ImportUrl = "/api/admin/library/items/import";

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LibraryImportEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // In the shape the editor uses, like the files the admin panel sends
    private static string Payload(string title, string questionTitle = "Який артикль?") => $$"""
        {
          "authorNickname": "Studytracker",
          "snapshot": {
            "title": "{{title}}",
            "type": "practice",
            "link": null,
            "content": {"type":"doc","content":[
              {"type":"heading","attrs":{"level":3},"content":[{"type":"text","text":"Вступ"}]},
              {"type":"paragraph","content":[{"type":"text","text":"Текст ","marks":[{"type":"bold"}]},{"type":"text","text":"код","marks":[{"type":"code"}]}]},
              {"type":"orderedList","attrs":{"start":1},"content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Пункт"}]}]}]},
              {"type":"codeBlock","content":[{"type":"text","text":"print(1)\n  print(2)"}]},
              {"type":"blockquote","content":[{"type":"paragraph","content":[{"type":"text","text":"Цитата"}]}]},
              {"type":"horizontalRule"}
            ]},
            "notes": [{"key":1,"title":"Артиклі","type":"text","textContent":{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"der, die, das"}]}]},"drawingContent":null}],
            "questions": [
              {"title":"{{questionTitle}}","answer":null,"questionType":"Options","questionDifficulty":"Easy","explanation":"Бо так.","noteKey":null,"entryKey":null,
               "options":[{"name":"der","position":0,"isCorrect":true},{"name":"die","position":1,"isCorrect":false}]},
              {"title":"Це правда?","answer":null,"questionType":"TrueFalse","questionDifficulty":"Medium","explanation":null,"noteKey":null,"entryKey":null,
               "options":[{"name":"True","position":0,"isCorrect":true},{"name":"False","position":1,"isCorrect":false}]},
              {"title":"Перекладіть","answer":"der Tisch","questionType":"Open","questionDifficulty":"Hard","explanation":null,"noteKey":null,"entryKey":null,"options":[]}
            ],
            "dictionaries": [{"title":"Слова","description":"Базові","entries":[
              {"key":1,"term":"der Tisch","translation":"стіл","transcription":"[tɪʃ]","partOfSpeech":"Noun","grammarInfo":"мн. die Tische","example":"Der Tisch ist neu.","exampleTranslation":"Стіл новий.","note":null,"synonyms":[],"position":0},
              {"key":2,"term":"gut","translation":"добрий","transcription":null,"partOfSpeech":null,"grammarInfo":null,"example":null,"exampleTranslation":null,"note":null,"synonyms":[],"position":1}
            ]}]
          }
        }
        """;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private async Task<JsonElement> ImportAsync(string body)
    {
        var response = await _factory.CreateAdminClient().PostAsync(ImportUrl, Json(body), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task ImportedMaterial_IsPublic_AndCopiesIntoAUsersAccount()
    {
        var title = $"Import {Guid.NewGuid():N}"[..20];

        var result = await ImportAsync(Payload(title));
        result.GetProperty("created").GetBoolean().ShouldBeTrue();

        var item = result.GetProperty("item");
        item.GetProperty("notesCount").GetInt32().ShouldBe(1);
        item.GetProperty("questionsCount").GetInt32().ShouldBe(3);
        item.GetProperty("wordsCount").GetInt32().ShouldBe(2);
        var id = item.GetProperty("id").GetGuid();

        // A guest reads it
        var read = await _factory.CreateClientWithoutAuth().GetFromJsonAsync<JsonElement>($"/api/library/{id}", Ct);
        read.GetProperty("title").GetString().ShouldBe(title);
        read.GetProperty("content").GetProperty("content").GetProperty("content").GetArrayLength().ShouldBe(6);

        // A user adds it, and it lands as an ordinary material
        var userId = Guid.NewGuid();
        var added = await _factory.CreateAuthenticatedClient(userId).PostAsync($"/api/library/{id}/import", null, Ct);
        added.StatusCode.ShouldBe(HttpStatusCode.OK, await added.Content.ReadAsStringAsync(Ct));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await db.Materials
            .Include(x => x.Notes)
            .Include(x => x.Questions).ThenInclude(x => x.Options)
            .Include(x => x.Dictionaries).ThenInclude(x => x.Entries)
            .SingleAsync(x => x.UserId == userId && x.ImportedFromLibraryItemId == id, Ct);

        material.Notes.Count.ShouldBe(1);
        material.Questions.Count.ShouldBe(3);
        material.Questions.Single(x => x.Title == "Це правда?").Options.Count.ShouldBe(2);
        material.Dictionaries.Single().Entries.Count.ShouldBe(2);

        var stored = RichTextJson.Parse(material.Content);
        stored!.Content.Select(x => x.Type).ShouldBe(["heading", "paragraph", "orderedList", "codeBlock", "blockquote", "horizontalRule"]);
        stored.Content[3].Content![0].Text.ShouldBe("print(1)\n  print(2)");
        stored.Content[1].Content![1].Marks![0].Type.ShouldBe("code");
    }

    [Fact]
    public async Task SameTitleAgain_UpdatesInsteadOfMakingATwin()
    {
        var title = $"Twice {Guid.NewGuid():N}"[..20];

        var first = await ImportAsync(Payload(title));
        var second = await ImportAsync(Payload(title, "Новий текст питання"));

        second.GetProperty("created").GetBoolean().ShouldBeFalse();
        second.GetProperty("item").GetProperty("id").GetGuid().ShouldBe(first.GetProperty("item").GetProperty("id").GetGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.LibraryItems.Where(x => x.Title == title).ToListAsync(Ct);

        items.Count.ShouldBe(1);
        items[0].Version.ShouldBe(2);
        items[0].Snapshot.RootElement.GetRawText().ShouldContain("Новий текст питання");
    }

    [Fact]
    public async Task ABrokenFile_IsRefusedWithTheReason_AndNothingIsPublished()
    {
        var title = $"Broken {Guid.NewGuid():N}"[..20];

        var response = await _factory.CreateAdminClient().PostAsync(ImportUrl, Json(Payload(title, new string('я', 101))), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Question 1 title is longer than 100");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.LibraryItems.AnyAsync(x => x.Title == title, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task OnlyAnAdminWithTheSecondFactor_CanImport()
    {
        var body = Payload("Not allowed");

        (await _factory.CreateAuthenticatedClient().PostAsync(ImportUrl, Json(body), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateAdminClient(secondFactor: false).PostAsync(ImportUrl, Json(body), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClientWithoutAuth().PostAsync(ImportUrl, Json(body), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }
}
