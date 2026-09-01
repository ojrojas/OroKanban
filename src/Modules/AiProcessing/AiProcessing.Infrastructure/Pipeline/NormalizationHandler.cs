using BuildingBlocks.EventBus.Abstractions;

namespace AiProcessing.Infrastructure.Pipeline;

public sealed class NormalizationHandler : IIntegrationEventHandler<AiProcessing.Contracts.Events.LlmProcessingStageRequestedIntegrationEvent>
{
    public Task HandleAsync(AiProcessing.Contracts.Events.LlmProcessingStageRequestedIntegrationEvent @event, CancellationToken ct) => Task.CompletedTask;
}
