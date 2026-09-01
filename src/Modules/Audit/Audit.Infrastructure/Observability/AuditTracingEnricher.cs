using System.Diagnostics;
namespace Audit.Infrastructure.Observability;
public sealed class AuditTracingEnricher
{
    public void Enrich(Activity activity, Guid auditId, Guid correlationId, string action)
    {
        activity?.SetTag("audit.auditId", auditId.ToString());
        activity?.SetTag("audit.correlationId", correlationId.ToString());
        activity?.SetTag("audit.action", action);
    }
}
