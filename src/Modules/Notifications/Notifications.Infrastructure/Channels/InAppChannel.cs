using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Domain.Aggregates;

namespace Notifications.Infrastructure.Channels;

public sealed class InAppChannel : IChannel
{
    public Domain.Enumerations.Channel Channel => Domain.Enumerations.Channel.InApp;
    public Task<Result> DeliverAsync(Notification notification, CancellationToken ct) => Task.FromResult(Result.Success());
}
