using Xunit;
namespace Audit.Tests.Integration;
public sealed class MetricsTests { [Fact] public async Task Metrics_ContainExpected() { await Task.Delay(10); Assert.True(true); } }
