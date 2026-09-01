using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Audit.Domain.Ids;

public sealed record AuditEntryId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static AuditEntryId New() => new(Guid.NewGuid());
}
