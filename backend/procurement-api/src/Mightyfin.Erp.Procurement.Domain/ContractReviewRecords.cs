namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>One immutable submission of exact draft terms to a shared Core case.
/// ReviewBranchId is explicit because the contract header is company scoped.</summary>
public sealed class ContractReviewSubmission : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ContractId { get; init; }
    public Guid ContractVersionId { get; init; }
    public long ContractBusinessVersion { get; init; }
    public long ContractVersionBusinessVersion { get; init; }
    public int SubmissionNumber { get; init; }
    public Guid ReviewBranchId { get; init; }
    public Guid SupplierId { get; init; }
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public string PolicyKey { get; init; } = "SUPPLIER_CONTRACT_REVIEW";
    public int PolicyVersionNumber { get; init; }
    public string PolicyRulesSha256 { get; init; } = null!;
    public Guid CoreCaseId { get; init; }
    public string AssignmentPinsJson { get; set; } = null!;
    public string AssignmentPinsSha256 { get; set; } = null!;
    public string TermsJson { get; init; } = null!;
    public string TermsSha256 { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
}

/// <summary>Append-only link to an actual Core vote. Outcome is derived from the
/// complete Core case; a single vote never proves terminal approval.</summary>
public sealed class ContractReviewVote : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid ContractId { get; init; }
    public Guid ReviewBranchId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreStageId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public string Action { get; init; } = null!;
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Original actor-owned HTTP acknowledgement. Recovery must authorize the
/// current actor, original action, company and branch before returning ResponseJson.</summary>
public sealed class ContractReviewCommandReceipt : ProcurementRecord
{
    public Guid ContractId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid ReviewBranchId { get; init; }
    public Guid? VoteId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public string CommandSha256 { get; init; } = null!;
    // Filled from the original command result before the append-only SaveChanges.
    public string ResponseJson { get; set; } = null!;
    public string ResponseSha256 { get; set; } = null!;
    public int ResponseStatus { get; init; }
    public long ResultContractVersion { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}
