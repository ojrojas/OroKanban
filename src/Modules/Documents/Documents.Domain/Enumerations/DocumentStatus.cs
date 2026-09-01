using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Documents.Domain.Enumerations;

public sealed class DocumentStatus : Enumeration<DocumentStatus>
{
    public static readonly DocumentStatus Draft = new(1, nameof(Draft));
    public static readonly DocumentStatus Uploaded = new(2, nameof(Uploaded));
    public static readonly DocumentStatus Validated = new(3, nameof(Validated));
    public static readonly DocumentStatus Classified = new(4, nameof(Classified));
    public static readonly DocumentStatus Indexed = new(5, nameof(Indexed));
    public static readonly DocumentStatus Available = new(6, nameof(Available));
    public static readonly DocumentStatus PendingApproval = new(7, nameof(PendingApproval));
    public static readonly DocumentStatus Approved = new(8, nameof(Approved));
    public static readonly DocumentStatus ProcessingFailed = new(9, nameof(ProcessingFailed));
    public static readonly DocumentStatus Archived = new(10, nameof(Archived));
    public static readonly DocumentStatus Deleted = new(11, nameof(Deleted));
    public static readonly DocumentStatus RetentionExpired = new(12, nameof(RetentionExpired));

    private DocumentStatus(int id, string name) : base(id, name) { }
}
