namespace Domain.Utility.Note;

public enum NoteType
{
    text,
    drawing,
    pdfAnnotation,
    // Short retelling of the whole material, at most one per material
    summary,
    // A text note written for a task, which AI can check
    essay
}
