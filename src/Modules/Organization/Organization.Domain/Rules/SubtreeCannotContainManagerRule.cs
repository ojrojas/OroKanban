using BuildingBlocks.Kernel.Domain.Rules;

namespace Organization.Domain.Rules;

public sealed class SubtreeCannotContainManagerRule(Guid managerId, Guid subordinateId, IReadOnlyList<Guid> ancestorsOfSubordinate) : IBusinessRule
{
    public bool IsBroken() => ancestorsOfSubordinate.Contains(managerId);
    public string Message => $"Relationship {managerId} → {subordinateId} would create a cycle — manager is already a descendant of subordinate.";
}
