using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ProcessingJobs;
public sealed record GetProcessingJobQuery(Guid DocumentId, Guid TenantId) : IQuery<Result<ProcessingJobResponse>>;
public sealed record ProcessingJobResponse(Guid JobId, string OverallStatus, string CurrentStage);
public sealed class GetProcessingJobHandler : IQueryHandler<GetProcessingJobQuery, Result<ProcessingJobResponse>>
{
    public Task<Result<ProcessingJobResponse>> HandleAsync(GetProcessingJobQuery q, CancellationToken ct) => Task.FromResult(Result.Failure<ProcessingJobResponse>(Error.Failure("NotImplemented","Not implemented")));
}
