using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Common;
public static class AuthorizationErrorMapper { public static Error Map(bool isTenantMismatch) => isTenantMismatch ? Error.NotFound("Document.NotFound","Document not found.") : Error.Forbidden("Document.Forbidden","Access denied."); }
