using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.GetDocument;
public sealed record GetDocumentQuery(Guid DocumentId, Guid TenantId, Guid ActorId) : IQuery<Result<DocumentResponse>>;
public sealed record DocumentResponse(Guid Id, string Name, string Classification, string RuleVersion, string Status, bool IsSafe, string ScanStatus, string MimeType, long Size, string ContentHash, string? DownloadUrl);
