using Application.DTOs.Profile;

namespace Application.Interfaces;

public interface IUserProfileService
{
    Task<ProfileDto> GetAsync();

    Task<ProfileDto> UpdateAsync(ProfileUpdateDto dto);

    // The nickname of the first library request becomes the profile's, a set one stays
    Task RememberNicknameAsync(Guid userId, string nickname);

    // Resolved code AI explanations are written in for the user
    Task<string> GetExplanationLanguageAsync(Guid userId);
}
