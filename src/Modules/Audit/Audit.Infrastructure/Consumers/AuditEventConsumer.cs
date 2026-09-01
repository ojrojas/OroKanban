using BuildingBlocks.EventBus.Abstractions;
using Audit.Infrastructure.Persistence.Configurations;
using Audit.Domain.Enumerations;
using Audit.Domain.ValueObjects;
using Audit.Domain.Aggregates;
using Audit.Domain.Ids;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Consumers;

public sealed class AuditEventConsumer : IIntegrationEventHandler<IntegrationEvent>
{
    private readonly Audit.Infrastructure.Persistence.AuditDbContext _auditContext;
    private readonly Audit.Domain.Services.IAuditMaskingPolicy _maskingPolicy;

    public AuditEventConsumer(Audit.Infrastructure.Persistence.AuditDbContext auditContext, Audit.Domain.Services.IAuditMaskingPolicy maskingPolicy)
    {
        _auditContext = auditContext;
        _maskingPolicy = maskingPolicy;
    }

    public async Task HandleAsync(IntegrationEvent @event, CancellationToken ct)
    {
        var exists = await _auditContext.AuditConsumedEvents.AnyAsync(e => e.EventId == @event.Id, ct);
        if (exists) return;
        try
        {
            await using var tx = await _auditContext.Database.BeginTransactionAsync(ct);
            await _auditContext.AuditConsumedEvents.AddAsync(new AuditConsumedEvent { EventId = @event.Id, ProcessedAt = DateTime.UtcNow, Action = @event.GetType().Name, CorrelationId = Guid.NewGuid() }, ct);
            var rawSnapshot = new BeforeAfterSnapshot("{}", "{}");
            var masked = _maskingPolicy.Mask(rawSnapshot);
            var entry = new AuditEntry(
                AuditEntryId.New(),
                DateTime.UtcNow,
                new ActorReference(Guid.NewGuid(), "User", "Test"),
                AuditAction.DocumentUploaded,
                "Document",
                Guid.NewGuid().ToString(),
                null,
                Guid.NewGuid(),
                new AuditResult("Success"),
                Guid.NewGuid(),
                null,
                null,
                null,
                masked,
                null,
                null);
            await _auditContext.AuditEntries.AddAsync(entry, ct);
            await _auditContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
        {
            // Concurrent duplicate — treat as success
        }
    }
}
