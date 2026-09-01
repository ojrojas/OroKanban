using System.Text.Json;

using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using Documents.Domain.Enumerations;
using Documents.Domain.Events;
using Documents.Domain.Ids;
using Documents.Domain.Rules;

namespace Documents.Domain.Aggregates;

public enum StageStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    FailedRetryable = 3,
    FailedPermanent = 4
}

public sealed class StageState
{
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class DocumentProcessingJob : AggregateRoot<DocumentProcessingJobId>
{
    public DocumentId DocumentId { get; private set; } = default!;
    public DocumentVersionId DocumentVersionId { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public int CurrentStageId { get; private set; }
    public Dictionary<int, StageState> StageStates { get; private set; } = new();
    public StageStatus OverallStatus { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public int? LastErrorStageId { get; private set; }
    public string? RuleVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private const int MaxAttempts = 3;

    private DocumentProcessingJob() { }

    private DocumentProcessingJob(DocumentProcessingJobId id, DocumentId docId, DocumentVersionId versionId, Guid tenantId, int currentStageId)
    {
        Id = id;
        DocumentId = docId;
        DocumentVersionId = versionId;
        TenantId = tenantId;
        CurrentStageId = currentStageId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        OverallStatus = StageStatus.Pending;
        foreach (var stage in ProcessingStage.Ordered)
        {
            StageStates[stage.Id] = new StageState
            {
                Status = stage.Id == ProcessingStage.Upload.Id ? StageStatus.Succeeded : StageStatus.Pending,
                AttemptCount = stage.Id == ProcessingStage.Upload.Id ? 1 : 0
            };
        }
        // If starting at Validation, Upload already succeeded
        if (currentStageId == ProcessingStage.Validation.Id)
        {
            StageStates[ProcessingStage.Upload.Id].Status = StageStatus.Succeeded;
        }
    }

    public static DocumentProcessingJob Create(DocumentId docId, DocumentVersionId versionId, Guid tenantId)
    {
        return new DocumentProcessingJob(DocumentProcessingJobId.New(), docId, versionId, tenantId, ProcessingStage.Validation.Id);
    }

    public string CurrentStageName => ProcessingStage.FromId(CurrentStageId).Name;

    public void MarkSucceeded(ProcessingStage stage)
    {
        if (StageStates[stage.Id].Status == StageStatus.Succeeded) return; // idempotent
        StageStates[stage.Id].Status = StageStatus.Succeeded;
        StageStates[stage.Id].UpdatedAt = DateTime.UtcNow;
        StageStates[stage.Id].LastError = null;
        UpdatedAt = DateTime.UtcNow;
        // advance CurrentStage to next pending stage if this was current
        if (stage.Id == CurrentStageId)
        {
            var next = ProcessingStage.Ordered.FirstOrDefault(s => StageStates[s.Id].Status == StageStatus.Pending);
            if (next is not null)
                CurrentStageId = next.Id;
            else
            {
                OverallStatus = StageStatus.Succeeded;
                CompletedAt = DateTime.UtcNow;
            }
        }
        RecalculateOverall();
        RaiseDomainEvent(new DocumentProcessingStageCompletedDomainEvent(Id.Value, stage.Name));
    }

    public void MarkFailed(ProcessingStage stage, string reason, bool retryable = true)
    {
        var state = StageStates[stage.Id];
        state.AttemptCount++;
        state.LastError = reason;
        state.UpdatedAt = DateTime.UtcNow;
        LastError = reason;
        LastErrorStageId = stage.Id;
        AttemptCount = state.AttemptCount;
        UpdatedAt = DateTime.UtcNow;

        if (state.AttemptCount >= MaxAttempts)
            state.Status = StageStatus.FailedPermanent;
        else
            state.Status = retryable ? StageStatus.FailedRetryable : StageStatus.FailedPermanent;

        RecalculateOverall();
        RaiseDomainEvent(new DocumentProcessingFailedDomainEvent(Id.Value, stage.Name, reason, state.Status == StageStatus.FailedRetryable, state.AttemptCount));
    }

    public Result RetryStage(ProcessingStage stage)
    {
        if (!StageStates.ContainsKey(stage.Id))
            return Result.Failure(Error.Validation("Job.StageUnknown", $"Stage {stage.Name} unknown."));
        var state = StageStates[stage.Id];
        if (state.Status == StageStatus.Succeeded)
            return Result.Failure(Error.Failure("Job.AlreadySucceeded", $"Stage {stage.Name} already succeeded."));
        if (OverallStatus == StageStatus.Succeeded)
            return Result.Failure(Error.Failure("Job.AlreadySucceeded", "Job already succeeded."));
        if (state.Status != StageStatus.FailedRetryable && state.Status != StageStatus.FailedPermanent && state.Status != StageStatus.Pending)
            return Result.Failure(Error.Failure("Job.NotRetryable", $"Stage {stage.Name} is {state.Status}, not retryable."));

        var rule = new StageIsRetryableRule(state.Status == StageStatus.FailedRetryable || state.Status == StageStatus.FailedPermanent);
        if (rule.IsBroken())
            return Result.Failure(Error.Failure("Job.RetryNotAllowed", rule.Message));

        state.Status = StageStatus.Pending;
        state.LastError = null;
        state.UpdatedAt = DateTime.UtcNow;
        CurrentStageId = stage.Id;
        LastError = null;
        LastErrorStageId = null;
        UpdatedAt = DateTime.UtcNow;
        RecalculateOverall();
        return Result.Success();
    }

    private void RecalculateOverall()
    {
        if (StageStates.Values.Any(s => s.Status == StageStatus.FailedPermanent))
            OverallStatus = StageStatus.FailedPermanent;
        else if (StageStates.Values.Any(s => s.Status == StageStatus.FailedRetryable))
            OverallStatus = StageStatus.FailedRetryable;
        else if (StageStates.Values.All(s => s.Status == StageStatus.Succeeded))
            OverallStatus = StageStatus.Succeeded;
        else if (StageStates.Values.Any(s => s.Status == StageStatus.InProgress))
            OverallStatus = StageStatus.InProgress;
        else
            OverallStatus = StageStatus.Pending;
    }

    public string StageStatesJson
    {
        get => JsonSerializer.Serialize(StageStates);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            StageStates = JsonSerializer.Deserialize<Dictionary<int, StageState>>(value) ?? new();
        }
    }
}
