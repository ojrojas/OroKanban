using Microsoft.EntityFrameworkCore;

using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Services;

public sealed class HierarchyInspector : IHierarchyInspector
{
    private readonly ProjectsDbContext _db;
    public HierarchyInspector(ProjectsDbContext db) => _db = db;

    public async Task<IReadOnlySet<Guid>> GetAncestorIdsAsync(Guid workItemId, CancellationToken ct)
    {
        // Simple CTE via raw SQL; fallback to iterative loop if needed
        var ancestors = new HashSet<Guid>();
        var currentId = workItemId;
        for (int i = 0; i < 100; i++)
        {
            var parent = await _db.WorkItems.Where(w => w.Id == new Projects.Domain.Ids.WorkItemId(currentId)).Select(w => w.ParentId).FirstOrDefaultAsync(ct);
            if (parent is null) break;
            // ParentId is WorkItemId? stored as Guid? in WorkItem.ParentId (nullable Guid?). Need conversion
            // WorkItem ParentId is Guid? but mapping is via Id.Value? For simplicity we store Guid? via ParentId property which EF maps as Guid? column.
            // So query above returns object? Let's handle via workaround: query entity.
            var entity = await _db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(currentId), ct);
            if (entity?.ParentId is null) break;
            var pid = entity.ParentId.Value;
            if (!ancestors.Add(pid)) break;
            currentId = pid;
        }
        return ancestors;
    }

    public async Task<IReadOnlySet<Guid>> GetDescendantIdsAsync(Guid workItemId, CancellationToken ct)
    {
        // BFS over adjacency list (acceptable for project-scoped graph <1k)
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(workItemId);
        var visited = new HashSet<Guid> { workItemId };
        for (int iter = 0; iter < 10000 && queue.Count > 0; iter++)
        {
            var parent = queue.Dequeue();
            var children = await _db.WorkItems.Where(w => w.ParentId.HasValue && w.ParentId.Value == parent).Select(w => w.Id.Value).ToListAsync(ct);
            foreach (var c in children)
            {
                if (result.Add(c) && visited.Add(c))
                    queue.Enqueue(c);
            }
        }
        return result;
    }

    public async Task<Guid?> GetRootEpicIdAsync(Guid workItemId, CancellationToken ct)
    {
        // Walk ancestors until root, find Epic (TypeId==1)
        var current = await _db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(workItemId), ct);
        if (current is null) return null;
        Guid? epic = null;
        // if self is epic, remember
        if (current.TypeId == 1) epic = current.Id.Value;
        for (int i = 0; i < 100; i++)
        {
            if (current?.ParentId is null) break;
            var parent = await _db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(current.ParentId.Value), ct);
            if (parent is null) break;
            if (parent.TypeId == 1) epic = parent.Id.Value;
            current = parent;
        }
        return epic;
    }
}