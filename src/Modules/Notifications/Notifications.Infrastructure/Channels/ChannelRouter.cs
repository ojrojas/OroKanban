using Microsoft.Extensions.Logging;
using Notifications.Domain.Aggregates;

namespace Notifications.Infrastructure.Channels;

public sealed class ChannelRouter : IChannelRouter
{
    private readonly IEnumerable<IChannel> _channels;
    private readonly ILogger<ChannelRouter> _logger;
    public ChannelRouter(IEnumerable<IChannel> channels, ILogger<ChannelRouter> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    public IReadOnlyList<IChannel> Channels => _channels.ToList();

    public async Task FanOutAsync(Notification notification, CancellationToken ct)
    {
        foreach (var channel in _channels)
        {
            try
            {
                var result = await channel.DeliverAsync(notification, ct);
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Channel {Channel} failed for notification {NotificationId} with error {Error}", channel.Channel.Name, notification.Id.Value, result.Error?.Code);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Channel {Channel} threw for notification {NotificationId}", channel.Channel.Name, notification.Id.Value);
            }
        }
    }
}
