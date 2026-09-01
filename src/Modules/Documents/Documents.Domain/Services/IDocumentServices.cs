using BuildingBlocks.Kernel.Domain.Results;

namespace Documents.Domain.Services;

public sealed record AccessContext(
    Guid ActorId,
    Guid TenantId,
    Guid DocumentTenantId,
    Guid OwnerId,
    Guid OrganizationId,
    Guid? ProjectId,
    Guid? WorkItemId,
    string ClassificationValue,
    int ClassificationLevelId,
    string RuleVersion,
    bool IsSafe,
    string ScanStatus,
    IReadOnlySet<string> ActorRoles,
    Guid DocumentId);

public sealed record AccessDecision(bool Granted, string Reason);

public sealed record ClassificationContext(
    string? MimeType,
    IReadOnlySet<string> Tags,
    string? DocumentType,
    Guid OrganizationId,
    string? AuthorDepartment);

public interface IDocumentAccessPolicy
{
    Task<AccessDecision> EvaluateAsync(AccessContext ctx, CancellationToken ct);
}

public interface IClassificationPolicy
{
    Task<(string Classification, string RuleVersion)> ClassifyAsync(ClassificationContext ctx, CancellationToken ct);
    IReadOnlyList<string> AllowedLevels(Guid organizationId);
    Task<bool> IsAllowedAsync(string classificationValue, Guid organizationId, CancellationToken ct);
}

public enum ScanFailureKind { Clean, Infected, Unavailable }

public sealed record ScanResult(bool IsClean, string? Reason, ScanFailureKind Kind);

public interface ISecurityScanProvider
{
    Task<ScanResult> ScanAsync(Stream content, string contentHash, CancellationToken ct);
}

public sealed record BlobRef(string ContentHash, string Key, long Size);

public interface IStorageGateway
{
    Task<Result<BlobRef>> PutAsync(Stream bytes, string contentHash, string mimeType, CancellationToken ct);
    Task<Result<Stream>> GetAsync(string contentHash, bool isSafe, CancellationToken ct);
    Task<bool> ExistsAsync(string contentHash, CancellationToken ct);
    string CreatePresignedUrl(string contentHash, bool isSafe, TimeSpan ttl);
}
