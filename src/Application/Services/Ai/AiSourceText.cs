using Application.Ai.Text;
using Application.Constants;
using Application.Exceptions;
using Domain.Entities;
using Domain.Utility.Note;

namespace Application.Services.Ai;

// Text the model works from: a text note or the material content, never anything outside the material
internal static class AiSourceText
{
    public static bool HasText(Note note) => note.Type is NoteType.text or NoteType.summary;

    public static string FromNote(Note note)
    {
        if (!HasText(note))
            throw new BadRequestException("Only text notes can be used for generation");

        return Require(RichTextConverter.ToPlainText(note.TextContent));
    }

    public static string FromMaterial(Material material) =>
        Require(RichTextConverter.ToPlainText(material.Content));

    private static string Require(string text)
    {
        if (text.Length < AiLimits.MinSourceLength)
            throw new BadRequestException("Not enough text to generate from, add more content first");

        return text;
    }
}
