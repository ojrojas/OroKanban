using Microsoft.AspNetCore.Authorization;

namespace Api.Features.Authorization;

/// <summary>
/// Named authorization policies mapping constitution roles to endpoint groups.
/// Role membership is authoritative from the identity server (claims).
/// </summary>
public static class AuthPolicies
{
    public const string Admin = nameof(Admin);
    public const string Manager = nameof(Manager);
    public const string Authenticated = nameof(Authenticated);

    public static AuthorizationOptions Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Authenticated, policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy(Admin, policy =>
            policy.RequireRole("Administrator", "SuperAdmin"));

        options.AddPolicy(Manager, policy =>
            policy.RequireRole("Manager", "Administrator", "SuperAdmin"));

        return options;
    }
}
