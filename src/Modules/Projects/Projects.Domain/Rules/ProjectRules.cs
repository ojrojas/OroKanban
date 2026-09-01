using BuildingBlocks.Kernel.Domain.Rules;

namespace Projects.Domain.Rules;

public sealed class TransitionIsAllowedRule : IBusinessRule
{
    private readonly int _fromId;
    private readonly int _toId;
    private readonly Services.IWorkItemTransitionPolicy _policy;

    public TransitionIsAllowedRule(int fromId, int toId, Services.IWorkItemTransitionPolicy policy)
    {
        _fromId = fromId;
        _toId = toId;
        _policy = policy;
    }

    public bool IsBroken() => !_policy.IsAllowed(_fromId, _toId);

    public string Message => $"Transition not allowed: {_fromId} → {_toId}";
}

public sealed class CircularDependencyRule : IBusinessRule
{
    private readonly bool _hasCycle;
    public CircularDependencyRule(bool hasCycle) => _hasCycle = hasCycle;
    public bool IsBroken() => _hasCycle;
    public string Message => "Circular dependency";
}

public sealed class ReparentNoCycleRule : IBusinessRule
{
    private readonly bool _isDescendant;
    public ReparentNoCycleRule(bool isDescendant) => _isDescendant = isDescendant;
    public bool IsBroken() => _isDescendant;
    public string Message => "Cannot reparent to descendant";
}

public sealed class WorkItemNotCompletedRule : IBusinessRule
{
    private readonly int _statusId;
    private readonly int _completedId;
    public WorkItemNotCompletedRule(int statusId, int completedId)
    {
        _statusId = statusId;
        _completedId = completedId;
    }
    public bool IsBroken() => _statusId == _completedId;
    public string Message => "Work item is completed";
}

public sealed class TitleRequiredRule : IBusinessRule
{
    private readonly string? _title;
    public TitleRequiredRule(string? title) => _title = title;
    public bool IsBroken() => string.IsNullOrWhiteSpace(_title) || _title!.Trim().Length is < 1 or > 200;
    public string Message => "Title is required (1..200)";
}

public sealed class SameProjectRule : IBusinessRule
{
    private readonly Guid _expectedProjectId;
    private readonly Guid _actualProjectId;
    public SameProjectRule(Guid expected, Guid actual)
    {
        _expectedProjectId = expected;
        _actualProjectId = actual;
    }
    public bool IsBroken() => _expectedProjectId != _actualProjectId;
    public string Message => "Parent and child must be in same project";
}