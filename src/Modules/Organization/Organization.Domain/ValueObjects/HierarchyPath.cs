using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Organization.Domain.ValueObjects;

public sealed class HierarchyPath : ValueObject
{
    public IReadOnlyList<string> Segments { get; private set; }

    // EF Core design-time constructor — not used by domain code
    private HierarchyPath() => Segments = Array.Empty<string>();

    public HierarchyPath(IEnumerable<string> segments) => Segments = segments.ToList().AsReadOnly();

    public static HierarchyPath Root(string root) => new([root]);

    public HierarchyPath Append(string segment) => new(Segments.Concat([segment]));

    public string ToPathString() => "/" + string.Join("/", Segments);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var s in Segments) yield return s;
    }
}

public sealed class SubtreeScope : ValueObject
{
    public Guid ManagerId { get; }
    public Guid TenantId { get; }

    public SubtreeScope(Guid managerId, Guid tenantId)
    {
        ManagerId = managerId;
        TenantId = tenantId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ManagerId;
        yield return TenantId;
    }
}

public sealed class GrantScope : ValueObject
{
    public string ResourceType { get; }
    public Guid ResourceId { get; }
    public Guid TenantId { get; }

    public GrantScope(string resourceType, Guid resourceId, Guid tenantId)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        TenantId = tenantId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ResourceType;
        yield return ResourceId;
        yield return TenantId;
    }
}