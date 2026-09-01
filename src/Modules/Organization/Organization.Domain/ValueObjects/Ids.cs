using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Organization.Domain.ValueObjects;

public sealed record ManagementRelationshipId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static ManagementRelationshipId New() => new(Guid.NewGuid());
}

public sealed record OrganizationUnitId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrganizationUnitId New() => new(Guid.NewGuid());
}

public sealed record ExplicitGrantId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static ExplicitGrantId New() => new(Guid.NewGuid());
}