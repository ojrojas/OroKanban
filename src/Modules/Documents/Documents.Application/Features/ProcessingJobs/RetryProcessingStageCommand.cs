using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ProcessingJobs;
public sealed record RetryProcessingStageCommand(Guid DocumentId, string Stage, Guid TenantId, Guid ActorId) : ICommand<Result<ProcessingJobResponse>>;
public sealed class RetryProcessingStageHandler : ICommandHandler<RetryProcessingStageCommand, Result<ProcessingJobResponse>>
{
    public Task<Result<ProcessingJobResponse>> HandleAsync(RetryProcessingStageCommand cmd, CancellationToken ct) => Task.FromResult(Result.Failure<ProcessingJobResponse>(Error.Failure("NotImplemented","Not implemented")));
}
