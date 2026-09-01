using AiProcessing.Domain.Aggregates;
using AiProcessing.Domain.Ids;
using Xunit;

namespace AiProcessing.Tests.Unit;

public sealed class PromptImmutabilityTests
{
    [Fact]
    public void PublishedVersion_IsImmutable()
    {
        var id = new LlmPromptVersionId(Guid.NewGuid());
        var prompt = new LlmPromptVersion(id, 1, 1, "Summarize {{content}} in 3 bullets", Guid.NewGuid());
        Assert.Throws<BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException>(() => prompt.UpdateTemplate("new template"));
    }
}
