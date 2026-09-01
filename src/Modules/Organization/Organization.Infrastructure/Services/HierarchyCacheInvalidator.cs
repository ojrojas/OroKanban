using BuildingBlocks.EventBus.Abstractions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using Organization.Contracts.Events;

namespace Organization.Infrastructure.Services;

public sealed class HierarchyCacheInvalidator : IIntegrationEventHandler<OrganizationHierarchyChangedIntegrationEvent>
{
    private readonly IDistributedCache? _cache;
    private readonly ILogger<HierarchyCacheInvalidator> _logger;

    public HierarchyCacheInvalidator(ILogger<HierarchyCacheInvalidator> logger, IDistributedCache? cache = null)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task HandleAsync(OrganizationHierarchyChangedIntegrationEvent @event, CancellationToken ct)
    {
        if (_cache == null) return;

        // Delete affected manager's subtree and isIn keys — plus ancestors' keys
        var keysToDelete = new[]
        {
            $"hierarchy:{@event.TenantId}:{@event.ActorUserId}:subtree",
            $"hierarchy:{@event.TenantId}:{@event.TargetUserId}:subtree",
        };

        foreach (var key in keysToDelete)
        {
            try { await _cache.RemoveAsync(key, ct); _logger.LogInformation("Invalidated cache key {Key} due to hierarchy change {@Event}", key, @event); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to invalidate cache key {Key}", key); }
        }
    }
}