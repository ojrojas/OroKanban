using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Audit.Domain.Enumerations;

public sealed class AuditActorType : Enumeration<AuditActorType>
{
    public static readonly AuditActorType User = new(1, nameof(User));
    public static readonly AuditActorType System = new(2, nameof(System));
    public static readonly AuditActorType Anonymous = new(3, nameof(Anonymous));
    private AuditActorType(int id, string name) : base(id, name) { }
}

public sealed class AuditResultType : Enumeration<AuditResultType>
{
    public static readonly AuditResultType Success = new(1, nameof(Success));
    public static readonly AuditResultType Denied = new(2, nameof(Denied));
    public static readonly AuditResultType Failed = new(3, nameof(Failed));
    private AuditResultType(int id, string name) : base(id, name) { }
}
