namespace Audit.Contracts.DTOs;

public sealed record AuditEntryResponse(Guid AuditId, DateTime Timestamp, object Actor, string Action, string ResourceType, string ResourceId, Guid? OrganizationId, Guid TenantId, string Result, string? ErrorCode, Guid CorrelationId, object? BeforeAfterSnapshot, string? PreviousHash, string? Hash);
public sealed record PagedAuditResult(IReadOnlyList<AuditEntryResponse> Items, int TotalCount, int Page, int PageSize);
public sealed record SearchAuditEntriesRequest(Guid? ActorId, string? Action, string? ResourceType, string? ResourceId, Guid? ProjectId, Guid? OrganizationId, DateTime? From, DateTime? To, string? Result, Guid? CorrelationId, int Page = 1, int PageSize = 50);
