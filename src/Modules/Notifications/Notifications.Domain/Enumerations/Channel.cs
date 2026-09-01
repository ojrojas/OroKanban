using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Notifications.Domain.Enumerations;

public sealed class Channel : Enumeration<Channel>
{
    public static readonly Channel InApp = new(1, nameof(InApp));
    public static readonly Channel Email = new(2, nameof(Email));

    private Channel(int id, string name) : base(id, name) { }
}
