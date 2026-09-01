using AiProcessing.Domain.ValueObjects;
using Xunit;

namespace AiProcessing.Tests.Unit;

public sealed class ProvenanceCompletenessTests
{
    [Fact]
    public void Provenance_WithAllFields_Succeeds()
    {
        var model = new ModelDescriptor("azure", "gpt-4o-2024-08-06", "1");
        var provenance = new Provenance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Summarization", model, "v1", DateTime.UtcNow, Guid.NewGuid(), "Completed", new QualityIndicator(0.92f));
        Assert.NotNull(provenance);
        Assert.Equal("Summarization", provenance.OperationType);
    }

    [Fact]
    public void Provenance_WithoutPromptVersion_Throws()
    {
        var model = new ModelDescriptor("azure", "gpt-4o-2024-08-06", "1");
        Assert.Throws<ArgumentException>(() => new Provenance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Summarization", model, "", DateTime.UtcNow, Guid.NewGuid(), "Completed"));
    }
}
