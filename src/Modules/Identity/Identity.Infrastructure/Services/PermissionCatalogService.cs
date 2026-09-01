using Identity.Contracts;
using Identity.Infrastructure.Seed;

namespace Identity.Infrastructure.Services;

public sealed class PermissionCatalogService : IPermissionCatalog
{
    public Task<bool> HasPermissionAsync(IReadOnlyList<string> roles, string permission, CancellationToken ct)
    {
        foreach (var role in roles)
        {
            if (PermissionCatalogSeed.RolePermissions.TryGetValue(role, out var perms) && perms.Contains(permission, StringComparer.OrdinalIgnoreCase))
                return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(string role, CancellationToken ct)
    {
        if (PermissionCatalogSeed.RolePermissions.TryGetValue(role, out var perms))
            return Task.FromResult<IReadOnlyList<string>>(perms);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
