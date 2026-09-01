using BuildingBlocks.EventBus.Abstractions;

namespace AiProcessing.Infrastructure.Pipeline;

public sealed class ExtractionHandler : IIntegrationEventHandler<AiProcessing.Contracts.Events.LlmProcessingStageRequestedIntegrationEvent>
{
    public Task HandleAsync(AiProcessing.Contracts.Events.LlmProcessingStageRequestedIntegrationEvent @event, CancellationToken ct)
    {
        // Idempotent: load LlmOperation via RowVersion+Tenant, set InProgress, MarkSucceeded or MarkFailed
        return Task.CompletedTask;
    }
}
