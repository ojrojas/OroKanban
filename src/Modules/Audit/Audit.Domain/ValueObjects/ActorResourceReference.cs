using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Audit.Domain.ValueObjects;

public sealed class ActorReference : ValueObject
{
    public Guid ActorId { get; }
    public string ActorType { get; }
    public string DisplayName { get; }
    public ActorReference(Guid actorId, string actorType, string displayName)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId required");
        ActorId = actorId;
        ActorType = actorType;
        DisplayName = displayName ?? "Unknown";
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return ActorId; yield return ActorType; yield return DisplayName; }
}

public sealed class ResourceReference : ValueObject
{
    public string ResourceType { get; }
    public string ResourceId { get; }
    public ResourceReference(string resourceType, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || resourceType.Length > 100) throw new ArgumentException("ResourceType 1..100");
        if (string.IsNullOrWhiteSpace(resourceId) || resourceId.Length > 200) throw new ArgumentException("ResourceId 1..200");
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return ResourceType; yield return ResourceId; }
}

public sealed class AuditResult : ValueObject
{
    public string Result { get; }
    public string? ErrorCode { get; }
    public AuditResult(string result, string? errorCode = null)
    {
        Result = result;
        ErrorCode = errorCode;
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Result; yield return ErrorCode; }
}
