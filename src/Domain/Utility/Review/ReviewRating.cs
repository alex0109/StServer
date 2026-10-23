namespace Domain.Utility.Review;

// FSRS rating, shared by flashcard reviews and question answers.
// Values match FSRS, Manual is used for logs that are not real answers (progress reset)
public enum ReviewRating
{
    Manual = 0,
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4
}
