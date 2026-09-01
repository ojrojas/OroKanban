using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.UploadDocument;
public sealed record UploadDocumentCommand(string Name, string MimeType, long Size, byte[] Content, Guid TenantId, Guid OwnerId, Guid? ProjectId, Guid? WorkItemId, string? ClassificationHint) : ICommand<Result<UploadDocumentResponse>>;
public sealed record UploadDocumentResponse(Guid DocumentId, Guid VersionId, int VersionNumber, string ContentHash, string MimeType, long Size, string Classification, string RuleVersion, string Status, string CurrentStage);
