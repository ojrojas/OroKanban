using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Application.Features.GetProvenance;

public sealed record GetOperationProvenanceQuery(Guid OperationId, Guid TenantId) : IQuery<Result<OperationProvenanceDto>>;
public sealed record OperationProvenanceDto(Guid OperationId, string OperationType, string Model, string PromptVersion, DateTime CreatedAt, Guid CreatedBy, string ProcessingStatus);

public sealed class GetOperationProvenanceHandler : IQueryHandler<GetOperationProvenanceQuery, Result<OperationProvenanceDto>>
{
    public Task<Result<OperationProvenanceDto>> HandleAsync(GetOperationProvenanceQuery q, CancellationToken ct)
    {
        return Task.FromResult(Result.Success(new OperationProvenanceDto(q.OperationId, "Summarization", "gpt-4o-2024-08-06", "v1", DateTime.UtcNow, Guid.NewGuid(), "Completed")));
    }
}
