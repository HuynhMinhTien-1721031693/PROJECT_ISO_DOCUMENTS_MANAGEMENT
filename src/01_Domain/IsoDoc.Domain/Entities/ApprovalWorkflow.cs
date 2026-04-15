using System;
using System.Collections.Generic;
using System.Linq;
using IsoDoc.Domain.Common;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Events;

namespace IsoDoc.Domain.Entities;

/// <summary>
/// Manages the multi-step approval workflow for a document version.
/// Default is 2 steps (review by ISO officer, then ISO manager sign-off).
/// </summary>
public sealed class ApprovalWorkflow : BaseEntity
{
    // Non-readonly so EF Core can materialize/replace the collection when loading aggregates.
    private List<ApprovalStep> _steps = new();

    public Guid DocumentId { get; private set; }
    public Guid VersionId { get; private set; }
    public int CurrentStepOrder { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<ApprovalStep> Steps => _steps.AsReadOnly();

    public ApprovalStep? CurrentStep =>
        _steps.SingleOrDefault(s => s.StepOrder == CurrentStepOrder && s.Decision == WorkflowDecision.Pending);

    public bool IsCompleted => Status is WorkflowStatus.Approved or WorkflowStatus.Rejected;

    private ApprovalWorkflow()
    {
        // For ORM
    }

    /// <summary>
    /// Creates a new 2-step workflow.
    /// Step 1 approver = officer (QA/Safety/ISMS based on ISO standard).
    /// Step 2 approver = ISO Manager.
    /// </summary>
    public static ApprovalWorkflow Create(
        Guid documentId,
        Guid versionId,
        Guid step1ApproverId,
        Guid step2ApproverId)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("documentId is required.", nameof(documentId));
        if (versionId == Guid.Empty) throw new ArgumentException("versionId is required.", nameof(versionId));
        if (step1ApproverId == Guid.Empty) throw new ArgumentException("step1ApproverId is required.", nameof(step1ApproverId));
        if (step2ApproverId == Guid.Empty) throw new ArgumentException("step2ApproverId is required.", nameof(step2ApproverId));

        var workflow = new ApprovalWorkflow
        {
            DocumentId = documentId,
            VersionId = versionId,
            CurrentStepOrder = 1,
            Status = WorkflowStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        workflow._steps.Add(ApprovalStep.Create(workflow.Id, 1, step1ApproverId));
        workflow._steps.Add(ApprovalStep.Create(workflow.Id, 2, step2ApproverId));

        return workflow;
    }

    /// <summary>
    /// Records a decision for the current step.
    /// - Approved: advance or complete (Approved)
    /// - Rejected: immediately mark as Rejected
    /// </summary>
    public void RecordDecision(Guid approverId, WorkflowDecision decision, string? comment = null)
    {
        if (IsCompleted)
            throw new IsoDoc.Domain.Exceptions.DomainException("Cannot record a decision on a completed workflow.");

        var step = _steps.SingleOrDefault(s => s.StepOrder == CurrentStepOrder);
        if (step is null)
            throw new IsoDoc.Domain.Exceptions.DomainException($"No step found at order {CurrentStepOrder}.");

        if (step.ApproverId != approverId)
            throw new IsoDoc.Domain.Exceptions.UnauthorizedWorkflowAccessException(approverId, CurrentStepOrder);

        step.RecordDecision(decision, comment);

        if (decision == WorkflowDecision.Rejected)
        {
            Status = WorkflowStatus.Rejected;
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new WorkflowRejectedEvent(DocumentId, VersionId, approverId, comment ?? ""));
            return;
        }

        var nextStep = _steps
            .Where(s => s.StepOrder > CurrentStepOrder)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault();

        if (nextStep is null)
        {
            Status = WorkflowStatus.Approved;
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new WorkflowCompletedEvent(DocumentId, VersionId));
        }
        else
        {
            CurrentStepOrder = nextStep.StepOrder;
            AddDomainEvent(new WorkflowStepAdvancedEvent(DocumentId, nextStep.ApproverId, nextStep.StepOrder));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Child entity: ApprovalStep (owned by workflow; immutable after decided)
    // ──────────────────────────────────────────────────────────────────────────

    public sealed class ApprovalStep : BaseEntity
    {
        public Guid WorkflowId { get; private set; }
        public int StepOrder { get; private set; }
        public Guid ApproverId { get; private set; }
        public WorkflowDecision Decision { get; private set; }
        public string? Comment { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        private ApprovalStep()
        {
            // For ORM
        }

        internal static ApprovalStep Create(Guid workflowId, int stepOrder, Guid approverId)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("workflowId is required.", nameof(workflowId));
            if (stepOrder <= 0) throw new ArgumentOutOfRangeException(nameof(stepOrder));
            if (approverId == Guid.Empty) throw new ArgumentException("approverId is required.", nameof(approverId));

            return new ApprovalStep
            {
                WorkflowId = workflowId,
                StepOrder = stepOrder,
                ApproverId = approverId,
                Decision = WorkflowDecision.Pending,
                Comment = null,
                DecidedAt = null
            };
        }

        internal void RecordDecision(WorkflowDecision decision, string? comment)
        {
            if (Decision != WorkflowDecision.Pending)
                throw new IsoDoc.Domain.Exceptions.DomainException("Decision has already been recorded for this step.");

            Decision = decision;
            Comment = comment;
            DecidedAt = DateTime.UtcNow;
        }
    }
}

