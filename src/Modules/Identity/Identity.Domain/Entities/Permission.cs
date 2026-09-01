using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.ValueObjects;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

public sealed class Permission : Entity<Guid>
{
    public PermissionCode Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Category { get; private set; } = default!;

    private Permission() { }

    public Permission(Guid id, PermissionCode code, string description, string category) : base(id)
    {
        Code = code;
        Description = description;
        Category = category;
    }
}

public sealed class RolePermission
{
    public string Role { get; set; } = default!;
    public string PermissionCode { get; set; } = default!;
}
