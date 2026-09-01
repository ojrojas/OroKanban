using Projects.Domain.Enumerations;
using Projects.Domain.Services;

namespace Projects.Infrastructure.Services;

public sealed class DependencyCycleDetector : IDependencyCycleDetector
{
    public bool HasCycle(IReadOnlyList<(Guid DependentId, Guid PrincipalId, int TypeId)> existingEdges, (Guid DependentId, Guid PrincipalId, int TypeId) candidate)
    {
        // RelatedTo excluded
        if (candidate.TypeId == DependencyType.RelatedTo.Id) return false;

        var edges = existingEdges.Where(e => e.TypeId != DependencyType.RelatedTo.Id).ToList();
        edges.Add(candidate);

        // adjacency: dependent -> principal
        var graph = edges.GroupBy(e => e.DependentId).ToDictionary(g => g.Key, g => g.Select(x => x.PrincipalId).ToList());
        var allNodes = edges.SelectMany(e => new[] { e.DependentId, e.PrincipalId }).Distinct().ToHashSet();

        var visited = new HashSet<Guid>();
        var inStack = new HashSet<Guid>();

        bool Dfs(Guid node)
        {
            visited.Add(node);
            inStack.Add(node);
            if (graph.TryGetValue(node, out var neighbors))
            {
                foreach (var nb in neighbors)
                {
                    if (!visited.Contains(nb))
                    {
                        if (Dfs(nb)) return true;
                    }
                    else if (inStack.Contains(nb))
                        return true;
                }
            }
            inStack.Remove(node);
            return false;
        }

        foreach (var n in allNodes)
            if (!visited.Contains(n) && Dfs(n))
                return true;
        return false;
    }
}