using BuildingBlocks.Kernel.Domain.Entities;
using AiProcessing.Domain.Ids;
using AiProcessing.Domain.Events;
using AiProcessing.Domain.Rules;

namespace AiProcessing.Domain.Aggregates;

public sealed class LlmPromptVersion : AggregateRoot<LlmPromptVersionId>
{
    public int OperationTypeId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Template { get; private set; } = default!;
    public bool IsPublished { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private LlmPromptVersion() { }

    public LlmPromptVersion(LlmPromptVersionId id, int operationTypeId, int versionNumber, string template, Guid publishedBy)
    {
        if (!template.Contains("{{content}}")) throw new ArgumentException("Template must contain {{content}}");
        Id = id;
        OperationTypeId = operationTypeId;
        VersionNumber = versionNumber;
        Template = template;
        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = publishedBy;
        RaiseDomainEvent(new PromptVersionPublishedDomainEvent(id, operationTypeId.ToString(), versionNumber, publishedBy));
    }

    public void UpdateTemplate(string newTemplate)
    {
        CheckRule(new PromptIsImmutableOncePublishedRule(IsPublished));
        Template = newTemplate;
    }
}
