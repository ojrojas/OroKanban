using BuildingBlocks.EventBus.Abstractions;
using Documents.Contracts.Events;
namespace Documents.Infrastructure.Pipeline;
public sealed class StorageHandler : IIntegrationEventHandler<DocumentProcessingStageRequestedIntegrationEvent>
{
    public Task HandleAsync(DocumentProcessingStageRequestedIntegrationEvent e, CancellationToken ct) => Task.CompletedTask;
}
