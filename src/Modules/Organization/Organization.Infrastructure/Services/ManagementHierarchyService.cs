using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using Organization.Contracts;
using Organization.Infrastructure.Persistence;

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

        // Fallback to synthetic hierarchy from IdentityServer users when local table empty (source of truth = oroidentityserver, Organization brings level 0 = superior)
        // Synthetic: admin → manager1,manager2; manager1 → operator1,operator2 (both operators under manager1)
        var activeRels = await _db.ManagementRelationships
            .Where(r => r.TenantId == tenantId && (r.ValidTo == null || r.ValidTo >= DateTime.UtcNow) && (r.ValidFrom == null || r.ValidFrom <= DateTime.UtcNow))
            .Select(r => new { r.ManagerId, r.SubordinateId })
            .ToListAsync(ct);

        if (activeRels.Count == 0)
        {
            // Synthetic fallback — matches /api/organization/units synthetic tree
            var synthetic = new[]
            {
                new { ManagerId = Guid.Parse("01a065aa-9020-70a9-a1e0-b844196713c7"), SubordinateId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c") }, // admin → manager1
                new { ManagerId = Guid.Parse("01a065aa-9020-70a9-a1e0-b844196713c7"), SubordinateId = Guid.Parse("01a06800-5db7-753b-a61b-7876ca7b5828") }, // admin → manager2
                new { ManagerId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c"), SubordinateId = Guid.Parse("01a06801-1345-7b00-a052-83b7be137228") }, // manager1 → operator1
                new { ManagerId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c"), SubordinateId = Guid.Parse("01a06801-a457-7323-8316-40726313b076") }, // manager1 → operator2
            };
            // Only use synthetic for the known tenant; otherwise keep empty
            if (tenantId == Guid.Parse("01a065aa-8cf7-7ba0-b3f4-613aa9979fca"))
                activeRels = synthetic.ToList();
        }

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

        if (activeRels.Count == 0 && tenantId == Guid.Parse("01a065aa-8cf7-7ba0-b3f4-613aa9979fca"))
        {
            activeRels = new[]
            {
                new { ManagerId = Guid.Parse("01a065aa-9020-70a9-a1e0-b844196713c7"), SubordinateId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c") },
                new { ManagerId = Guid.Parse("01a065aa-9020-70a9-a1e0-b844196713c7"), SubordinateId = Guid.Parse("01a06800-5db7-753b-a61b-7876ca7b5828") },
                new { ManagerId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c"), SubordinateId = Guid.Parse("01a06801-1345-7b00-a052-83b7be137228") },
                new { ManagerId = Guid.Parse("01a067ff-bab8-7529-aff7-c6ce7fb7363c"), SubordinateId = Guid.Parse("01a06801-a457-7323-8316-40726313b076") },
            }.ToList();
        }

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