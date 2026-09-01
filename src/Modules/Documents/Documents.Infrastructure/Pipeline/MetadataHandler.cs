using BuildingBlocks.EventBus.Abstractions;
using Documents.Contracts.Events;
namespace Documents.Infrastructure.Pipeline;
public sealed class MetadataHandler : IIntegrationEventHandler<DocumentProcessingStageRequestedIntegrationEvent>
{
    public System.Threading.Tasks.Task HandleAsync(DocumentProcessingStageRequestedIntegrationEvent e, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
}
