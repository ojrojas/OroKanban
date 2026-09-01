using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Projects.Domain.Ids;

public sealed record ProjectId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static ProjectId New() => new(Guid.NewGuid());
}

public sealed record WorkItemId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static WorkItemId New() => new(Guid.NewGuid());
}

public sealed record WorkItemDependencyId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static WorkItemDependencyId New() => new(Guid.NewGuid());
}