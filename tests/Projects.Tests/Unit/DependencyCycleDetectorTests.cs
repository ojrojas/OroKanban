using FluentAssertions;

using Projects.Domain.Enumerations;
using Projects.Infrastructure.Services;

namespace Projects.Tests.Unit;

public class DependencyCycleDetectorTests
{
    private readonly DependencyCycleDetector _detector = new();

    [Fact]
    public void ThreeNodeChain_ClosingCycle_ShouldDetect()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var existing = new List<(Guid, Guid, int)>
        {
            (a, b, DependencyType.Blocks.Id),
            (b, c, DependencyType.Blocks.Id)
        };
        var candidate = (c, a, DependencyType.Blocks.Id);
        _detector.HasCycle(existing, candidate).Should().BeTrue();
    }

    [Fact]
    public void RelatedTo_ShouldNotCauseCycle()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var existing = new List<(Guid, Guid, int)> { (a, b, DependencyType.Blocks.Id), (b, c, DependencyType.Blocks.Id) };
        var candidate = (c, a, DependencyType.RelatedTo.Id);
        _detector.HasCycle(existing, candidate).Should().BeFalse();
    }

    [Fact]
    public void Diamond_NoCycle_ShouldBeFalse()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var existing = new List<(Guid, Guid, int)> { (a, b, 1), (a, c, 1), (b, d, 1) };
        _detector.HasCycle(existing, (c, d, 1)).Should().BeFalse();
    }
}