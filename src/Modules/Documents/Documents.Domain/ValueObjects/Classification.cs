using BuildingBlocks.Kernel.Domain.ValueObjects;

using Documents.Domain.Enumerations;

namespace Documents.Domain.ValueObjects;

public sealed class Classification : ValueObject
{
    public ClassificationLevel Level { get; }
    public string Value { get; }
    public string RuleVersion { get; }

    public Classification(ClassificationLevel level, string value, string ruleVersion)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
            throw new ArgumentException("Classification value must be 1..100 chars", nameof(value));
        if (string.IsNullOrWhiteSpace(ruleVersion) || ruleVersion.Length > 20)
            throw new ArgumentException("RuleVersion must be 1..20 chars", nameof(ruleVersion));
        Value = value;
        RuleVersion = ruleVersion;
    }

    public bool IsMoreSensitiveThan(Classification other) => Level.IsMoreSensitiveThan(other.Level);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Level.Id;
        yield return Value;
        yield return RuleVersion;
    }

    public override string ToString() => $"{Value} ({Level.Name} v{RuleVersion})";
}
