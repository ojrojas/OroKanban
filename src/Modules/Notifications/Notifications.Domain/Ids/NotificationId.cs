using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Notifications.Domain.Ids;

public sealed record NotificationId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
}
