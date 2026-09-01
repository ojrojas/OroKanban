using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.Extensions.Logging;
using Notifications.Domain.Aggregates;

namespace Notifications.Infrastructure.Channels;

public sealed class EmailChannel : IChannel
{
    private readonly ILogger<EmailChannel> _logger;
    public bool ShouldFail { get; set; }

    public EmailChannel(ILogger<EmailChannel> logger)
    {
        _logger = logger;
    }

    public Domain.Enumerations.Channel Channel => Domain.Enumerations.Channel.Email;

    public Task<Result> DeliverAsync(Notification notification, CancellationToken ct)
    {
        if (ShouldFail)
        {
            _logger.LogError("Email channel failed for notification {NotificationId} type {Type}", notification.Id.Value, notification.NotificationType.Name);
            return Task.FromResult(Result.Failure(Error.Failure("Email.Failed", "Email channel failed")));
        }
        _logger.LogInformation("Would send email to {RecipientId} title={Title} link={Link}", notification.RecipientId, notification.Title, notification.Link);
        return Task.FromResult(Result.Success());
    }
}
