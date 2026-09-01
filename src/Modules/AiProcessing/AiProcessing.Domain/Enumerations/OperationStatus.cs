using BuildingBlocks.Kernel.Domain.Enumerations;

namespace AiProcessing.Domain.Enumerations;

public sealed class OperationStatus : Enumeration<OperationStatus>
{
    public static readonly OperationStatus Queued = new(1, nameof(Queued));
    public static readonly OperationStatus Processing = new(2, nameof(Processing));
    public static readonly OperationStatus Completed = new(3, nameof(Completed));
    public static readonly OperationStatus FailedRetryable = new(4, nameof(FailedRetryable));
    public static readonly OperationStatus FailedPermanent = new(5, nameof(FailedPermanent));
    public static readonly OperationStatus Superseded = new(6, nameof(Superseded));
    public static readonly OperationStatus Cancelled = new(7, nameof(Cancelled));

    private OperationStatus(int id, string name) : base(id, name) { }
}
