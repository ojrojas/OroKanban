namespace Projects.Contracts;

public interface IProjectMembership
{
    Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken ct);
}