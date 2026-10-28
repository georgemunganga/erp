using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Synthetic evidence tests do not establish a configured production Finance/review owner.</summary>
public sealed class InvoiceMatchExecutionEvidenceTests
{
    [Fact]
    public void Exact_review_owner_and_current_original_produce_capacity_claim_only_not_AP_authority()
    {
        var f = new Fixture(); var claim = Assert.Single(f.Claims());
        Assert.Equal(1m, claim.Quantity); Assert.Equal(10m, claim.NetAmount);
        Assert.Equal(f.Invoice.Id, claim.SupplierInvoiceId); Assert.Equal(f.Mapping.Id, f.Result.MappingVersionId);
        Assert.False(f.Comparison.CanPostToFinance); Assert.Null(f.Invoice.FinancePostingRef);
        Assert.Equal("Received", f.Invoice.Status); Assert.Equal(0, f.Exposure.Sequence);
    }

    [Theory]
    [InlineData("branch")][InlineData("principal")][InlineData("worker")][InlineData("policy")]
    [InlineData("mapping")][InlineData("original")][InlineData("document")][InlineData("documentHash")][InlineData("unit")]
    public void Verified_review_cannot_be_reused_for_other_actor_scope_mapping_or_document(string changed)
    {
        var f = new Fixture(); var envelope = f.ReviewEnvelope;
        envelope = changed switch
        {
            "branch" => envelope with { BranchId = Guid.NewGuid() },
            "principal" => envelope with { PrincipalId = "other" },
            "worker" => envelope with { WorkerId = Guid.NewGuid() },
            "policy" => envelope with { PolicyId = Guid.NewGuid() },
            "mapping" => envelope with { MappingVersionId = Guid.NewGuid() },
            "original" => envelope with { InvoiceOriginalSha256 = new string('b',64) },
            "document" => envelope with { Lines = [envelope.Lines[0] with { DocumentVersionId = Guid.NewGuid() }] },
            "documentHash" => envelope with { Lines = [envelope.Lines[0] with { DocumentSha256 = new string('d',64) }] },
            _ => envelope with { Lines = [envelope.Lines[0] with { UnitCode = "BOX" }] },
        };
        f.Review = f.Review with { EvidenceJson = Serialize(envelope) };
        Assert.Throws<InvalidOperationException>(() => f.Claims());
    }

    [Theory]
    [InlineData("branch")][InlineData("policy")][InlineData("expiry")][InlineData("allocations")]
    [InlineData("missing")][InlineData("malformed")][InlineData("duplicate")]
    public void Owner_checkpoint_is_bound_to_exact_scope_policy_lease_and_allocation_bytes(string changed)
    {
        var f = new Fixture(); var e = f.OwnerEnvelope;
        e = changed switch
        {
            "branch" => e with { BranchId = Guid.NewGuid() },
            "policy" => e with { PolicyRulesSha256 = new string('b',64) },
            "expiry" => e with { ValidUntil = e.ValidUntil.AddMinutes(1) },
            "allocations" => e with { AllocationsSha256 = new string('b',64) },
            _ => e,
        };
        var json = changed switch
        {
            "missing" => "{}", "malformed" => "{", "duplicate" => "{\"schemaVersion\":1,\"schemaVersion\":1}", _ => Serialize(e),
        };
        var owner = f.Owner with { EvidenceJson = json, EvidenceSha256 = changed == "malformed" ? "bad" : Digest(json) };
        Assert.False(InvoiceMatchExecutionEvidence.OwnerCovers(owner, f.OwnerContext));
    }

    [Fact]
    public void Procurement_owned_v2_checkpoint_requires_exact_reviewed_baseline_and_current_set_fields()
    {
        var f=new Fixture();var old=f.OwnerEnvelope;
        var current=new InvoiceProcurementOwnerExposureEnvelope(2,old.TenantId,old.LegalEntityId,
            old.BranchId,old.PurchaseOrderId,old.IssuedVersionId,old.SupplierId,
            old.CurrencyCode,old.PolicyRulesSha256,old.OwnerReference,old.Watermark,
            old.ValidUntil,old.AllocationsSha256,Guid.NewGuid(),Guid.NewGuid(),
            new string('a',64),new string('b',64),new string('c',64),new string('d',64));
        var json=Serialize(current);
        var owner=f.Owner with{EvidenceJson=json,EvidenceSha256=Digest(json)};
        Assert.True(InvoiceMatchExecutionEvidence.OwnerCovers(owner,f.OwnerContext));
        foreach(var malformed in new[]{
            Serialize(current with{CoreCaseId=Guid.Empty}),
            Serialize(current with{CurrentSetSha256=""}),
            json[..^1]+",\"unknown\":1}",
            json[..^1]+",\"baselineId\":\""+current.BaselineId+"\"}"
        })
            Assert.False(InvoiceMatchExecutionEvidence.OwnerCovers(owner with
                {EvidenceJson=malformed,EvidenceSha256=Digest(malformed)},f.OwnerContext));
    }

    [Theory]
    [InlineData("input")][InlineData("invoiceVersion")][InlineData("resultScope")][InlineData("currentMapping")]
    [InlineData("original")][InlineData("history")][InlineData("watermark")]
    public void Previously_compared_result_cannot_claim_after_current_binding_changes(string changed)
    {
        var f = new Fixture();
        switch(changed)
        {
            case "input": f.Input = f.Input with { Order = f.Input.Order with { Version = 2 } }; break;
            case "invoiceVersion": f.Invoice.Version++; break;
            case "resultScope": f.Result.TenantId = "other"; break;
            case "currentMapping": f.Invoice.CurrentMappingVersionId = Guid.NewGuid(); break;
            case "original": f.Line.Description = "Changed original"; break;
            case "watermark": f.Input = f.Input with { Consumption = f.Input.Consumption with { Watermark = "other" } }; break;
            default: f.Input = f.Input with { Consumption = f.Input.Consumption with { Allocations = [f.PriorDebit()] } }; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Claims());
        Assert.Equal(0, f.Exposure.Sequence); Assert.Null(f.Invoice.ActiveMatchClaimResultId);
    }

    [Theory]
    [InlineData("rules")][InlineData("projection")][InlineData("publication")]
    public void Same_policy_id_cannot_hide_different_current_rules_or_unpublished_authority(string changed)
    {
        var f = new Fixture();
        if (changed == "rules") f.CurrentPolicy.RulesJson = f.CurrentPolicy.RulesJson.Replace("\"moneyScale\":2", "\"moneyScale\":3");
        else if (changed == "publication") f.CurrentPolicy.PublishedBy = f.CurrentPolicy.CreatedBy;
        else f.Input = f.Input with { Policy = f.Input.Policy with { MoneyScale = 3 } };
        Assert.Throws<InvalidOperationException>(() => f.Claims());
    }

    [Fact]
    public void Claims_use_recomputed_lines_and_cannot_be_increased_by_detached_previous_line_objects()
    {
        var f = new Fixture();
        f.Comparison = f.Comparison with { Lines = [f.Comparison.Lines[0] with { Quantity = "999", NetAmount = "9990" }] };
        var claim = Assert.Single(f.Claims());
        Assert.Equal(1m, claim.Quantity); Assert.Equal(10m, claim.NetAmount);
    }

    [Fact]
    public void Owner_and_active_local_history_overlap_requires_explicit_reconciliation_not_double_counting()
    {
        var f = new Fixture(); var prior = f.PriorDebit();
        var owner = f.Owner with { Allocations = [prior] };
        var evidence = f.OwnerEnvelope with { AllocationsSha256 = InvoiceMatchExecutionEvidence.AllocationsDigest([prior]) };
        owner = owner with { EvidenceJson = Serialize(evidence), EvidenceSha256 = Digest(Serialize(evidence)) };
        Assert.True(InvoiceMatchExecutionEvidence.OwnerCovers(owner, f.OwnerContext));
        var oldInvoice = new SupplierInvoice { Id = prior.InvoiceId, TenantId = f.TenantId, LegalEntityId = f.LegalEntityId,
            BranchRef = f.WorkLocationId, SupplierId = f.Invoice.SupplierId, CurrencyCode = "ZMW" };
        var allocation = new InvoiceMatchAllocation { Id = prior.Id, TenantId = f.TenantId, LegalEntityId = f.LegalEntityId,
            SupplierInvoiceId = prior.InvoiceId, SupplierInvoiceLineId = prior.InvoiceLineId, MatchResultId = Guid.NewGuid(),
            ExposureStateId = f.Exposure.Id, PurchaseOrderId = prior.PurchaseOrderId, IssuedVersionId = prior.RevisionId,
            PurchaseOrderLineId = prior.PurchaseOrderLineId, SupplierId = prior.SupplierId, CurrencyCode = "ZMW", UnitCode = "each",
            Quantity = 1, NetAmount = 10, ExposureSequence = 1, ValidationReference = prior.ValidationReference, OwnerCoverageReference = "owner" };
        oldInvoice.ActiveMatchClaimResultId = allocation.MatchResultId; f.Exposure.Sequence = 1;
        Assert.Throws<InvalidOperationException>(() => InvoiceMatchExecutionEvidence.CompleteHistory(owner, f.OwnerContext,
            f.Exposure, [new(allocation, oldInvoice)]));
        oldInvoice.ActiveMatchClaimResultId = null;
        Assert.Throws<InvalidOperationException>(() => InvoiceMatchExecutionEvidence.CompleteHistory(f.Owner, f.OwnerContext,
            f.Exposure, [new(allocation, oldInvoice)]));
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static string Digest(string json) => ProcurementEvidenceEncoding.PolicyJsonDigest(json);
    internal sealed class Fixture : IProcurementScope
    {
        public string TenantId => "execution-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public Guid? WorkLocationId { get; } = Guid.NewGuid();
        public Guid? OrgUnitId => null;
        public bool IsConfined => true;
        public string SubjectId => "matcher";
        public string? CorrelationId => null;
        public DateTimeOffset Now { get; } = new(2026,10,20,12,0,0,TimeSpan.Zero);
        public SupplierInvoice Invoice { get; }
        public SupplierInvoiceLine Line { get; }
        public InvoiceMatchMappingVersion Mapping { get; }
        public InvoiceMappingReviewContext ReviewContext { get; }
        public InvoiceMappingReviewEnvelope ReviewEnvelope { get; }
        public InvoiceMappingReview Review { get; set; }
        public InvoiceExposureReadContext OwnerContext { get; }
        public InvoiceOwnerExposureEnvelope OwnerEnvelope { get; }
        public InvoiceHistoricalExposure Owner { get; }
        public InvoiceMatchExposureState Exposure { get; }
        public InvoiceMatchingInput Input { get; set; }
        public InvoiceMatchCommand Command { get; }
        public InvoiceMatchingEvaluation Comparison { get; set; }
        public InvoiceMatchResult Result { get; }
        public PolicyVersion CurrentPolicy { get; }
        public Fixture()
        {
            var order = Guid.NewGuid(); var issued = Guid.NewGuid(); var orderLine = Guid.NewGuid(); var document = Guid.NewGuid();
            Invoice = new() { TenantId = TenantId, LegalEntityId = LegalEntityId, BranchRef = WorkLocationId,
                SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-1", NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW",
                InvoiceDate = new(2026,10,20), NetAmount = 10, GrossAmount = 10, IntakeChannel = "Manual", Version = 1 };
            Line = new() { TenantId = TenantId, LegalEntityId = LegalEntityId, SupplierInvoiceId = Invoice.Id,
                LineNumber = 1, Description = "Original", Quantity = 1, UnitPrice = 10, Version = 1 };
            Mapping = InvoiceMappingEvidence.Create(Invoice, [Line], order, issued,
                [new(Line.Id, orderLine, null, null, "each", document, "1")], SubjectId, Guid.NewGuid(), Now);
            Invoice.CurrentMappingVersionId = Mapping.Id; Invoice.MappingRevision = Mapping.MappingRevision;
            var policyId = Guid.NewGuid();
            var tolerance = new { absolute = "0", percent = "0", combination = "AllLimits" };
            var rulesJson = Serialize(new { schemaVersion = 1, transactionType = "InvoiceMatching", currencyCode = "ZMW", moneyScale = 2,
                rounding = "ToEven", rules = new[] { new { categoryCode = "IT", method = "TwoWay", acceptanceKind = (string?)null,
                    quantity = tolerance, unitPrice = tolerance, tax = tolerance, total = tolerance } } });
            CurrentPolicy = new() { Id = policyId, TenantId = TenantId, LegalEntityId = LegalEntityId, PolicyKey = "INVOICE_MATCHING",
                PolicyVersionNumber = 1, State = "Published", RulesJson = rulesJson, EffectiveFrom = Now.AddDays(-1),
                PublishedAt = Now.AddHours(-1), PublishedBy = "independent-reviewer", CreatedBy = "policy-maker", PublicationReason = "Synthetic governed policy" };
            var policySha = Digest(rulesJson);
            ReviewContext = new(TenantId, LegalEntityId, WorkLocationId, SubjectId, Mapping.ActorWorkerId, policyId, policySha, Now, new Dictionary<Guid,string> { [document] = new string('c',64) });
            ReviewEnvelope = new(1,TenantId,LegalEntityId,WorkLocationId,Invoice.Id,Mapping.Id,Mapping.MappingSha256,
                Mapping.InvoiceOriginalSha256,order,issued,SubjectId,Mapping.ActorWorkerId!.Value,policyId,policySha,Now,
                [new(Line.Id,document,new string('c',64),"1","each")]);
            Review = new(true,"Verified",Serialize(ReviewEnvelope),new Dictionary<Guid,string> { [Line.Id] = "each" });
            OwnerContext = new(TenantId,LegalEntityId,WorkLocationId,order,issued,Invoice.SupplierId,"ZMW",policySha,Now);
            OwnerEnvelope = new(1,TenantId,LegalEntityId,WorkLocationId,order,issued,Invoice.SupplierId,"ZMW",policySha,
                "owner","watermark",Now.AddMinutes(5),InvoiceMatchExecutionEvidence.AllocationsDigest([]));
            Owner = new(true,"Verified","owner","watermark",Serialize(OwnerEnvelope),Digest(Serialize(OwnerEnvelope)),policySha,Now.AddMinutes(5),[]);
            Exposure = new() { TenantId = TenantId, LegalEntityId = LegalEntityId, PurchaseOrderId = order,
                IssuedVersionId = issued, SupplierId = Invoice.SupplierId, CurrencyCode = "ZMW" };
            Input = new(InvoiceMappingEvidence.Project(Mapping,Invoice,[Line],Review,ReviewContext),
                new(order,issued,1,1,TenantId,LegalEntityId,WorkLocationId,Invoice.SupplierId,"ZMW","Issued","synthetic",Now.AddDays(-1),
                    [new(orderLine,1,"IT","each",10,10,0)]),[],
                InvoiceMatchingPolicyRules.Project(CurrentPolicy),
                InvoiceMatchExecutionEvidence.CompleteHistory(Owner,OwnerContext,Exposure,[]),
                new("duplicates",true,[new(Invoice.Id,TenantId,LegalEntityId,Invoice.SupplierId,"INV-1","ZMW")]));
            Command = new(Guid.NewGuid(),1,Now,null); Comparison = InvoiceMatchingFoundation.Evaluate(Command,this,Input);
            Result = new() { TenantId = TenantId, LegalEntityId = LegalEntityId, SupplierInvoiceId = Invoice.Id,
                ExecutionVersion = 1, MappingVersionId = Mapping.Id, PurchaseOrderId = order, IssuedVersionId = issued,
                InvoiceVersion = 1, InvoiceOriginalSha256 = Mapping.InvoiceOriginalSha256, ActorPrincipalId = SubjectId,
                ActorWorkerId = Mapping.ActorWorkerId, CommandKey = Command.CommandKey, InputFingerprint = Comparison.InputFingerprint,
                EvidenceJson = Comparison.EvidenceJson, EvidenceSha256 = Comparison.EvidenceSha256,
                PolicyVersionId = policyId, PolicyRulesSha256 = policySha, Outcome = "Compared", MatchedAt = Now,
                SourceWatermarksJson = InvoiceMatchExecutionEvidence.SourceWatermarks(Input, Mapping, Review) };
        }
        public InvoiceMatchConsumption PriorDebit() => new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),TenantId,LegalEntityId,
            WorkLocationId,Invoice.SupplierId,Input.Order.Id,Input.Order.RevisionId,Input.Order.Lines[0].Id,null,"ZMW",
            InvoiceConsumptionKind.Debit,null,1,10,0,"synthetic-prior");
        public IReadOnlyList<InvoiceMatchAllocation> Claims() => InvoiceMatchExecutionEvidence.Claims(Comparison,Input,Result,Exposure,
            Owner,new Dictionary<Guid,Guid>(),Now,Invoice,[Line],Mapping,Review,ReviewContext,Command,this,[],CurrentPolicy);
    }
}
