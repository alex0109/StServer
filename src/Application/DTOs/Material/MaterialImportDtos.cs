using Application.Import;

namespace Application.DTOs.Material;

public class MaterialImportDto
{
    public string? Url { get; set; }
    public bool GenerateQuestions { get; set; }
}

public class MaterialImportResultDto
{
    public required MaterialResponseDto Material { get; init; }
    public int QuestionsCreated { get; init; }
    // Why no questions were made when they were asked for: "quota", "unavailable" or "failed"
    public string? QuestionsSkipped { get; init; }
}

// The 422 answer of a failed import carries this code for the client to translate
public record ArticleImportErrorDto(ArticleImportError Code);
