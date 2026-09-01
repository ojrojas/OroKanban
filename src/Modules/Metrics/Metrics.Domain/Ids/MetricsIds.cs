using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Metrics.Domain.Ids;

public sealed record MetricDefinitionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static MetricDefinitionId New() => new(Guid.NewGuid());
}

public sealed record MetricValueId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static MetricValueId New() => new(Guid.NewGuid());
}

public sealed record MilestoneId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static MilestoneId New() => new(Guid.NewGuid());
}
