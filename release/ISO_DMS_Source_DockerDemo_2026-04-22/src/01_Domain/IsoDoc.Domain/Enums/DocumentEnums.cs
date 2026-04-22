namespace IsoDoc.Domain.Enums;

/// <summary>
/// Document lifecycle states.
/// State transitions enforced in Document entity methods.
///
///   Draft → UnderReview → PendingFinalApproval → Published → Archived
///                ↓                  ↓
///           Rejected ←←←←←←←←←←←←←←←←←←←←←←←←←←←←←
/// </summary>
public enum DocumentStatus
{
    Draft = 1,
    UnderReview = 2,
    PendingFinalApproval = 3,
    Published = 4,
    Rejected = 5,
    Archived = 6
}

/// <summary>
/// ISO standard scope. Documents are tagged with exactly one standard.
/// RBAC roles are also scoped per standard (e.g., QAOfficer can only manage ISO9001 docs).
/// </summary>
public enum IsoStandard
{
    ISO9001 = 1,
    ISO45001 = 2,
    ISO27001 = 3
}

/// <summary>
/// Document type / category for classification and search filtering.
/// </summary>
public enum DocumentCategory
{
    Policy = 1,
    Procedure = 2,
    WorkInstruction = 3,
    Form = 4,
    Record = 5,
    Manual = 6,
    Specification = 7
}

/// <summary>
/// Decision recorded at each approval step.
/// </summary>
public enum WorkflowDecision
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Supported file types for document uploads.
/// </summary>
public enum DocumentFileType
{
    Pdf = 1,
    Docx = 2,
    Xlsx = 3,
    Pptx = 4
}

public enum WorkflowStatus
{
    InProgress = 1,
    Approved = 2,
    Rejected = 3
}

