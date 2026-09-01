using BuildingBlocks.Kernel.Domain.Entities;

using Projects.Domain.Events;
using Projects.Domain.Ids;

namespace Projects.Domain.Aggregates;

public sealed class WorkItemDependency : AggregateRoot<WorkItemDependencyId>
{
    public Guid TenantId { get; private set; }
    public Guid DependentId { get; private set; }
    public Guid PrincipalId { get; private set; }
    public int TypeId { get; private set; }

    private WorkItemDependency() { }

    private WorkItemDependency(WorkItemDependencyId id, Guid tenantId, Guid dependentId, Guid principalId, int typeId)
        : base(id)
    {
        TenantId = tenantId;
        DependentId = dependentId;
        PrincipalId = principalId;
        TypeId = typeId;
        RaiseDomainEvent(new DependencyAddedDomainEvent(id.Value, dependentId, principalId, typeId));
    }

    public static WorkItemDependency Create(Guid tenantId, Guid dependentId, Guid principalId, int typeId)
    {
        if (dependentId == principalId) throw new BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException(new SelfDependencyRule());
        return new WorkItemDependency(WorkItemDependencyId.New(), tenantId, dependentId, principalId, typeId);
    }

    private sealed class SelfDependencyRule : BuildingBlocks.Kernel.Domain.Rules.IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "Dependent and principal must differ";
    }
}