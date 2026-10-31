namespace Domain.Utility.Flashcard;

// What a flashcard session takes its cards from
public enum FlashcardSessionScope
{
    // One dictionary, the session's DictionaryId
    Dictionary = 0,
    // Everything due today across all of the user's dictionaries
    Today = 1
}
