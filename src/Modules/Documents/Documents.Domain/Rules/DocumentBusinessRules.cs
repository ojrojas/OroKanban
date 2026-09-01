using BuildingBlocks.Kernel.Domain.Rules;

using Documents.Domain.Enumerations;

namespace Documents.Domain.Rules;

public sealed class VersionIsImmutableOncePublishedRule : IBusinessRule
{
    private readonly bool _isPublished;
    public VersionIsImmutableOncePublishedRule(bool isPublished) => _isPublished = isPublished;
    public bool IsBroken() => _isPublished;
    public string Message => "Version is immutable once published.";
}

public sealed class DocumentStatusTransitionRule : IBusinessRule
{
    private static readonly Dictionary<string, HashSet<string>> Allowed = new()
    {
        ["Draft"] = ["Uploaded"],
        ["Uploaded"] = ["Validated", "ProcessingFailed", "Deleted"],
        ["Validated"] = ["Classified", "ProcessingFailed", "Deleted"],
        ["Classified"] = ["Indexed", "Available", "PendingApproval", "ProcessingFailed", "Deleted"],
        ["Indexed"] = ["Available", "ProcessingFailed", "Deleted"],
        ["Available"] = ["PendingApproval", "Approved", "Archived", "Deleted", "RetentionExpired", "ProcessingFailed"],
        ["PendingApproval"] = ["Approved", "Deleted", "ProcessingFailed"],
        ["Approved"] = ["Deleted", "Archived", "RetentionExpired"],
        ["ProcessingFailed"] = ["Validated", "Deleted"],
        ["Archived"] = ["RetentionExpired"],
        ["Deleted"] = [],
        ["RetentionExpired"] = ["Archived", "Deleted"],
    };

    private readonly string _from;
    private readonly string _to;
    public DocumentStatusTransitionRule(DocumentStatus from, DocumentStatus to)
    {
        _from = from.Name;
        _to = to.Name;
    }
    public bool IsBroken() => !Allowed.TryGetValue(_from, out var tos) || !tos.Contains(_to);
    public string Message => $"Transition not allowed: {_from}→{_to}";
}

public sealed class ClassificationIsValidRule : IBusinessRule
{
    private readonly bool _isValid;
    public ClassificationIsValidRule(bool isValid, string? classification)
    {
        _isValid = isValid;
        Classification = classification;
    }
    public string? Classification { get; }
    public bool IsBroken() => !_isValid;
    public string Message => $"Classification '{Classification}' is not allowed.";
}

public sealed class MetadataSnapshotValidationRule : IBusinessRule
{
    private readonly bool _isValid;
    private readonly string _reason;
    public MetadataSnapshotValidationRule(bool isValid, string reason = "Metadata snapshot invalid")
    {
        _isValid = isValid;
        _reason = reason;
    }
    public bool IsBroken() => !_isValid;
    public string Message => _reason;
}

public sealed class ProcessingStageTransitionRule : IBusinessRule
{
    private readonly bool _isValid;
    public ProcessingStageTransitionRule(bool isValid) => _isValid = isValid;
    public bool IsBroken() => !_isValid;
    public string Message => "Processing stage transition not allowed.";
}

public sealed class StageIsRetryableRule : IBusinessRule
{
    private readonly bool _isRetryable;
    private readonly string _reason;
    public StageIsRetryableRule(bool isRetryable, string reason = "Stage is not retryable")
    {
        _isRetryable = isRetryable;
        _reason = reason;
    }
    public bool IsBroken() => !_isRetryable;
    public string Message => _reason;
}
