# Subtree Specification Usage — Golden Rule A

**Spec**: 003-identity-access-organization — R6
**Contract**: `Organization.Infrastructure/Specifications/SubtreeSpecification<T>`

Every list/search/dashboard query that returns tenant-scoped resources MUST compose a subtree `Specification<T>` before fetching from the repository. Never filter after fetching.

```csharp
// In a query handler (e.g., GetWorkItemsQueryHandler)
var subtree = await hierarchy.GetSubtreeAsync(tenantId, actorId, ct); // IManagementHierarchy
var spec = new SubtreeSpecification<WorkItem>(subtree, tenantId, actorId, x => x.OwnerId)
    .And(new WorkItemByStatusSpecification(status))
    .And(new WorkItemTenantSpecification(tenantId));

var items = await repository.ListAsync(spec, ct); // EF translates Where to SQL with IN (subtree)
```

- `SubtreeSpecification<T>` is the only authorization filter — it ensures `ownerId IN subtree ∪ {actorId}` and `tenant_id == TenantId`.
- Compose via `And` with the resource query (status, project, classification) so the SQL is a single `WHERE` with `IN`.
- The `IsSatisfiedBy` helper (`SubtreeSpecificationTestHelper.IsOwnerInSubtree`) is for unit tests only; the `Where` expression is for EF.

**Tests**: `tests/Organization.Tests/Domain/SubtreeSpecificationTests.cs` (not yet created — see T028) and `tests/Organization.Tests/Integration/SubtreeFilteredQueryTests.cs` prove the composition.

**References**: `specs/003-identity-access-organization/contracts/authorization-contract.md`, `data-model.md` (SubtreeScope), `quickstart.md` step 2.
