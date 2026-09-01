using BuildingBlocks.Kernel.Domain.Rules;

namespace AiProcessing.Domain.Rules;

public sealed class PromptIsImmutableOncePublishedRule : IBusinessRule
{
    private readonly bool _isPublished;
    public PromptIsImmutableOncePublishedRule(bool isPublished) => _isPublished = isPublished;
    public bool IsBroken() => _isPublished;
    public string Message => "Prompt version is immutable once published.";
}

public sealed class ReviewStatusTransitionRule : IBusinessRule
{
    private static readonly Dictionary<string, HashSet<string>> Allowed = new()
    {
        ["Generated"] = new HashSet<string> { "PendingReview" },
        ["PendingReview"] = new HashSet<string> { "Approved", "Rejected", "Superseded" },
        ["Approved"] = new HashSet<string> { "Superseded" },
        ["Rejected"] = new HashSet<string> { "Superseded" },
        ["GeneratedPending"] = new HashSet<string>() // fake
    };
    private readonly string _from;
    private readonly string _to;
    public ReviewStatusTransitionRule(string from, string to) { _from = from; _to = to; }
    public bool IsBroken()
    {
        if (!Allowed.TryGetValue(_from, out var tos)) return true;
        return !tos.Contains(_to);
    }
    public string Message => $"Review transition not allowed: {_from}→{_to}";
}

public sealed class ProvenanceCompleteRule : IBusinessRule
{
    private readonly bool _complete;
    public ProvenanceCompleteRule(bool complete) => _complete = complete;
    public bool IsBroken() => !_complete;
    public string Message => "Provenance is incomplete - all mandatory fields required.";
}

public sealed class StageIsRetryableRule : IBusinessRule
{
    private readonly string _status;
    public StageIsRetryableRule(string status) => _status = status;
    public bool IsBroken() => _status == "Succeeded" || _status == "Completed";
    public string Message => $"Stage with status {_status} is not retryable.";
}

public sealed class ChunkReferenceValidationRule : IBusinessRule
{
    private readonly bool _valid;
    public ChunkReferenceValidationRule(bool valid) => _valid = valid;
    public bool IsBroken() => !_valid;
    public string Message => "ChunkReference is invalid.";
}

public sealed class OperationStatusTransitionRule : IBusinessRule
{
    private readonly bool _allowed;
    public OperationStatusTransitionRule(bool allowed) => _allowed = allowed;
    public bool IsBroken() => !_allowed;
    public string Message => "Operation status transition not allowed.";
}
