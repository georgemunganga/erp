namespace Mightyfin.Erp.Core.Approvals.Domain;

public interface IApprovalScope
{
    string TenantId { get; }
    Guid LegalEntityId { get; }
    string SubjectId { get; }
}
