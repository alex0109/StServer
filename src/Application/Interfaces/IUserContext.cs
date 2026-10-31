namespace Application.Interfaces;

public interface IUserContext
{
    Guid UserId { get; }

    // Null for anonymous requests, for endpoints that work with and without a user
    Guid? CurrentUserId { get; }

    // Email of the signed in account, from the token
    string? Email { get; }

    // Address of the caller, for limits on guests (behind a proxy it needs forwarded headers)
    string? ClientIp { get; }

    string? UserAgent { get; }

    // Claims of the signed in session, null when the token has none
    string? Claim(string type);

    // Every claim of a type: a JSON array in the token becomes one claim per element
    IReadOnlyList<string> Claims(string type) => Claim(type) is { } value ? [value] : [];
}