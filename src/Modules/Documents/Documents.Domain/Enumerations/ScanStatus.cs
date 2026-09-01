using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Documents.Domain.Enumerations;

public sealed class ScanStatus : Enumeration<ScanStatus>
{
    public static readonly ScanStatus Pending = new(0, nameof(Pending));
    public static readonly ScanStatus Safe = new(1, nameof(Safe));
    public static readonly ScanStatus Infected = new(2, nameof(Infected));
    public static readonly ScanStatus Unavailable = new(3, nameof(Unavailable));

    private ScanStatus(int id, string name) : base(id, name) { }
}
