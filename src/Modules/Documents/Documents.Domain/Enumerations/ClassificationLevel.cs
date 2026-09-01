using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Documents.Domain.Enumerations;

public sealed class ClassificationLevel : Enumeration<ClassificationLevel>
{
    public static readonly ClassificationLevel Public = new(1, nameof(Public));
    public static readonly ClassificationLevel Internal = new(2, nameof(Internal));
    public static readonly ClassificationLevel Confidential = new(3, nameof(Confidential));
    public static readonly ClassificationLevel Restricted = new(4, nameof(Restricted));
    public static readonly ClassificationLevel HighlyRestricted = new(5, nameof(HighlyRestricted));

    // Org extensions start at 101
    public static ClassificationLevel FromOrgExtension(int id, string name) => new(id, name);

    private ClassificationLevel(int id, string name) : base(id, name) { }

    public bool IsMoreSensitiveThan(ClassificationLevel other) => Id > other.Id;
}
