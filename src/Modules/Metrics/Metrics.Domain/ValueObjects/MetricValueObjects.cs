using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Metrics.Domain.ValueObjects;

public sealed class MetricWeight : ValueObject
{
    public decimal Value { get; }
    private MetricWeight(decimal value) => Value = value;
    public static MetricWeight From(decimal value)
    {
        if (value < 0m || value > 1m) throw new ArgumentOutOfRangeException(nameof(value), "Weight must be 0–1");
        return new MetricWeight(Math.Round(value, 2));
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}

public sealed class MetricTarget : ValueObject
{
    public decimal Value { get; }
    private MetricTarget(decimal value) => Value = value;
    public static MetricTarget From(decimal value)
    {
        if (value < 0m || value > 100m) throw new ArgumentOutOfRangeException(nameof(value), "Target must be 0–100");
        return new MetricTarget(value);
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}

public sealed class MetricThreshold : ValueObject
{
    public decimal Value { get; }
    private MetricThreshold(decimal value) => Value = value;
    public static MetricThreshold From(decimal value)
    {
        if (value < 0m || value > 100m) throw new ArgumentOutOfRangeException(nameof(value), "Threshold must be 0–100");
        return new MetricThreshold(value);
    }
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}

public sealed class ComponentValue : ValueObject
{
    public string Name { get; }
    public decimal Weight { get; }
    public decimal Progress { get; }
    public decimal Contribution => Progress * Weight;
    public bool IsOverride { get; }

    public ComponentValue(string name, decimal weight, decimal progress, bool isOverride = false)
    {
        Name = name;
        Weight = weight;
        Progress = progress;
        IsOverride = isOverride;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Weight;
        yield return Progress;
        yield return IsOverride;
    }
}
