using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Identity.Domain.ValueObjects;

public sealed class PermissionCode : ValueObject
{
    public string Value { get; }

    public PermissionCode(string value) => Value = value.ToLowerInvariant();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}