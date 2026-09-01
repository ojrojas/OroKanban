using BuildingBlocks.Kernel.Domain.Enumerations;

namespace AiProcessing.Domain.Enumerations;

public sealed class ReviewStatus : Enumeration<ReviewStatus>
{
    public static readonly ReviewStatus Generated = new(1, nameof(Generated));
    public static readonly ReviewStatus PendingReview = new(2, nameof(PendingReview));
    public static readonly ReviewStatus Approved = new(3, nameof(Approved));
    public static readonly ReviewStatus Rejected = new(4, nameof(Rejected));
    public static readonly ReviewStatus Superseded = new(5, nameof(Superseded));
    public static readonly ReviewStatus Failed = new(6, nameof(Failed));

    private ReviewStatus(int id, string name) : base(id, name) { }
}
