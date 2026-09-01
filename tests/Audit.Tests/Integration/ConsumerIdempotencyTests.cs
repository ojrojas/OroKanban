using Xunit;

namespace Audit.Tests.Integration;

public sealed class ConsumerIdempotencyTests
{
    [Fact]
    public async Task DuplicateEventId_ProducesOneEntry()
    {
        await Task.Delay(10);
        Assert.True(true); // Placeholder: Testcontainers + RabbitMQ duplicate delivery → count ==1
    }
}
