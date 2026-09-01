using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Notifications.Domain.Enumerations;

public sealed class DeliveryState : Enumeration<DeliveryState>
{
    public static readonly DeliveryState Pending = new(1, nameof(Pending));
    public static readonly DeliveryState Delivered = new(2, nameof(Delivered));
    public static readonly DeliveryState Failed = new(3, nameof(Failed));
    public static readonly DeliveryState SkippedByPreference = new(4, nameof(SkippedByPreference));
    public static readonly DeliveryState SkippedByPolicy = new(5, nameof(SkippedByPolicy));

    private DeliveryState(int id, string name) : base(id, name) { }
}
