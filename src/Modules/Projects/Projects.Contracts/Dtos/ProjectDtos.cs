namespace Projects.Contracts.Dtos;

public sealed record CreateProjectResponse(Guid Id, Guid TenantId, string Name, string Status, string Priority, string Criticality, Guid OwnerId, Guid ManagerId, int Version);
public sealed record ProjectDetailResponse(Guid Id, Guid TenantId, string Name, string? Description, string Status, string Priority, string Criticality, Guid OwnerId, Guid ManagerId, DateTime? DueDate, DateTime UpdatedAt, IReadOnlyList<ProjectMemberDto> Members, IReadOnlyList<MilestoneDto> Milestones);
public sealed record ProjectMemberDto(Guid UserId, string Role, DateTime JoinedAt);
public sealed record MilestoneDto(Guid Id, string Title, DateTime? DueDate, bool IsReached);

public sealed record WorkItemDetailResponse(Guid Id, Guid ProjectId, Guid? ParentId, string Title, string? Description, string Type, string Status, string Priority, string Criticality, Guid? OwnerId, Guid? ResponsibleId, Guid? ReviewerId, DateTime? DueDate, int Progress, IReadOnlyList<string> Tags, int Version, DateTime UpdatedAt, Guid TenantId, bool IsOverdue, IReadOnlyList<Guid> DependencyIds);
public sealed record CreateWorkItemResponse(Guid Id, Guid ProjectId, Guid? ParentId, string Title, string Type, string Status, string Priority, string Criticality, Guid? ResponsibleId, DateTime? DueDate, int Progress, IReadOnlyList<string> Tags, int Version);
public sealed record BoardItemDto(Guid Id, string Title, string Type, string Status, string Priority, string Criticality, Guid? ResponsibleId, DateTime? DueDate, bool IsOverdue, int Progress, IReadOnlyList<string> Tags, Guid? ParentId, Guid? EpicId, bool BlockedDerived, int Version, DateTime UpdatedAt);
public sealed record BoardColumnDto(string Status, int StatusId, int Count, IReadOnlyList<BoardItemDto> Items);
public sealed record KanbanBoardResponse(Guid ProjectId, DateTime GeneratedAt, IReadOnlyList<BoardColumnDto> Columns, IReadOnlyList<SwimlaneDto> Swimlanes, int Page, int PageSize, int TotalCount, int OverdueCount);
public sealed record SwimlaneDto(string Key, string Label, IReadOnlyList<BoardColumnDto> Columns);