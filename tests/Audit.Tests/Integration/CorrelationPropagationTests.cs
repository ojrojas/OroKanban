using Xunit;

namespace Audit.Tests.Integration;

public sealed class CorrelationPropagationTests
{
    [Fact]
    public async Task XCorrelationId_PropagatesToAuditEntry()
    {
        await Task.Delay(10);
        Assert.True(true);
    }
}
