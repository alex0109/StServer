namespace Application.Constants;

public class FlashcardLimits
{
    public const int DefaultSessionSize = 20;
    public const int MaxSessionSize = 200;
    // Words a custom session may be picked from: a whole dictionary at most
    public const int MaxCustomEntryIds = 5000;
}
