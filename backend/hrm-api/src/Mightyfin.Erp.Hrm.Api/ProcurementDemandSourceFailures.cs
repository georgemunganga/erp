using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Transport mapping stays in the API; the shared owner loader knows typed failures only.</summary>
internal static class ProcurementDemandSourceFailures
{
    internal static (string Code, int Status) ForOrder(ApprovedDemandLoadFailure failure) => failure switch
    {
        ApprovedDemandLoadFailure.SourceNotFound => ("OrderSourceNotFound", 404),
        ApprovedDemandLoadFailure.SourceOutsideScope => ("OrderNotFound", 404),
        ApprovedDemandLoadFailure.SourceNotApproved => ("OrderSourceNotApproved", 409),
        ApprovedDemandLoadFailure.ApprovalEvidenceMissing => ("OrderSourceApprovalEvidenceMissing", 409),
        ApprovedDemandLoadFailure.CoreCaseMissing => ("OrderSourceCaseMissing", 409),
        ApprovedDemandLoadFailure.CoreDecisionMissing => ("OrderSourceDecisionMissing", 409),
        ApprovedDemandLoadFailure.MethodUnavailable => ("OrderSourceMethodUnavailable", 409),
        _ => throw new InvalidOperationException("Unknown approved-demand load failure."),
    };
    internal static (string Code, int Status) ForSourcing(ApprovedDemandLoadFailure failure) => failure switch
    {
        ApprovedDemandLoadFailure.SourceNotFound => ("SourcingSourceNotFound", 404),
        ApprovedDemandLoadFailure.SourceOutsideScope => ("SourcingNotFound", 404),
        ApprovedDemandLoadFailure.SourceNotApproved => ("SourcingSourceNotApproved", 409),
        ApprovedDemandLoadFailure.ApprovalEvidenceMissing => ("SourcingSourceApprovalEvidenceMissing", 409),
        ApprovedDemandLoadFailure.CoreCaseMissing => ("SourcingSourceCaseMissing", 409),
        ApprovedDemandLoadFailure.CoreDecisionMissing => ("SourcingSourceDecisionMissing", 409),
        ApprovedDemandLoadFailure.MethodUnavailable => ("SourcingSourceMethodUnavailable", 409),
        _ => throw new InvalidOperationException("Unknown approved-demand load failure."),
    };
}
