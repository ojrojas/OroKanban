using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Audit.Domain.ValueObjects;

public sealed class BeforeAfterSnapshot : ValueObject
{
    public string BeforeJson { get; }
    public string AfterJson { get; }
    public BeforeAfterSnapshot(string beforeJson, string afterJson)
    {
        BeforeJson = beforeJson ?? "{}";
        AfterJson = afterJson ?? "{}";
    }
    // Masking is done via IAuditMaskingPolicy before persistence, not here
    protected override IEnumerable<object?> GetEqualityComponents() { yield return BeforeJson; yield return AfterJson; }
}
