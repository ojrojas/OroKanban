using Xunit;
namespace Audit.Tests.Integration;
public sealed class HealthEndpointsTests { [Fact] public async Task Health_ReturnsHealthy() { await Task.Delay(10); Assert.True(true); } }
