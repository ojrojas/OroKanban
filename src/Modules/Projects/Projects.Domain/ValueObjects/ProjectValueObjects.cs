using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Projects.Domain.ValueObjects;

public sealed class Effort : ValueObject
{
    public decimal Hours { get; }

    private Effort(decimal hours) => Hours = hours;

    public static Effort FromHours(decimal hours)
    {
        if (hours < 0) throw new ArgumentOutOfRangeException(nameof(hours), "Effort must be >= 0");
        if (hours > 9999.9m) throw new ArgumentOutOfRangeException(nameof(hours), "Effort must be <= 9999.9");
        // round to 1 decimal
        hours = Math.Round(hours, 1, MidpointRounding.AwayFromZero);
        return new Effort(hours);
    }

    public static Effort Zero => new(0);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Hours; }
}

public sealed class ProgressValue : ValueObject
{
    public int Percent { get; }

    private ProgressValue(int percent) => Percent = percent;

    public static ProgressValue FromPercent(int percent)
    {
        if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent), "Progress must be 0..100");
        return new ProgressValue(percent);
    }

    public static ProgressValue Zero => new(0);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Percent; }
}

public sealed class DueDate : ValueObject
{
    public DateTime? Value { get; }

    private DueDate(DateTime? value) => Value = value?.ToUniversalTime();

    public static DueDate Empty => new(null);
    public static DueDate From(DateTime? value) => new(value);

    public bool IsOverdue(DateTime now, string statusName) =>
        Value.HasValue && Value.Value < now && !string.Equals(statusName, "Completed", StringComparison.OrdinalIgnoreCase);

    public bool IsOverdue(DateTime now) => Value.HasValue && Value.Value < now;

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}

public sealed class Tag : ValueObject
{
    public string Value { get; }

    private Tag(string value) => Value = value;

    public static Tag Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tag must not be empty", nameof(value));
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length < 1 || normalized.Length > 50) throw new ArgumentOutOfRangeException(nameof(value), "Tag must be 1..50 chars");
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[a-z0-9_-]+$"))
            throw new ArgumentException("Tag must match ^[a-z0-9_-]+$", nameof(value));
        return new Tag(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}