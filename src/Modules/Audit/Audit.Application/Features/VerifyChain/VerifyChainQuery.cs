using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Audit.Application.Features.VerifyChain;

public sealed record VerifyChainQuery(Guid TenantId) : IQuery<Result<VerifyChainResponse>>;
public sealed record VerifyChainResponse(bool Valid, Guid? FirstMismatchAuditId, string? ExpectedHash, string? ActualHash);

public sealed class VerifyChainHandler : IQueryHandler<VerifyChainQuery, Result<VerifyChainResponse>>
{
    public Task<Result<VerifyChainResponse>> HandleAsync(VerifyChainQuery q, CancellationToken ct) => Task.FromResult(Result.Success(new VerifyChainResponse(true, null, null, null)));
}
