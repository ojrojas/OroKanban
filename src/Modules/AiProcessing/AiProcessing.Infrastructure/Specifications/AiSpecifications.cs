using BuildingBlocks.Kernel.Domain.Specifications;
using AiProcessing.Domain.Aggregates;
using AiProcessing.Infrastructure.Persistence.Configurations;

namespace AiProcessing.Infrastructure.Specifications;

public sealed class LlmOperationByTenantSpec : Specification<LlmOperation>
{
    public LlmOperationByTenantSpec(Guid tenantId, Guid operationId)
    {
        Where(o => o.TenantId == tenantId && o.Id.Value == operationId);
    }
}

public sealed class LlmPromptVersionByOperationTypeSpec : Specification<LlmPromptVersion>
{
    public LlmPromptVersionByOperationTypeSpec(int operationTypeId)
    {
        Where(p => p.OperationTypeId == operationTypeId);
    }
}

public sealed class LlmResultByDocumentVersionSpec : Specification<LlmResult>
{
    public LlmResultByDocumentVersionSpec(Guid tenantId, Guid documentVersionId)
    {
        Where(r => r.TenantId == tenantId && r.DocumentVersionId == documentVersionId);
    }
}

public sealed class PendingReviewSpec : Specification<LlmResult>
{
    public PendingReviewSpec(Guid tenantId)
    {
        Where(r => r.TenantId == tenantId && r.ReviewStatusId == 2);
    }
}

public sealed class ChunkByTenantAndClassificationSpec : Specification<ChunkReferenceEntity>
{
    public ChunkByTenantAndClassificationSpec(Guid tenantId, string classification)
    {
        Where(c => c.TenantId == tenantId && c.Classification == classification && c.IsSafe);
    }
}
