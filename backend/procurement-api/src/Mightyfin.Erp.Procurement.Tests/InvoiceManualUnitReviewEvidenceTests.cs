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
