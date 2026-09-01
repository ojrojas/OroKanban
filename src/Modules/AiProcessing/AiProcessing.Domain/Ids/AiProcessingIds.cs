using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace AiProcessing.Domain.Ids;

public sealed record LlmOperationId(Guid Value) : StronglyTypedId<Guid>(Value);
public sealed record LlmPromptVersionId(Guid Value) : StronglyTypedId<Guid>(Value);
public sealed record LlmResultId(Guid Value) : StronglyTypedId<Guid>(Value);
public sealed record LlmReviewId(Guid Value) : StronglyTypedId<Guid>(Value);
