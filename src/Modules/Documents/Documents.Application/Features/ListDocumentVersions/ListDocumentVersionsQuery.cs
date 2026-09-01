using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ListDocumentVersions;
public sealed record ListDocumentVersionsQuery(Guid DocumentId, Guid TenantId, int Page, int PageSize) : IQuery<Result<PagedResult<DocumentVersionResponse>>>;
public sealed record DocumentVersionResponse(Guid VersionId, int VersionNumber, string ContentHash, bool IsSafe, string ScanStatus);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
