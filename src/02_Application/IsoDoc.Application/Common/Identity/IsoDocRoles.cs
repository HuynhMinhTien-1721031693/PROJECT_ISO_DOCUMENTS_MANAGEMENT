namespace IsoDoc.Application.Common.Identity;

public static class IsoDocRoles
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        SystemAdmin,
        ISOManager,
        DocumentController,
        QAOfficer,
        SafetyOfficer,
        ISMSOfficer
    };

    public const string SystemAdmin = "SystemAdmin";
    public const string ISOManager = "ISOManager";
    public const string DocumentController = "DocumentController";
    public const string QAOfficer = "QAOfficer";
    public const string SafetyOfficer = "SafetyOfficer";
    public const string ISMSOfficer = "ISMSOfficer";
}
