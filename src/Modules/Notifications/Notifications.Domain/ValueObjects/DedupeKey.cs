using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Notifications.Domain.ValueObjects;

public sealed class DedupeKey : ValueObject
{
    public Guid SourceEventId { get; }
    public Guid RecipientId { get; }
    public int ChannelId { get; }

    public DedupeKey(Guid sourceEventId, Guid recipientId, int channelId)
    {
        if (sourceEventId == Guid.Empty) throw new ArgumentException("SourceEventId required", nameof(sourceEventId));
        if (recipientId == Guid.Empty) throw new ArgumentException("RecipientId required", nameof(recipientId));
        SourceEventId = sourceEventId;
        RecipientId = recipientId;
        ChannelId = channelId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceEventId;
        yield return RecipientId;
        yield return ChannelId;
    }
}
