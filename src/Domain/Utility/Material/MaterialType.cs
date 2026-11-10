namespace Domain.Utility.Material;

// Values are stored as numbers: 2 was "summary", it moved to notes and must not be reused.
// New kinds only ever get new numbers
public enum MaterialType
{
    article = 0,
    video = 1,
    practice = 3,
    test = 4,
    lecture = 5,
    book = 6,
    course = 7,
    podcast = 8,
    document = 9,
    // A subject's topic or an exam question learnt from several sources
    topic = 10
}
