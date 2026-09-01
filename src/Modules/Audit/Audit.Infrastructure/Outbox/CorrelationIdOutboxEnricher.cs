namespace Audit.Infrastructure.Outbox;

public sealed class CorrelationIdOutboxEnricher
{
    public Guid Enrich(Guid? correlationId, Guid fallback)
    {
        return correlationId ?? fallback;
    }
}
