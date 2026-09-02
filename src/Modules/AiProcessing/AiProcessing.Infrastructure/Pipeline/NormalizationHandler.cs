using BuildingBlocks.EventBus.Abstractions;

namespace AiProcessing.Infrastructure.Pipeline;

public sealed class NormalizationHandler : IIntegrationEventHandler<Contracts.Events.LlmProcessingStageRequestedIntegrationEvent>
{
    public Task HandleAsync(Contracts.Events.LlmProcessingStageRequestedIntegrationEvent @event, CancellationToken ct) => Task.CompletedTask;
}
