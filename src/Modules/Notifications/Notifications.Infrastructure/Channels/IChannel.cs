using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Domain.Aggregates;

namespace Notifications.Infrastructure.Channels;

public interface IChannel
{
    Domain.Enumerations.Channel Channel { get; }
    Task<Result> DeliverAsync(Notification notification, CancellationToken ct);
}

public interface IChannelRouter
{
    IReadOnlyList<IChannel> Channels { get; }
    Task FanOutAsync(Notification notification, CancellationToken ct);
}
