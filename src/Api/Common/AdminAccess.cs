using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Api.Common;

// Who may use /api/admin: a user id from ADMIN_USER_IDS, signed in with a second factor.
// Supabase writes the assurance level into the token: "aal2" only after a TOTP code was verified
public static class AdminAccess
{
    public const string Policy = "Admin";
    public const string AssuranceClaim = "aal";
    public const string SecondFactor = "aal2";
    public const string SessionClaim = "session_id";

    public static IServiceCollection AddAdminAccess(this IServiceCollection services)
    {
        var allowlist = AdminAllowlist.FromEnvironment();
        services.AddSingleton(allowlist);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    allowlist.Contains(UserId(context.User)) && HasSecondFactor(context.User)));
        });

        // Refused admin requests also email the owner
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AdminDeniedAlertHandler>();

        return services;
    }

    // Account ids from ADMIN_USER_IDS, comma separated. Empty means nobody is admin
    public sealed class AdminAllowlist
    {
        private readonly HashSet<Guid> _ids;

        private AdminAllowlist(HashSet<Guid> ids) => _ids = ids;

        public static AdminAllowlist FromEnvironment() => new(
            (Environment.GetEnvironmentVariable("ADMIN_USER_IDS") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToHashSet());

        public bool Contains(Guid? userId) => userId is Guid id && _ids.Contains(id);
    }

    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public static bool HasSecondFactor(ClaimsPrincipal user) =>
        user.FindFirst(AssuranceClaim)?.Value == SecondFactor;

    private sealed class AdminDeniedAlertHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public async Task HandleAsync(
            RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
        {
            if (!result.Succeeded && context.Request.Path.StartsWithSegments("/api/admin"))
            {
                var reason = !result.Forbidden
                    ? "немає входу"
                    : HasSecondFactor(context.User)
                        ? "акаунта немає в списку адмінів"
                        : "вхід без другого фактора (2FA)";

                // Anonymous calls are bots knocking: only signed-in accounts are worth an email
                if (result.Forbidden)
                {
                    await context.RequestServices.GetRequiredService<IAdminAlerts>().AccessDeniedAsync(reason);
                }
            }

            await _default.HandleAsync(next, context, policy, result);
        }
    }
}
