using BuildingBlocks.Kernel.Domain.Rules;

namespace Organization.Domain.Rules;

public sealed class ManagerCannotBeSubordinateRule(Guid managerId, Guid subordinateId) : IBusinessRule
{
    public bool IsBroken() => managerId == subordinateId;
    public string Message => $"Manager and subordinate cannot be the same user ({managerId}).";
}