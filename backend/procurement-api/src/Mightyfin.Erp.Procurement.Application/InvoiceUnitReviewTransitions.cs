using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;
using Rules = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewPolicyRules;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Bounded proposed application contracts only. Owning adapters must load all inputs
/// in one enlisted transaction. These plans install no routes, persistence, policy or verifier.</summary>
public static class InvoiceUnitReviewTransitions
{
    public const string RequestPermission = "procurement-invoice-unit-review-request";
    public const string ReadPermission = "procurement-invoice-unit-review-read";
    public sealed record OpenCommand(Guid Key, long ExpectedInvoiceVersion, int ExpectedMappingRevision,
        DateTimeOffset At, string? Reason);
    public sealed record DecisionCommand(Guid Key, long ExpectedInvoiceVersion, long ExpectedCaseVersion,
        long ExpectedAssignmentVersion, string Action, string? Reason, DateTimeOffset At);
    public sealed record Source(Guid ReviewId, long SourceVersion, Guid CoreCaseId, string TenantId, Guid LegalEntityId,
        Guid? BranchId, Guid InvoiceId, long InvoiceVersionAtOpen, Guid MappingId, int MappingRevision,
        string OriginalSha256, string MappingSha256, Guid PurchaseOrderId, Guid IssuedVersionId,
        string IssuedEvidenceSha256, Guid MatchingPolicyId, int MatchingPolicyVersion, string MatchingPolicySha256,
        Rules.Projection ReviewPolicy, DateTimeOffset OpenedAt, IReadOnlyList<InvoiceReviewedDocumentLine> Lines)
    {
        public int IdentitySchemaVersion { get; init; }
    }
    public sealed record OpeningPlan(Source Source, string SourceJson, string SourceSha256, ApprovalCase Case,
        IReadOnlyList<InvoiceUnitReviewAssignmentWorker> AssignmentWorkers, Guid CommandKey, string? Reason);

    public static OpeningPlan PrepareOpen(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        InvoiceMatchMappingVersion mapping, InvoiceIssuedOrder issued, PolicyVersion matchingPolicy, PolicyVersion reviewPolicy,
        Rules.Person maker, IReadOnlyList<Rules.Person> reviewers, IReadOnlyDictionary<Guid, string> ownedDocumentHashes,
        OpenCommand command)
    {
        Need(command.Key != Guid.Empty && command.At != default && command.ExpectedInvoiceVersion == invoice.Version
            && command.ExpectedMappingRevision == mapping.MappingRevision && invoice.Version > 0);
        CurrentMapping(invoice, originalLines, mapping);
        PersonScope(maker, invoice, RequestPermission);
        Need(maker.PrincipalId == mapping.ActorPrincipalId && ProcurementPrincipalEvidence.OptionalWorker(maker.WorkerId)
            && mapping.ReviewedAt != default && mapping.ReviewedAt <= command.At);
        var projection = Rules.Resolve(reviewPolicy, invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef,
            mapping.ActorPrincipalId, mapping.ActorWorkerId, reviewers, command.At);
        var matchingSha = MatchingPolicy(matchingPolicy, invoice, command.At);
        Need(matchingPolicy.Id != reviewPolicy.Id);
        var lines = DocumentLines(mapping, invoice, originalLines, ownedDocumentHashes);
        var now = ProcurementEvidenceEncoding.DatabaseTimestamp(command.At)!.Value;
        var source = new Source(Guid.CreateVersion7(), 1, Guid.CreateVersion7(), invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef,
            invoice.Id, invoice.Version, mapping.Id, mapping.MappingRevision, mapping.InvoiceOriginalSha256,
            mapping.MappingSha256, mapping.PurchaseOrderId, mapping.IssuedVersionId,
            IssuedFingerprint(issued, mapping, invoice, originalLines, command.At), matchingPolicy.Id,
            matchingPolicy.PolicyVersionNumber, matchingSha, projection, now, lines) { IdentitySchemaVersion = 3 };
        var workflow = new ApprovalCase { Id = source.CoreCaseId, TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            SourceModule = "procurement", SourceType = InvoiceRetainedUnitReview.ReviewSourceType,
            SourceId = source.ReviewId, SourceVersion = source.SourceVersion, PolicyVersionId = projection.PolicyId,
            PolicyVersionNumber = projection.PolicyVersion, MakerSubjectId = mapping.ActorPrincipalId, OpenedAt = now };
        var workers = new List<InvoiceUnitReviewAssignmentWorker>();
        foreach (var row in projection.Stages)
        {
            var stage = new ApprovalStage { TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
                CaseId = workflow.Id, Sequence = row.Sequence, Code = row.Code, Mode = row.CoreMode,
                RequiredApprovals = row.RequiredApprovals, State = row.Sequence == 1 ? "Active" : "Waiting" };
            foreach (var person in row.Assignees)
            {
                var assignment = new ApprovalAssignment { TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
                    CaseId = workflow.Id, StageId = stage.Id, AssigneeSubjectId = person.PrincipalId,
                    PermissionKey = InvoiceRetainedUnitReview.ReviewPermission };
                stage.Assignments.Add(assignment); workers.Add(new(assignment.Id, person.PrincipalId, person.WorkerId));
            }
            workflow.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(workflow); Rules.VerifyCaseGraph(projection, workflow, workers);
        var json = JsonSerializer.Serialize(source);
        return new(source, json, ProcurementEvidenceEncoding.Digest(json), workflow, workers.AsReadOnly(), command.Key, Reason(command.Reason));
    }

    /// <summary>New effects only; recover durable original-key receipts before invoking this.
    /// Core alone records the decision/quorum/state; this method never marks invoice units verified.</summary>
    public static ApprovalActionResult ApplyNewDecision(OpeningPlan owned, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMatchMappingVersion mapping, InvoiceIssuedOrder issued,
        PolicyVersion matchingPolicy, PolicyVersion reviewPolicy, Rules.Person actor,
        IReadOnlyDictionary<Guid, string> ownedDocumentHashes,
        Guid assignmentId, DecisionCommand command, ICollection<ApprovalDecision> decisions)
    {
        var source = owned.Source; var workflow = owned.Case;
        Need(command.Key != Guid.Empty && command.At >= source.OpenedAt && workflow.State == "Pending"
            && workflow.CompletedAt is null && workflow.CompletionKey is null
            && invoice.Version == command.ExpectedInvoiceVersion && workflow.Version == command.ExpectedCaseVersion);
        ValidateOwnedEvidence(owned, invoice, originalLines, mapping, issued, matchingPolicy, reviewPolicy, ownedDocumentHashes, command.At);
        PersonScope(actor, invoice, InvoiceRetainedUnitReview.ReviewPermission);
        Need(ProcurementPrincipalEvidence.Independent(actor.PrincipalId, actor.WorkerId, source.ReviewPolicy.MakerPrincipalId, source.ReviewPolicy.MakerWorkerId));
        var assignment = workflow.Stages.SelectMany(x => x.Assignments).SingleOrDefault(x => x.Id == assignmentId);
        Need(assignment is not null && assignment.Version == command.ExpectedAssignmentVersion
            && owned.AssignmentWorkers.Any(x => x.AssignmentId == assignmentId && x.PrincipalId == actor.PrincipalId)
            && command.Action is "Approve" or "Reject" or "Return");
        var reason = Reason(command.Reason); Need(command.Action == "Approve" || reason is not null);
        return ApprovalEngine.Decide(workflow, assignment!, new(actor.PrincipalId, actor.TenantId, actor.LegalEntityId,
            actor.Permissions), command.Action, command.Key.ToString("D"), reason, decisions,
            ProcurementEvidenceEncoding.DatabaseTimestamp(command.At)!.Value);
    }

    // Shared by new decisions and terminal sealing; never authenticates a public caller.
    public static void ValidateOwnedEvidence(OpeningPlan owned, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMatchMappingVersion mapping, InvoiceIssuedOrder issued,
        PolicyVersion matchingPolicy, PolicyVersion reviewPolicy, IReadOnlyDictionary<Guid, string> ownedDocumentHashes, DateTimeOffset at)
    {
        var source = owned.Source; var workflow = owned.Case;
        Need(source.IdentitySchemaVersion == 3 && at >= source.OpenedAt && source.CoreCaseId == workflow.Id && workflow.OpenedAt == source.OpenedAt
            && source.InvoiceVersionAtOpen > 0 && invoice.Version >= source.InvoiceVersionAtOpen
            && source.ReviewPolicy.MakerPrincipalId == mapping.ActorPrincipalId && source.ReviewPolicy.MakerWorkerId == mapping.ActorWorkerId
            && source.ReviewId == workflow.SourceId && source.SourceVersion == workflow.SourceVersion
            && source.SourceVersion == 1 && source.InvoiceId == invoice.Id && source.TenantId == invoice.TenantId
            && source.LegalEntityId == invoice.LegalEntityId && source.BranchId == invoice.BranchRef
            && source.MappingId == mapping.Id && source.MappingRevision == mapping.MappingRevision
            && source.MappingSha256 == mapping.MappingSha256 && source.OriginalSha256 == mapping.InvoiceOriginalSha256
            && source.PurchaseOrderId == mapping.PurchaseOrderId && source.IssuedVersionId == mapping.IssuedVersionId
            && owned.SourceJson == JsonSerializer.Serialize(source) && owned.SourceSha256 == ProcurementEvidenceEncoding.Digest(owned.SourceJson));
        CurrentMapping(invoice, originalLines, mapping);
        Rules.VerifyRetainedProjection(source.ReviewPolicy, reviewPolicy, at);
        Need(matchingPolicy.Id == source.MatchingPolicyId && matchingPolicy.PolicyVersionNumber == source.MatchingPolicyVersion
            && MatchingPolicy(matchingPolicy, invoice, at) == source.MatchingPolicySha256
            && IssuedFingerprint(issued, mapping, invoice, originalLines, at) == source.IssuedEvidenceSha256
            && DocumentLines(mapping, invoice, originalLines, ownedDocumentHashes).SequenceEqual(source.Lines));
        Rules.VerifyCaseGraph(source.ReviewPolicy, workflow, owned.AssignmentWorkers);
    }

    public static void VerifyIssuedEvidence(InvoiceIssuedOrder issued, InvoiceMatchMappingVersion mapping, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals, DateTimeOffset at, string expectedSha)
        => Need(IssuedFingerprint(issued, mapping, invoice, originals, at) == expectedSha);

    // This is an owner-produced immutable issued projection, not a public DTO or receiving-state loader.
    // Exclude operational header version/counters: no current receiving/capacity authority is asserted.
    private static string IssuedFingerprint(InvoiceIssuedOrder order, InvoiceMatchMappingVersion mapping,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals, DateTimeOffset at)
    {
        Need(order.Id == mapping.PurchaseOrderId && order.RevisionId == mapping.IssuedVersionId
            && order.TenantId == invoice.TenantId && order.LegalEntityId == invoice.LegalEntityId
            && order.BranchRef == invoice.BranchRef && order.SupplierId == invoice.SupplierId
            && order.CurrencyCode == invoice.CurrencyCode && order.RevisionState == "Issued"
            && order.RevisionNumber > 0 && !string.IsNullOrWhiteSpace(order.IssuedReference)
            && order.IssuedAt != default && order.IssuedAt <= at && order.Lines.Count is > 0 and <= 200
            && order.Lines.Select(x => x.Id).Distinct().Count() == order.Lines.Count);
        var links = InvoiceMappingReviewSourceDispatch.Verify(mapping, invoice, originals).Lines;
        Need(links.All(x => order.Lines.Any(line => line.Id == x.PurchaseOrderLineId)));
        return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new {
            order.Id, order.RevisionId, order.RevisionNumber, order.TenantId, order.LegalEntityId, order.BranchRef,
            order.SupplierId, order.CurrencyCode, order.IssuedReference, order.IssuedAt,
            lines = order.Lines.OrderBy(x => x.Id).Select(x => new { x.Id, x.CategoryCode, x.UnitCode,
                quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity), price = ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
                tax = ProcurementEvidenceEncoding.Decimal(x.TaxAmount) }).ToArray() }));
    }

    private static void CurrentMapping(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals, InvoiceMatchMappingVersion mapping)
    {
        Need(invoice.CurrentMappingVersionId == mapping.Id && invoice.MappingRevision == mapping.MappingRevision
            && invoice.FinancePostingRef is null && invoice.ActiveMatchClaimResultId is null
            && invoice.Status is "Received" or "MatchPending" or "Exception");
        _ = InvoiceMappingReviewSourceDispatch.Verify(mapping, invoice, originals);
    }
    private static string MatchingPolicy(PolicyVersion policy, SupplierInvoice invoice, DateTimeOffset now)
    {
        Need(policy.TenantId == invoice.TenantId && policy.LegalEntityId == invoice.LegalEntityId
            && policy.EffectiveFrom != default && policy.EffectiveFrom <= now && policy.PublishedAt <= now && (policy.EffectiveTo is null || now < policy.EffectiveTo));
        Need(InvoiceMatchingPolicyRules.Project(policy).Rules.All(x => x.CurrencyCode == invoice.CurrencyCode));
        return ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson);
    }
    private static IReadOnlyList<InvoiceReviewedDocumentLine> DocumentLines(InvoiceMatchMappingVersion mapping,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals, IReadOnlyDictionary<Guid, string> hashes)
    {
        var snapshot = InvoiceMappingReviewSourceDispatch.Verify(mapping, invoice, originals);
        Need(hashes is not null && hashes.Count == snapshot.Lines.Select(x => x.DocumentVersionId).Distinct().Count());
        var result = new List<InvoiceReviewedDocumentLine>();
        foreach (var line in snapshot.Lines.OrderBy(x => x.InvoiceLineId))
        {
            Need(line.DocumentVersionId != Guid.Empty && !string.IsNullOrWhiteSpace(line.DocumentLineReference)
                && hashes!.TryGetValue(line.DocumentVersionId, out var sha) && sha is { Length: 64 }
                && sha.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
            result.Add(new(line.InvoiceLineId, line.DocumentVersionId, hashes![line.DocumentVersionId], line.DocumentLineReference, line.InvoiceUnitCode));
        }
        return result.AsReadOnly();
    }
    private static void PersonScope(Rules.Person person, SupplierInvoice invoice, string action)
    {
        Need(person is not null && person.Active && ProcurementPrincipalEvidence.OptionalWorker(person.WorkerId)
            && Guid.TryParseExact(person.PrincipalId, "D", out var principal) && principal != Guid.Empty && person.PrincipalId == principal.ToString("D")
            && person.TenantId == invoice.TenantId && person.LegalEntityId == invoice.LegalEntityId
            && person.Permissions is not null && person.AllowedBranches is not null && !person.AllowedBranches.Contains(Guid.Empty)
            && invoice.BranchRef != Guid.Empty && person.Permissions.Contains("procurement-access") && person.Permissions.Contains(action)
            && (person.OrganisationWide || invoice.BranchRef is { } branch && person.AllowedBranches.Contains(branch)));
    }
    private static string? Reason(string? value)
    {
        var reason = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Need(reason.Length <= 2000); return reason.Length == 0 ? null : reason;
    }
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("InvoiceUnitReviewSourceChanged"); }
}
