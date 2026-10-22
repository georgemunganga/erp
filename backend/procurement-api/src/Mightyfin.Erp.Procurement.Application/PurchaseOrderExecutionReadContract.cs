using System.Collections.ObjectModel;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Current Core-resolved identity, action grants and record scope; never a client claim.</summary>
public sealed record PurchaseOrderExecutionReadActor(string TenantId, Guid LegalEntityId, string PrincipalId,
    Guid WorkerId, bool Active, Guid? SelectedBranchId, IReadOnlySet<Guid> BranchIds, IReadOnlySet<string> Permissions);

public sealed record PurchaseOrderExecutionReadFacts
{
    public string TenantId { get; init; } = "";
    public Guid LegalEntityId { get; init; }
    public Guid? BranchId { get; init; }
    public string BuyerPrincipalId { get; init; } = "";
    public string Status { get; init; } = "";
    public bool SourceControlled { get; init; }
    public bool AssignedToActor { get; init; }
    public bool PendingAssignmentToActor { get; init; }
    public bool RecordedFinanceActor { get; init; }
    public bool RecordedDispatchActor { get; init; }
    public bool FinanceIndependent { get; init; }
    public bool FinanceReleaseIndependent { get; init; }
    public bool DispatchIndependent { get; init; }
    public bool HasIssuedVersion { get; init; }
    public bool HasSubmission { get; init; }
    public bool DraftReopened { get; init; }
    public bool FinanceHeld { get; init; }
    public bool CurrentFinanceEvidence { get; init; }
    public bool CurrentFinancePolicy { get; init; }
    public bool CurrentDispatchPolicy { get; init; }
    public bool PublishedOrderPolicy { get; init; }
}

/// <summary>A candidate allows opening an action form, not executing or approving a command.
/// Commands independently revalidate complete sources, grants, amount, version and evidence.</summary>
public sealed record PurchaseOrderActionCandidate(string Code, string Permission, bool CanStart,
    IReadOnlyList<string> Blockers, bool RequiresCommandValidation = true);

public static class PurchaseOrderExecutionReadContract
{
    public static readonly IReadOnlyDictionary<string, string> OperationPermissions = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Convert"] = "procurement-po-convert", ["SourceEdit"] = "procurement-po-edit",
        ["CancelDraft"] = "procurement-po-edit", ["Finance"] = "procurement-po-finance-confirm",
        ["DraftFinanceRelease"] = "procurement-po-finance-confirm", ["FinanceRelease"] = "procurement-po-finance-confirm",
        ["Submit"] = "procurement-po-submit", ["Decide"] = "procurement-po-approve",
        ["Issue"] = "procurement-po-issue", ["Delivery"] = "procurement-po-dispatch-confirm",
    });

    public static bool InScope(PurchaseOrderExecutionReadActor actor, PurchaseOrderExecutionReadFacts facts) =>
        actor.Active && actor.WorkerId != Guid.Empty && !string.IsNullOrWhiteSpace(actor.PrincipalId)
        && actor.TenantId == facts.TenantId && actor.LegalEntityId == facts.LegalEntityId
        && (!actor.SelectedBranchId.HasValue || actor.SelectedBranchId == facts.BranchId)
        && (actor.BranchIds.Count == 0 || facts.BranchId.HasValue && actor.BranchIds.Contains(facts.BranchId.Value));

    public static bool CanRead(PurchaseOrderExecutionReadActor actor, PurchaseOrderExecutionReadFacts facts) =>
        InScope(actor, facts) && facts.SourceControlled && (
            actor.PrincipalId == facts.BuyerPrincipalId && Has(actor, "procurement-po-read")
            || facts.AssignedToActor && Has(actor, "procurement-po-approve")
            || Has(actor, "procurement-po-finance-confirm") && (facts.Status is "Draft" or "Returned" or "Rejected" || facts.RecordedFinanceActor)
            || Has(actor, "procurement-po-dispatch-confirm") && facts.HasIssuedVersion
                && (facts.Status is "IssuePending" or "Issued" || facts.RecordedDispatchActor));

    public static IReadOnlyList<PurchaseOrderActionCandidate> Actions(PurchaseOrderExecutionReadActor actor,
        PurchaseOrderExecutionReadFacts facts)
    {
        if (!CanRead(actor, facts)) return [];
        var own = actor.PrincipalId == facts.BuyerPrincipalId;
        var draft = facts.Status == "Draft";
        var editable = draft && !facts.FinanceHeld && (!facts.HasSubmission || facts.DraftReopened);
        var actions = new List<PurchaseOrderActionCandidate>();
        Candidate("SourceEdit", "procurement-po-edit", own && editable, "UnfundedEditableBuyerDraftRequired");
        Candidate("CancelDraft", "procurement-po-edit", own && draft && !facts.FinanceHeld, "UnfundedBuyerDraftRequired");
        Candidate("Finance", "procurement-po-finance-confirm", draft && facts.FinanceIndependent
            && facts.CurrentFinancePolicy, "IndependentFinanceActorAndApprovedPolicyRequired");
        Candidate("DraftFinanceRelease", "procurement-po-finance-confirm", draft && facts.FinanceHeld
            && facts.FinanceIndependent && facts.CurrentFinancePolicy, "HeldDraftCommitmentAndIndependentFinancePolicyRequired");
        Candidate("Submit", "procurement-po-submit", own && draft && facts.CurrentFinanceEvidence
            && facts.PublishedOrderPolicy && facts.CurrentFinancePolicy, "CurrentFinanceEvidenceAndPublishedOrderPolicyRequired");
        Candidate("Decide", "procurement-po-approve", facts.Status == "PendingApproval" && facts.PendingAssignmentToActor,
            "ActiveAssignedApprovalRequired");
        Candidate("FinanceRelease", "procurement-po-finance-confirm", facts.Status is "Returned" or "Rejected"
            && facts.FinanceHeld && facts.FinanceReleaseIndependent && facts.CurrentFinancePolicy,
            "TerminalApprovalHeldCommitmentAndIndependentFinancePolicyRequired");
        Candidate("Issue", "procurement-po-issue", own && facts.Status == "Approved" && facts.CurrentFinanceEvidence
            && facts.CurrentFinancePolicy, "ApprovedBuyerOrderAndCurrentFinanceEvidenceRequired");
        Candidate("Delivery", "procurement-po-dispatch-confirm", facts.Status == "IssuePending" && facts.HasIssuedVersion
            && facts.DispatchIndependent && facts.CurrentDispatchPolicy, "IssuedVersionIndependentVerifierAndDispatchPolicyRequired");
        return actions;

        void Candidate(string code, string permission, bool eligible, string blocker)
        {
            if (!Has(actor, permission)) return;
            actions.Add(new(code, permission, eligible, eligible ? [] : [blocker]));
        }
    }

    public static bool Has(PurchaseOrderExecutionReadActor actor, string permission) =>
        actor.Permissions.Any(x => string.Equals(x, permission, StringComparison.Ordinal));
}

/// <summary>Exact decimal transport. SQL numeric(38,8) is wider than CLR decimal: never silently
/// round a product or aggregate that cannot be represented. Legacy draft transport is unchanged.</summary>
public static class PurchaseOrderExecutionReadAmounts
{
    public static (string LineBase, string Gross) Calculate(IEnumerable<PurchaseOrderLine> source)
    {
        var lines = source.ToArray();
        var products = lines.Select(x => ExactDecimalArithmetic.Multiply(x.Quantity, x.UnitPrice)).ToArray();
        return (ProcurementEvidenceEncoding.Decimal(ExactDecimalArithmetic.Sum(products)),
            ProcurementEvidenceEncoding.Decimal(ExactDecimalArithmetic.Sum(products.Concat(lines.Select(x => x.TaxAmount)))));
    }
}
