using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Audit.Domain.Enumerations;

public sealed class AuditAction : Enumeration<AuditAction>
{
    public static readonly AuditAction AuthenticationSucceeded = new(1, nameof(AuthenticationSucceeded));
    public static readonly AuditAction AuthenticationFailed = new(2, nameof(AuthenticationFailed));
    public static readonly AuditAction AuthorizationDenied = new(3, nameof(AuthorizationDenied));
    public static readonly AuditAction ProjectCreated = new(4, nameof(ProjectCreated));
    public static readonly AuditAction ProjectUpdated = new(5, nameof(ProjectUpdated));
    public static readonly AuditAction WorkItemCreated = new(6, nameof(WorkItemCreated));
    public static readonly AuditAction WorkItemUpdated = new(7, nameof(WorkItemUpdated));
    public static readonly AuditAction WorkItemAssigned = new(8, nameof(WorkItemAssigned));
    public static readonly AuditAction WorkItemStatusChanged = new(9, nameof(WorkItemStatusChanged));
    public static readonly AuditAction ProjectMetricChanged = new(10, nameof(ProjectMetricChanged));
    public static readonly AuditAction DocumentUploaded = new(11, nameof(DocumentUploaded));
    public static readonly AuditAction DocumentClassified = new(12, nameof(DocumentClassified));
    public static readonly AuditAction DocumentVersionPublished = new(13, nameof(DocumentVersionPublished));
    public static readonly AuditAction DocumentAccessed = new(14, nameof(DocumentAccessed));
    public static readonly AuditAction DocumentAccessDenied = new(15, nameof(DocumentAccessDenied));
    public static readonly AuditAction DocumentDeleted = new(16, nameof(DocumentDeleted));
    public static readonly AuditAction DocumentApproved = new(17, nameof(DocumentApproved));
    public static readonly AuditAction PermissionChanged = new(18, nameof(PermissionChanged));
    public static readonly AuditAction GrantAdded = new(19, nameof(GrantAdded));
    public static readonly AuditAction GrantRevoked = new(20, nameof(GrantRevoked));
    public static readonly AuditAction HierarchyChanged = new(21, nameof(HierarchyChanged));
    public static readonly AuditAction LlmOperationQueued = new(22, nameof(LlmOperationQueued));
    public static readonly AuditAction LlmOperationCompleted = new(23, nameof(LlmOperationCompleted));
    public static readonly AuditAction LlmOperationFailed = new(24, nameof(LlmOperationFailed));
    public static readonly AuditAction LlmResultGenerated = new(25, nameof(LlmResultGenerated));
    public static readonly AuditAction LlmResultApproved = new(26, nameof(LlmResultApproved));
    public static readonly AuditAction LlmResultRejected = new(27, nameof(LlmResultRejected));
    public static readonly AuditAction LlmReviewCreated = new(28, nameof(LlmReviewCreated));
    public static readonly AuditAction RagQueryExecuted = new(29, nameof(RagQueryExecuted));
    public static readonly AuditAction ConfigurationChanged = new(30, nameof(ConfigurationChanged));
    public static readonly AuditAction AuditCorrected = new(31, nameof(AuditCorrected));
    public static readonly AuditAction AuditSearchDenied = new(32, nameof(AuditSearchDenied));
    private AuditAction(int id, string name) : base(id, name) { }
    public static AuditAction FromIntegrationEventType(Type type) => FromName(type.Name.Replace("IntegrationEvent",""));
}
