using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Flow = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewTransitions;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceManualUnitReviewEvidenceTests
{
    [Fact]
    public void Explicit_manual_policy_uses_real_Core_decision_and_no_document_claim()
    {
        var f = Manual();
        var plan = Open(f);
        Assert.Equal(4, plan.Source.SourceSchemaVersion);
        Assert.Empty(plan.Source.Lines);
        Assert.Equal("Entered unit checked", plan.Source.ManualReason);
        Assert.DoesNotContain("documentVersionId", plan.SourceJson, StringComparison.OrdinalIgnoreCase);
        var task = Assert.Single(plan.Case.Stages.Single().Assignments);
        var actor = Assert.Single(f.Policy.People);
        var decisions = new List<ApprovalDecision>();
        Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued,
            f.Matching, f.Policy.Policy, actor, f.Hashes, task.Id,
            new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version, task.Version,
                "Approve", "I checked the entered unit", f.Policy.Now.AddMinutes(1)), decisions);
        var terminal = InvoiceUnitReviewTerminal.Seal(plan, f.Invoice, [f.Line],
            f.Mapping, f.Issued, f.Matching, f.Policy.Policy, f.Hashes,
            decisions, decisions.Single().Id, f.Policy.Now.AddMinutes(2));
        Assert.Equal(4, terminal.EvidenceSchemaVersion);
        Assert.Equal("Verified", terminal.State);
        Assert.DoesNotContain("documentVersionId", terminal.EvidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("documentSha256", terminal.EvidenceJson, StringComparison.OrdinalIgnoreCase);
        var storedSource = InvoiceUnitReviewStorage.Source(plan, f.Issued);
        Assert.Equal(4, InvoiceUnitReviewStorage.Read(storedSource).SourceSchemaVersion);
        var storedOutcome = InvoiceUnitReviewStorage.Outcome(terminal, storedSource);
        var response = InvoiceUnitReviewCommands.BindSource(new(1, f.Invoice.Id,
            storedSource.Id, plan.Case.Id, Guid.NewGuid(), "Decision", f.Invoice.Version,
            plan.Case.Version, plan.Case.State, storedOutcome.State, decisions.Single().Id,
            storedOutcome.Id, terminal.CompletedAt, true, false, false),
            storedSource, storedOutcome);
        Assert.Equal(4, response.EvidenceSchemaVersion);
        var consumer = new InvoiceUnitReviewConsumer(f.Invoice.TenantId,
            f.Invoice.LegalEntityId, f.Invoice.BranchRef, Guid.NewGuid().ToString("D"), null,
            new HashSet<string> { "procurement-access", "procurement-invoice-match" },
            f.Policy.Now.AddMinutes(2), new Dictionary<Guid, string>(), f.Invoice.BranchRef,
            new HashSet<Guid> { f.Policy.Branch }, false);
        var units = InvoiceUnitReviewTerminal.UnitsForNewComparison(terminal, plan,
            f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy,
            decisions, consumer);
        Assert.Equal("EACH", units[f.Line.Id]);
        Assert.Null(f.Invoice.FinancePostingRef);
    }

    [Fact]
    public void Missing_manual_reasons_or_fabricated_document_link_fail_before_completion()
    {
        var f = Manual();
        Assert.Throws<InvalidOperationException>(() => Flow.PrepareOpen(f.Invoice, [f.Line],
            f.Mapping, f.Issued, f.Matching, f.Policy.Policy, f.Maker, f.Policy.People,
            f.Hashes, new(Guid.NewGuid(), f.Invoice.Version, f.Mapping.MappingRevision,
                f.Policy.Now, "  ")));
        var plan = Open(f);
        var task = Assert.Single(plan.Case.Stages.Single().Assignments);
        var actor = Assert.Single(f.Policy.People);
        var decisions = new List<ApprovalDecision>();
        Assert.Throws<InvalidOperationException>(() => Flow.ApplyNewDecision(plan, f.Invoice,
            [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy, actor, f.Hashes,
            task.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version,
                task.Version, "Approve", " ", f.Policy.Now.AddMinutes(1)), decisions));
        Assert.Empty(decisions);
        f.Hashes[f.Document] = new string('a', 64);
        Assert.Throws<InvalidOperationException>(() => Flow.ValidateOwnedEvidence(plan,
            f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy,
            f.Hashes, f.Policy.Now.AddMinutes(1)));
    }

    [Fact]
    public void Public_manual_proof_rejects_rebound_maker_and_rehashed_blank_reasons()
    {
        var f = Manual();
        var plan = Open(f);
        var task = Assert.Single(plan.Case.Stages.Single().Assignments);
        var decisions = new List<ApprovalDecision>();
        Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued,
            f.Matching, f.Policy.Policy, Assert.Single(f.Policy.People), f.Hashes,
            task.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version,
                task.Version, "Approve", "I checked the entered unit",
                f.Policy.Now.AddMinutes(1)), decisions);
        var terminal = InvoiceUnitReviewTerminal.Seal(plan, f.Invoice, [f.Line],
            f.Mapping, f.Issued, f.Matching, f.Policy.Policy, f.Hashes,
            decisions, decisions.Single().Id, f.Policy.Now.AddMinutes(2));
        var authority = new InvoiceRetainedUnitReviewAuthority(terminal.ReviewId,
            terminal.SourceVersion, terminal.EvidenceJson, terminal.EvidenceSha256,
            plan.Case, decisions, plan.AssignmentWorkers, f.Matching, f.Policy.Policy);
        var consumer = new InvoiceUnitReviewConsumer(f.Invoice.TenantId,
            f.Invoice.LegalEntityId, f.Invoice.BranchRef, Guid.NewGuid().ToString("D"), null,
            new HashSet<string> { "procurement-access", "procurement-invoice-match" },
            f.Policy.Now.AddMinutes(2), new Dictionary<Guid, string>(), f.Invoice.BranchRef,
            new HashSet<Guid> { f.Policy.Branch }, false);
        Assert.Equal("EACH", InvoiceManualUnitReviewEvidence.Verify(authority,
            plan.Source, f.Invoice, [f.Line], f.Mapping, consumer)[f.Line.Id]);

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var envelope = JsonSerializer.Deserialize<InvoiceManualUnitReviewEnvelope>(terminal.EvidenceJson, json)!;
        var otherMaker = Guid.NewGuid().ToString("D");
        var reboundSource = plan.Source with { ReviewPolicy = plan.Source.ReviewPolicy with
            { MakerPrincipalId = otherMaker } };
        var reboundAuthority = Rehash(authority, envelope with { MakerPrincipalId = otherMaker }, json);
        Assert.Throws<InvalidOperationException>(() => InvoiceManualUnitReviewEvidence.Verify(
            reboundAuthority, reboundSource, f.Invoice, [f.Line], f.Mapping, consumer));

        var otherWorker = Guid.NewGuid();
        var reboundWorkerSource = plan.Source with { ReviewPolicy = plan.Source.ReviewPolicy with
            { MakerWorkerId = otherWorker } };
        var reboundWorkerAuthority = Rehash(authority, envelope with { MakerWorkerId = otherWorker }, json);
        Assert.Throws<InvalidOperationException>(() => InvoiceManualUnitReviewEvidence.Verify(
            reboundWorkerAuthority, reboundWorkerSource, f.Invoice, [f.Line], f.Mapping, consumer));

        var blankSource = plan.Source with { ManualReason = "   " };
        var blankAuthority = Rehash(authority, envelope with { MakerReason = "   " }, json);
        Assert.Throws<InvalidOperationException>(() => InvoiceManualUnitReviewEvidence.Verify(
            blankAuthority, blankSource, f.Invoice, [f.Line], f.Mapping, consumer));
        var blankReviewerAuthority = Rehash(authority, envelope with { ReviewerReason = "   " }, json);
        Assert.Throws<InvalidOperationException>(() => InvoiceManualUnitReviewEvidence.Verify(
            blankReviewerAuthority, plan.Source, f.Invoice, [f.Line], f.Mapping, consumer));
    }

    [Fact]
    public void Document_policy_source_bytes_remain_unchanged_when_manual_capability_is_absent()
    {
        var f = new InvoiceUnitReviewTransitionTests.Fixture();
        var plan = f.Open();
        Assert.Null(plan.Source.SourceSchemaVersion);
        Assert.Null(plan.Source.ManualReason);
        Assert.Null(plan.Source.ManualLines);
        Assert.DoesNotContain("SourceSchemaVersion", plan.SourceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualReason", plan.SourceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualLines", plan.SourceJson, StringComparison.Ordinal);
        Assert.Equal(JsonSerializer.Serialize(plan.Source), plan.SourceJson);
        var stored = InvoiceUnitReviewStorage.Source(plan, f.Issued);
        Assert.Equal(plan.SourceJson, stored.SourceJson);
        Assert.Equal(plan.SourceSha256, stored.SourceSha256);
        Assert.Equal(plan.SourceJson, JsonSerializer.Serialize(InvoiceUnitReviewStorage.Read(stored)));
    }

    private static InvoiceRetainedUnitReviewAuthority Rehash(
        InvoiceRetainedUnitReviewAuthority authority, InvoiceManualUnitReviewEnvelope envelope,
        JsonSerializerOptions options)
    {
        var text = JsonSerializer.Serialize(envelope, options);
        return authority with { EvidenceJson = text, EvidenceSha256 = ProcurementEvidenceEncoding.Digest(text) };
    }

    private static InvoiceUnitReviewTransitionTests.Fixture Manual()
    {
        var f = new InvoiceUnitReviewTransitionTests.Fixture();
        f.Policy.Policy.RulesJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 2, transactionType = "InvoiceUnitReview",
            evidenceMode = "ManualAttestation",
            stages = new[] { new { code = "REVIEW", mode = "One",
                principals = new[] { f.Policy.First } } }
        });
        f.Policy.People = f.Policy.People.Where(x => x.PrincipalId == f.Policy.First).ToArray();
        var link = InvoiceMappingEvidence.Verify(f.Mapping, f.Invoice, [f.Line]).Lines.Single();
        f.Mapping = InvoiceMappingEvidence.Create(f.Invoice, [f.Line],
            f.Mapping.PurchaseOrderId, f.Mapping.IssuedVersionId,
            [link with { DocumentVersionId = Guid.Empty, DocumentLineReference = "" }],
            f.Maker.PrincipalId, f.Maker.WorkerId, f.Policy.Now, identitySchemaVersion: 2);
        f.Invoice.CurrentMappingVersionId = f.Mapping.Id;
        f.Invoice.MappingRevision = f.Mapping.MappingRevision;
        f.Hashes.Clear();
        return f;
    }

    private static Flow.OpeningPlan Open(InvoiceUnitReviewTransitionTests.Fixture f)
        => Flow.PrepareOpen(f.Invoice, [f.Line], f.Mapping, f.Issued,
            f.Matching, f.Policy.Policy, f.Maker, f.Policy.People, f.Hashes,
            new(Guid.NewGuid(), f.Invoice.Version, f.Mapping.MappingRevision,
                f.Policy.Now, "Entered unit checked"));
}
