using BuildingBlocks.Kernel.Domain.Rules;

namespace Audit.Domain.Rules;

public sealed class AuditEntryIsImmutableRule : IBusinessRule
{
    public bool IsBroken() => true; // Always broken if someone tries to mutate — enforced by no setters
    public string Message => "AuditEntry is immutable — corrections must be new entries with Action=AuditCorrected.";
}

public sealed class DateRangeInvalidRule : IBusinessRule
{
    private readonly DateTime? _from;
    private readonly DateTime? _to;
    public DateRangeInvalidRule(DateTime? from, DateTime? to) { _from = from; _to = to; }
    public bool IsBroken() => _from.HasValue && _to.HasValue && _from > _to;
    public string Message => "DateRange invalid: From > To";
}
