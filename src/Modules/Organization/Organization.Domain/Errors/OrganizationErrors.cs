using BuildingBlocks.Kernel.Domain.Results;

namespace Organization.Domain.Errors;

public static class OrganizationErrors
{
    public static Error CycleDetected(Guid managerId, Guid subordinateId) =>
        Error.Validation("Organization.CycleDetected", $"Relationship {managerId} → {subordinateId} would create a cycle — manager is already a descendant of subordinate.");

    public static Error SelfReference(Guid userId) =>
        Error.Validation("Organization.SelfReference", $"Manager and subordinate cannot be the same user ({userId}).");

    public static Error DuplicateActiveRelationship(Guid subordinateId, Guid unitId) =>
        Error.Conflict("Organization.DuplicateActiveRelationship", $"Subordinate {subordinateId} already has an active manager in unit {unitId}.");

    public static Error GrantExpired(Guid grantId) =>
        Error.Validation("Organization.GrantExpired", $"Grant {grantId} has expired.");

    public static Error NotInSubtree(Guid managerId, Guid userId) =>
        Error.Forbidden("Organization.NotInSubtree", $"User {userId} is not in subtree of {managerId}.");
}