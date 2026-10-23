namespace Domain.Utility.Flashcard;

// Values match FSRS ratings, Manual is used for logs that are not real answers (progress reset)
public enum FlashcardRating
{
    Manual = 0,
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4
}
