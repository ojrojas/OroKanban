using BuildingBlocks.Kernel.Domain.Enumerations;

namespace AiProcessing.Domain.Enumerations;

public sealed class OperationType : Enumeration<OperationType>
{
    public static readonly OperationType Summarization = new(1, nameof(Summarization));
    public static readonly OperationType Classification = new(2, nameof(Classification));
    public static readonly OperationType MetadataExtraction = new(3, nameof(MetadataExtraction));
    public static readonly OperationType EntityExtraction = new(4, nameof(EntityExtraction));
    public static readonly OperationType TaskExtraction = new(5, nameof(TaskExtraction));
    public static readonly OperationType DeadlineExtraction = new(6, nameof(DeadlineExtraction));
    public static readonly OperationType RequirementExtraction = new(7, nameof(RequirementExtraction));
    public static readonly OperationType RiskDetection = new(8, nameof(RiskDetection));
    public static readonly OperationType ContentCompleteness = new(9, nameof(ContentCompleteness));
    public static readonly OperationType VersionComparison = new(10, nameof(VersionComparison));
    public static readonly OperationType QuestionAnswering = new(11, nameof(QuestionAnswering));
    public static readonly OperationType ProjectContextAnalysis = new(12, nameof(ProjectContextAnalysis));

    private OperationType(int id, string name) : base(id, name) { }
}
