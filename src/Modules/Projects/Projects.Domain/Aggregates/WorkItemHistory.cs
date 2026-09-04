using BuildingBlocks.Kernel.Domain.Entities;

namespace Projects.Domain.Aggregates;

public sealed class WorkItemHistory : Entity<Guid>
{
    public Guid WorkItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Field { get; private set; } = default!;
    public string? FromJson { get; private set; }
    public string? ToJson { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WorkItemHistory() {}
    public WorkItemHistory(Guid workItemId, Guid tenantId, Guid? actorId, string field, string? fromJson, string? toJson, string? comment)
    {
        Id = Guid.NewGuid();
        WorkItemId = workItemId;
        TenantId = tenantId;
        ActorId = actorId;
        Field = field;
        FromJson = fromJson;
        ToJson = toJson;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }
}
