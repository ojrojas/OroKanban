using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Metrics.Domain.Enumerations;

public sealed class MetricDimension(int id, string name) : Enumeration<MetricDimension>(id, name)
{
    public static readonly MetricDimension Completion = new(1, "Completion");
    public static readonly MetricDimension DeadlineAdherence = new(2, "DeadlineAdherence");
    public static readonly MetricDimension ContentCompleteness = new(3, "ContentCompleteness");
    public static readonly MetricDimension Quality = new(4, "Quality");
    public static readonly MetricDimension Risk = new(5, "Risk");
    public static readonly MetricDimension Criticality = new(6, "Criticality");
    public static readonly MetricDimension Effort = new(7, "Effort");
    public static readonly MetricDimension DependencyHealth = new(8, "DependencyHealth");
    public static readonly MetricDimension DocumentCompliance = new(9, "DocumentCompliance");
    public static readonly MetricDimension ReviewStatus = new(10, "ReviewStatus");
}

public sealed class DeadlineStatus(int id, string name) : Enumeration<DeadlineStatus>(id, name)
{
    public static readonly DeadlineStatus OnTime = new(1, "OnTime");
    public static readonly DeadlineStatus AtRisk = new(2, "AtRisk");
    public static readonly DeadlineStatus Overdue = new(3, "Overdue");
    public static readonly DeadlineStatus CompletedOnTime = new(4, "CompletedOnTime");
    public static readonly DeadlineStatus CompletedLate = new(5, "CompletedLate");
}
