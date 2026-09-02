using BuildingBlocks.EventBus.Abstractions;

namespace AiProcessing.Infrastructure.Pipeline;

public sealed class ExtractionHandler : IIntegrationEventHandler<Contracts.Events.LlmProcessingStageRequestedIntegrationEvent>
{
    public Task HandleAsync(Contracts.Events.LlmProcessingStageRequestedIntegrationEvent @event, CancellationToken ct)
    {
        // Idempotent: load LlmOperation via RowVersion+Tenant, set InProgress, MarkSucceeded or MarkFailed
        return Task.CompletedTask;
    }
}
