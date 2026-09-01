namespace Identity.Contracts;

public interface IPermissionCatalog
{
    Task<bool> HasPermissionAsync(IReadOnlyList<string> roles, string permission, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(string role, CancellationToken ct);
}
