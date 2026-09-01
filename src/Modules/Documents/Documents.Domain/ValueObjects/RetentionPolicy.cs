using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.ValueObjects;

public sealed class RetentionPolicy : ValueObject
{
    public DateTimeOffset? RetainUntil { get; }
    public int? RetentionDays { get; }
    public bool LegalHold { get; }

    public RetentionPolicy(DateTimeOffset? retainUntil, int? retentionDays, bool legalHold)
    {
        if (retentionDays is not null && retentionDays <= 0)
            throw new ArgumentException("RetentionDays must be > 0", nameof(retentionDays));
        RetainUntil = retainUntil;
        RetentionDays = retentionDays;
        LegalHold = legalHold;
    }

    public static RetentionPolicy None => new(null, null, false);

    public DateTimeOffset? ComputeRetainUntil(DateTimeOffset? effectiveDate)
    {
        if (RetainUntil is not null) return RetainUntil;
        if (RetentionDays is not null && effectiveDate is not null)
            return effectiveDate.Value.AddDays(RetentionDays.Value);
        return null;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        if (LegalHold) return false;
        if (RetainUntil is null) return false;
        return now >= RetainUntil.Value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RetainUntil;
        yield return RetentionDays;
        yield return LegalHold;
    }
}
