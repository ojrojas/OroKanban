using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Rules;
using Metrics.Domain.Ids;

namespace Metrics.Domain.Aggregates;

public sealed class MetricDefinition : AggregateRoot<MetricDefinitionId>
{
    public Guid ProjectId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int DimensionId { get; private set; }
    public decimal Weight { get; private set; }
    public decimal Target { get; private set; }
    public decimal Threshold { get; private set; }
    public bool RequiresEvidence { get; private set; }
    public int Version { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public Guid TenantId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private MetricDefinition() { }

    private MetricDefinition(MetricDefinitionId id, Guid tenantId, Guid? projectId, string code, string name, int dimensionId, decimal weight, decimal target, decimal threshold, bool requiresEvidence)
        : base(id)
    {
        TenantId = tenantId;
        ProjectId = projectId ?? Guid.Empty;
        Code = code;
        Name = name;
        DimensionId = dimensionId;
        Weight = weight;
        Target = target;
        Threshold = threshold;
        RequiresEvidence = requiresEvidence;
        Version = 1;
        IsCurrent = true;
        EffectiveFrom = DateTime.UtcNow;
    }

    public static MetricDefinition Create(Guid tenantId, Guid? projectId, string code, string name, int dimensionId, decimal weight, decimal target, decimal threshold, bool requiresEvidence)
    {
        if (weight < 0m || weight > 1m) throw new BusinessRuleValidationException(new WeightRule());
        return new MetricDefinition(MetricDefinitionId.New(), tenantId, projectId, code.Trim().ToLowerInvariant(), name, dimensionId, weight, target, threshold, requiresEvidence);
    }

    private sealed class WeightRule : IBusinessRule { public bool IsBroken() => true; public string Message => "Weight 0–1"; }
}

public sealed class MetricValue : AggregateRoot<MetricValueId>
{
    public MetricDefinitionId DefinitionId { get; private set; } = default!;
    public Guid ProjectId { get; private set; }
    public decimal Value { get; private set; }
    public decimal Threshold { get; private set; }
    public bool IsViolated => Value < Threshold;
    public DateTime ComputedAt { get; private set; }
    public Guid TenantId { get; private set; }

    private MetricValue() { }
    public MetricValue(MetricValueId id, MetricDefinitionId defId, Guid projectId, decimal value, decimal threshold, Guid tenantId) : base(id)
    {
        DefinitionId = defId;
        ProjectId = projectId;
        Value = value;
        Threshold = threshold;
        TenantId = tenantId;
        ComputedAt = DateTime.UtcNow;
    }
}

public sealed class Milestone : AggregateRoot<MilestoneId>
{
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime DueDate { get; private set; }
    public List<Guid> LinkedWorkItemIds { get; private set; } = [];
    public int Status { get; private set; } // 1 Planned 2 Reached 3 Slipped
    public int Version { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public Guid TenantId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Milestone() { }
    public Milestone(MilestoneId id, Guid projectId, string title, DateTime dueDate, List<Guid> linked, Guid tenantId) : base(id)
    {
        ProjectId = projectId;
        Title = title;
        DueDate = dueDate.ToUniversalTime();
        LinkedWorkItemIds = linked;
        Status = 1;
        Version = 1;
        IsCurrent = true;
        EffectiveFrom = DateTime.UtcNow;
        TenantId = tenantId;
    }
}

public sealed class ProgressExplanation : AggregateRoot<Guid>
{
    public Guid WorkItemId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string StrategyId { get; private set; } = default!;
    public DateTime ComputedAt { get; private set; }
    public decimal ResultPercent { get; private set; }
    public decimal WeightsSum { get; private set; }
    public bool ZeroWeight { get; private set; }
    public bool IsOverride { get; private set; }
    public string ComponentsJson { get; private set; } = "[]";
    public string InputsSnapshotJson { get; private set; } = "{}";
    public Guid TenantId { get; private set; }

    private ProgressExplanation() { }
    public ProgressExplanation(Guid id, Guid workItemId, Guid projectId, string strategyId, decimal result, decimal weightsSum, bool zeroWeight, string componentsJson, string snapshot, Guid tenantId) : base(id)
    {
        WorkItemId = workItemId;
        ProjectId = projectId;
        StrategyId = strategyId;
        ResultPercent = result;
        WeightsSum = weightsSum;
        ZeroWeight = zeroWeight;
        ComponentsJson = componentsJson;
        InputsSnapshotJson = snapshot;
        TenantId = tenantId;
        ComputedAt = DateTime.UtcNow;
    }
}
