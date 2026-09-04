using BuildingBlocks.Kernel.Domain.Entities;
using Projects.Domain.Ids;

namespace Projects.Domain.Aggregates;

public sealed class WorkItemDeliverable : Entity<Guid>
{
    public Guid WorkItemId { get; private set; }
    public string Title { get; private set; } = default!;
    public int TypeId { get; private set; }
    public int StatusId { get; private set; }
    public string? Url { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WorkItemDeliverable() {}
    public WorkItemDeliverable(Guid id, Guid workItemId, string title, int typeId, int statusId, string? url)
    {
        Id = id;
        WorkItemId = workItemId;
        Title = title.Trim();
        TypeId = typeId;
        StatusId = statusId;
        Url = url;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
    public void Update(string title, int typeId, int statusId, string? url)
    {
        Title = title.Trim();
        TypeId = typeId;
        StatusId = statusId;
        Url = url;
        UpdatedAt = DateTime.UtcNow;
    }
}
