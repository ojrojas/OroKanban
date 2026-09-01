using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Documents.Domain.Enumerations;

public sealed class ProcessingStage : Enumeration<ProcessingStage>
{
    public static readonly ProcessingStage Upload = new(1, nameof(Upload));
    public static readonly ProcessingStage Validation = new(2, nameof(Validation));
    public static readonly ProcessingStage VirusScan = new(3, nameof(VirusScan));
    public static readonly ProcessingStage Metadata = new(4, nameof(Metadata));
    public static readonly ProcessingStage Classification = new(5, nameof(Classification));
    public static readonly ProcessingStage Storage = new(6, nameof(Storage));
    public static readonly ProcessingStage Indexing = new(7, nameof(Indexing));

    private ProcessingStage(int id, string name) : base(id, name) { }

    public static IReadOnlyList<ProcessingStage> Ordered =>
    [
        Upload, Validation, VirusScan, Metadata, Classification, Storage, Indexing
    ];
}
