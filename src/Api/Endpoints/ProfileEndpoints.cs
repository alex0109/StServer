using Application.DTOs.Profile;
using Application.Interfaces;

namespace Api.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var profileGroup = app.MapGroup("api/profile")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        profileGroup.MapGet("/", GetProfile);
        // Only the sent fields change
        profileGroup.MapPatch("/", UpdateProfile);
        // Replays the review history with the current settings, so a new retention moves due dates now
        profileGroup.MapPost("/reschedule", Reschedule);

        static async Task<IResult> GetProfile(IUserProfileService service)
        {
            return TypedResults.Ok(await service.GetAsync());
        }

        static async Task<IResult> UpdateProfile(ProfileUpdateDto dto, IUserProfileService service)
        {
            return TypedResults.Ok(await service.UpdateAsync(dto));
        }

        static async Task<IResult> Reschedule(
            IUserContext user,
            IFlashcardProgressRebuilder words,
            IQuestionReviewService questions)
        {
            var wordCount = await words.RescheduleUserAsync(user.UserId);
            var questionCount = await questions.RescheduleUserAsync(user.UserId);

            return TypedResults.Ok(new { Words = wordCount, Questions = questionCount });
        }
    }
}
