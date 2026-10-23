using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Server-owned current Core projection. Not an HTTP input or a substitute for current authorization.</summary>
public sealed record PrincipalReceivingActor(string PrincipalId,Guid? WorkerAliasId,string TenantId,
    Guid LegalEntityId,IReadOnlySet<Guid> AllowedBranches,IReadOnlySet<string> Permissions);

/// <summary>Complete exact-source history supplied by owning persistence, never a paged UI result.</summary>
public sealed record PrincipalReceivingLedger(PrincipalReceivingSource Source,long Sequence,
    IReadOnlyList<PrincipalGoodsObservation> Observations,IReadOnlyList<PrincipalGoodsInspection> Inspections,
    IReadOnlyList<PrincipalReceivingAcceptance> Acceptances,IReadOnlyList<PolicyVersion> RetainedPolicies);
