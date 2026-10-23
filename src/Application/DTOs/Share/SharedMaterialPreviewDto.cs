using Domain.Utility.Material;

namespace Application.DTOs.Share;

public class SharedMaterialPreviewDto
{
    public required string Token { get; set; }
    public required string Title { get; set; }
    public MaterialType Type { get; set; }
    public int NotesCount { get; set; }
    public int QuestionsCount { get; set; }
    public int DictionariesCount { get; set; }
    public int WordsCount { get; set; }
    // Filled only for signed in users
    public bool IsOwner { get; set; }
    public bool IsAlreadyImported { get; set; }
    public DateTime SharedAt { get; set; }
}
