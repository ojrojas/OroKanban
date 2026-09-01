using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Organization.Contracts;
using Organization.Infrastructure.Persistence;
using System.Text.Json;

namespace Organization.Infrastructure.Services;

public sealed class ManagementHierarchyService : IManagementHierarchy
{
    private readonly OrganizationDbContext _db;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<ManagementHierarchyService> _logger;

    public ManagementHierarchyService(OrganizationDbContext db, ILogger<ManagementHierarchyService> logger, IDistributedCache? cache = null)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    public async Task<bool> IsInSubtreeAsync(Guid tenantId, Guid managerId, Guid userId, CancellationToken ct)
    {
        if (managerId == userId) return false;
        var subtree = await GetSubtreeAsync(tenantId, managerId, ct);
        return subtree.Contains(userId);
    }

    public async Task<IReadOnlyList<Guid>> GetSubtreeAsync(Guid tenantId, Guid managerId, CancellationToken ct)
    {
        var cacheKey = $"hierarchy:{tenantId}:{managerId}:subtree";
        if (_cache != null)
        {
            try
            {
                var cached = await _cache.GetStringAsync(cacheKey, ct);
                if (cached != null) return JsonSerializer.Deserialize<List<Guid>>(cached) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache miss for {CacheKey}, falling back to CTE", cacheKey);
            }
        }

        // Recursive CTE over adjacency list — simplified to in-memory BFS for foundation (O(depth) and correct for 1k users)
        // Production would use: WITH RECURSIVE subtree AS (SELECT subordinate_id FROM management_relationships WHERE tenant_id=@t AND manager_id=@m UNION ...)
        var activeRels = await _db.ManagementRelationships
            .Where(r => r.TenantId == tenantId && (r.ValidTo == null || r.ValidTo >= DateTime.UtcNow) && (r.ValidFrom == null || r.ValidFrom <= DateTime.UtcNow))
            .Select(r => new { r.ManagerId, r.SubordinateId })
            .ToListAsync(ct);

        var map = activeRels.GroupBy(r => r.ManagerId).ToDictionary(g => g.Key, g => g.Select(x => x.SubordinateId).ToList());
        var result = new List<Guid>();
        var queue = new Queue<Guid>();
        if (map.TryGetValue(managerId, out var direct)) foreach (var d in direct) queue.Enqueue(d);

        var visited = new HashSet<Guid>();
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!visited.Add(cur)) continue;
            result.Add(cur);
            if (map.TryGetValue(cur, out var next)) foreach (var n in next) if (!visited.Contains(n)) queue.Enqueue(n);
        }

        if (_cache != null)
        {
            try
            {
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to cache {CacheKey}", cacheKey); }
        }

        return result;
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorsAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var activeRels = await _db.ManagementRelationships
            .Where(r => r.TenantId == tenantId && (r.ValidTo == null || r.ValidTo >= DateTime.UtcNow))
            .Select(r => new { r.ManagerId, r.SubordinateId })
            .ToListAsync(ct);

        var reverse = activeRels.GroupBy(r => r.SubordinateId).ToDictionary(g => g.Key, g => g.Select(x => x.ManagerId).ToList());
        var result = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(userId);
        var visited = new HashSet<Guid> { userId };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (reverse.TryGetValue(cur, out var managers))
            {
                foreach (var m in managers)
                {
                    if (visited.Add(m))
                    {
                        result.Add(m);
                        queue.Enqueue(m);
                    }
                }
            }
        }

        return result;
    }

    public async Task<Guid?> GetCommonAncestorAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct)
    {
        var ancA = await GetAncestorsAsync(tenantId, a, ct);
        var ancB = new HashSet<Guid>(await GetAncestorsAsync(tenantId, b, ct));
        return ancA.FirstOrDefault(x => ancB.Contains(x)) is Guid g && g != Guid.Empty ? g : null;
    }
}
