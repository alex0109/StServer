namespace Application.Interfaces;

public interface IUserContext
{
    Guid UserId { get; }

    // Null for anonymous requests, for endpoints that work with and without a user
    Guid? CurrentUserId { get; }
}