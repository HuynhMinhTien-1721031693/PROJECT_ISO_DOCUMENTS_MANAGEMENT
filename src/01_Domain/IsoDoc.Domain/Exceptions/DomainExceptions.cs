using System;
using IsoDoc.Domain.Enums;

namespace IsoDoc.Domain.Exceptions;

/// <summary>Base class for all domain-layer exceptions.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a state transition on Document is attempted that violates workflow rules.
/// </summary>
public sealed class InvalidDocumentWorkflowStateException : DomainException
{
    public Guid DocumentId { get; }
    public DocumentStatus CurrentStatus { get; }
    public DocumentStatus AttemptedStatus { get; }

    public InvalidDocumentWorkflowStateException(
        Guid documentId,
        DocumentStatus current,
        DocumentStatus attempted,
        string message)
        : base($"Document {documentId}: Cannot transition from {current} to {attempted}. {message}")
    {
        DocumentId = documentId;
        CurrentStatus = current;
        AttemptedStatus = attempted;
    }
}

/// <summary>Thrown when a Document is not found by ID or Code.</summary>
public sealed class DocumentNotFoundException : DomainException
{
    public DocumentNotFoundException(Guid id)
        : base($"Document with ID '{id}' was not found.") { }

    public DocumentNotFoundException(string code)
        : base($"Document with code '{code}' was not found.") { }
}

/// <summary>
/// Thrown when a user attempts to approve/reject a step they are not assigned to.
/// </summary>
public sealed class UnauthorizedWorkflowAccessException : DomainException
{
    public UnauthorizedWorkflowAccessException(Guid userId, int stepOrder)
        : base($"User '{userId}' is not the assigned approver for step {stepOrder}.") { }
}

/// <summary>
/// Thrown when a user attempts an action they don't have RBAC permission for.
/// </summary>
public sealed class InsufficientPermissionException : DomainException
{
    public InsufficientPermissionException(Guid userId, string requiredPermission)
        : base($"User '{userId}' does not have the required permission: '{requiredPermission}'.") { }
}

