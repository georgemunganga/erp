using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMatchingPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => null; }
    private static ProcurementDbContext Context() => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new Scope("invoice-test", Guid.NewGuid(), "matcher"));
    [Fact]
    public void Claims_bind_exact_invoice_line_result_exposure_issued_line_and_receiving_source_state()
    {
        using var db = Context(); var allocation = db.Model.FindEntityType(typeof(InvoiceMatchAllocation))!;
        Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SupplierInvoiceLine)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","SupplierInvoiceLineId","SupplierInvoiceId" }));
        var result=db.Model.FindEntityType(typeof(InvoiceMatchResult))!;
        Assert.Contains(result.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceMatchMappingVersion)
            && fk.Properties.Select(x=>x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","MappingVersionId","SupplierInvoiceId","PurchaseOrderId","IssuedVersionId" }));
        Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ReceivingSourceState)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","ReceivingSourceStateId","PurchaseOrderId","IssuedVersionId","PurchaseOrderLineId" }));
        foreach (var target in new[] { typeof(GoodsReceiptLine),typeof(ServiceAcceptance) })
            Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == target
                && fk.Properties.Select(x=>x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId",target==typeof(GoodsReceiptLine)?"GoodsReceiptLineId":"ServiceAcceptanceId" }));
        Assert.Equal(38,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetPrecision());
        Assert.Equal(8,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetScale());
        Assert.Contains(allocation.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(
            new[] { "TenantId","LegalEntityId","ExposureStateId","ExposureSequence","SupplierInvoiceLineId" }));
        var exception = db.Model.FindEntityType(typeof(InvoiceException))!;
        Assert.Contains(exception.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceMatchResult)
            && fk.Properties.Any(x => x.Name == "SupplierInvoiceId") && fk.Properties.Any(x => x.Name == "MatchResultId"));
    }
    [Fact]
    public void Matching_does_not_reintroduce_mutable_receiving_completion_pins_as_EF_keys()
    {
        using var db=Context();
        foreach(var (type,names) in new[] {
            (typeof(GoodsReceiptLine),new[] { "SourceStateBindingId","PhysicalBindingId","ReceivingSourceStateId","PhysicalObservationId" }),
            (typeof(ServiceAcceptance),new[] { "SourceStateBindingId","ReceivingSourceStateId","CertificationEffectId" }) })
        {
            var entity=db.Model.FindEntityType(type)!;
            foreach(var name in names)
            {
                var property=entity.FindProperty(name)!;
                Assert.DoesNotContain(entity.GetKeys(),key=>key.Properties.Contains(property));
                Assert.Equal(ValueGenerated.Never,property.ValueGenerated);
            }
            Assert.Contains(entity.GetIndexes(),index=>index.IsUnique && index.Properties.Select(x=>x.Name)
                .SequenceEqual(new[] { "TenantId","LegalEntityId","Id","PurchaseOrderLineId","SourceStateBindingId" }));
        }
    }
    [Fact]
    public void Invoice_issued_order_key_keeps_existing_receiving_principal_constraint_name()
    {
        // Check the actual PostgreSQL naming convention; InMemory does not impose its 63-byte limit.
        // Building the design model never opens this deliberately invalid connection.
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=procurement_design_only;Username=invalid").Options,
            new Scope("invoice-test", Guid.NewGuid(), "matcher"));
        var issued = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PurchaseOrderIssuedVersion))!;
        var retained = issued.GetKeys().Single(x => x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id", "PurchaseOrderId", "SubmissionId" }));
        var added = issued.GetKeys().Single(x => x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id", "PurchaseOrderId" }));
        Assert.Equal("AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id~", retained.GetName());
        Assert.Equal("ak_invoice_issued_order_binding", added.GetName());
    }

    [Fact]
    public void Current_result_check_requires_nullable_evidence_explicitly_without_changing_legacy_columns()
    {
        using var db=Context(); var entity=db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InvoiceMatchResult))!;
        var sql=entity.GetCheckConstraints().Single(x=>x.Name=="ck_invoice_match_execution").Sql;
        Assert.StartsWith("execution_version = 0 OR (execution_version = 1 AND",sql);
        foreach(var (property,column) in new[] {
            (nameof(InvoiceMatchResult.InvoiceOriginalSha256),"invoice_original_sha256"),
            (nameof(InvoiceMatchResult.InputFingerprint),"input_fingerprint"),
            (nameof(InvoiceMatchResult.EvidenceSha256),"evidence_sha256"),
            (nameof(InvoiceMatchResult.PolicyRulesSha256),"policy_rules_sha256") })
        {
            // Legacy execution0 may still have no evidence; v1 must not pass CHECK via SQL unknown.
            Assert.True(entity.FindProperty(property)!.IsNullable);
            Assert.Contains(column+" IS NOT NULL AND "+column+" ~ '^[0-9a-f]{64}$'",sql);
        }
        foreach(var (property,column) in new[] {
            (nameof(InvoiceMatchResult.EvidenceJson),"evidence_json"),
            (nameof(InvoiceMatchResult.SourceWatermarksJson),"source_watermarks_json") })
        {
            Assert.True(entity.FindProperty(property)!.IsNullable);
            Assert.Contains(column+" IS NOT NULL",sql);
        }
    }
    [Fact]
    public void Verified_exposure_check_cannot_accept_null_sha_as_SQL_unknown()
    {
        using var db=Context(); var entity=db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InvoiceMatchExposureState))!;
        Assert.True(entity.FindProperty(nameof(InvoiceMatchExposureState.OwnerEvidenceSha256))!.IsNullable);
        var sql=entity.GetCheckConstraints().Single(x=>x.Name=="ck_invoice_exposure_state").Sql;
        Assert.Contains("coverage_state <> 'Verified' OR",sql);
        Assert.Contains("owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$'",sql);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Retained_mapping_and_capacity_claim_cannot_be_overwritten(bool mapping)
    {
        await using var db = Context();
        if(mapping) db.Entry(new InvoiceMatchMappingVersion()).State = EntityState.Modified;
        else db.Entry(new InvoiceMatchAllocation()).State = EntityState.Modified;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        Assert.Equal(mapping ? "InvoiceMappingAppendOnly" : "InvoiceAllocationAppendOnly",error.Message);
    }
    [Fact]
    public async Task Original_intake_stays_usable_but_material_header_or_line_overwrite_is_denied()
    {
        await using var db = Context(); var invoice = new SupplierInvoice { SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-1",
            NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW", IntakeChannel = "Manual", InvoiceDate = new(2026,10,20), NetAmount=1,GrossAmount=1 };
        db.AddScoped(invoice); var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Original", Quantity = 1, UnitPrice = 1 };
        db.AddScoped(line); await db.SaveChangesAsync();
        Assert.Equal("NotCompared",invoice.MatchingState); Assert.Null(invoice.CurrentMappingVersionId);
        line.Quantity=2;
        Assert.Equal("OriginalInvoiceLineImmutable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); invoice=await db.SupplierInvoices.SingleAsync(); invoice.NetAmount=2;
        Assert.Equal("OriginalInvoiceImmutable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Persisted_intake_line_set_cannot_grow_after_its_original_document_was_captured()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20) };
        db.AddScoped(invoice); await db.SaveChangesAsync();
        db.AddScoped(new SupplierInvoiceLine { SupplierInvoiceId=invoice.Id,LineNumber=1,Description="Late inserted line",Quantity=1,UnitPrice=1 });
        Assert.Equal("OriginalInvoiceLineSetClosed",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Blocked_result_still_cannot_bind_a_valid_mapping_to_a_different_issued_source()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20),NetAmount=1,GrossAmount=1 };
        db.AddScoped(invoice); var line=new SupplierInvoiceLine { SupplierInvoiceId=invoice.Id,LineNumber=1,Description="Original",Quantity=1,UnitPrice=1 };
        db.AddScoped(line); await db.SaveChangesAsync();
        var mapping=InvoiceMappingEvidence.Create(invoice,[line],Guid.NewGuid(),Guid.NewGuid(),
            [new(line.Id,Guid.NewGuid(),null,null,"each",Guid.Empty,"")],"matcher",Guid.NewGuid(),DateTimeOffset.UtcNow);
        db.AddScoped(mapping); invoice.CurrentMappingVersionId=mapping.Id; invoice.MappingRevision=mapping.MappingRevision; await db.SaveChangesAsync();
        db.AddScoped(new InvoiceMatchResult { SupplierInvoiceId=invoice.Id,ExecutionVersion=1,MappingVersionId=mapping.Id,
            PurchaseOrderId=Guid.NewGuid(),IssuedVersionId=Guid.NewGuid(),InvoiceVersion=invoice.Version,
            InvoiceOriginalSha256=mapping.InvoiceOriginalSha256,Outcome="Blocked",ActorPrincipalId="matcher",ActorWorkerId=Guid.NewGuid(),
            CommandKey=Guid.NewGuid(),EvidenceJson="{}",EvidenceSha256=ProcurementEvidenceEncoding.Digest("{}") });
        Assert.Equal("InvoiceMatchCurrentSourceRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task First_blocked_comparison_persists_bigint_revision_but_revision_only_change_is_denied()
    {
        await using var db = Context();
        var invoice = new SupplierInvoice { SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-FIRST-COMPARISON",
            NormalizedInvoiceNumber = "INV-FIRST-COMPARISON", CurrencyCode = "ZMW", IntakeChannel = "Manual",
            InvoiceDate = new(2026, 10, 20), NetAmount = 1m, GrossAmount = 1m };
        db.AddScoped(invoice);
        var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 1,
            Description = "Original captured line", Quantity = 1m, UnitPrice = 1m };
        db.AddScoped(line);
        await db.SaveChangesAsync();
        Assert.Equal(1L, invoice.Version);
        Assert.Equal(0L, invoice.MatchRevision);

        var actorWorkerId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        var mapping = InvoiceMappingEvidence.Create(invoice, [line], Guid.NewGuid(), Guid.NewGuid(),
            [new(line.Id, Guid.NewGuid(), null, null, "each", Guid.Empty, "")], "matcher", actorWorkerId, at);
        db.AddScoped(mapping);
        invoice.CurrentMappingVersionId = mapping.Id;
        invoice.MappingRevision = mapping.MappingRevision;
        await db.SaveChangesAsync();
        Assert.Equal(2L, invoice.Version);
        Assert.Equal("Unverified", mapping.VerificationState);

        // A blocked comparison needs durable evidence, but grants no unit-review, historical
        // exposure, capacity claim or Finance authority. This tests SaveChanges, not SQL FKs.
        const string policyJson = "{\"schemaVersion\":1,\"moneyScale\":2}";
        const string evidenceJson = "{\"schemaVersion\":1,\"outcome\":\"Blocked\",\"unitReview\":\"Unverified\",\"consumption\":\"Unavailable\"}";
        var watermarks = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1,
            invoiceId = invoice.Id, invoiceVersion = invoice.Version, mappingVersionId = mapping.Id,
            mapping.InvoiceOriginalSha256, purchaseOrderId = mapping.PurchaseOrderId,
            issuedVersionId = mapping.IssuedVersionId, consumptionState = "Unavailable" });
        var result = new InvoiceMatchResult { SupplierInvoiceId = invoice.Id, MatchType = "ThreeWay",
            Outcome = "Blocked", PolicyVersionId = Guid.NewGuid(), MatchedAt = at, ExecutionVersion = 1,
            MappingVersionId = mapping.Id, PurchaseOrderId = mapping.PurchaseOrderId,
            IssuedVersionId = mapping.IssuedVersionId, InvoiceVersion = invoice.Version,
            InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256, ActorPrincipalId = "matcher",
            ActorWorkerId = actorWorkerId, CommandKey = Guid.NewGuid(),
            InputFingerprint = ProcurementEvidenceEncoding.Digest(watermarks), EvidenceJson = evidenceJson,
            EvidenceSha256 = ProcurementEvidenceEncoding.Digest(evidenceJson),
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policyJson), SourceWatermarksJson = watermarks };
        db.AddScoped(result);
        invoice.LastMatchResultId = result.Id;
        invoice.MatchingState = "Blocked";
        invoice.MatchRevision = 1L;
        await db.SaveChangesAsync(); // The former GetValue<int>(MatchRevision) throws here.

        db.ChangeTracker.Clear();
        var saved = await db.SupplierInvoices.SingleAsync();
        var retained = await db.InvoiceMatchResults.SingleAsync();
        Assert.Equal(3L, saved.Version);
        Assert.Equal(1L, saved.MatchRevision);
        Assert.Equal(result.Id, saved.LastMatchResultId);
        Assert.Equal("Blocked", saved.MatchingState);
        Assert.Equal(2L, retained.InvoiceVersion);
        Assert.Equal(mapping.Id, retained.MappingVersionId);
        Assert.Equal(mapping.InvoiceOriginalSha256, retained.InvoiceOriginalSha256);
        Assert.Equal(evidenceJson, retained.EvidenceJson);
        Assert.Equal(watermarks, retained.SourceWatermarksJson);
        Assert.Null(saved.ActiveMatchClaimResultId);
        Assert.Null(saved.FinancePostingRef);
        Assert.Equal("Received", saved.Status);
        Assert.Empty(await db.InvoiceMatchAllocations.ToListAsync());

        saved.MatchRevision = 2L;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("InvoiceComparisonPointerFactRequired", error.Message);
        db.ChangeTracker.Clear();
        var unchanged = await db.SupplierInvoices.SingleAsync();
        Assert.Equal(1L, unchanged.MatchRevision);
        Assert.Equal(3L, unchanged.Version);
        Assert.Equal(result.Id, unchanged.LastMatchResultId);
        Assert.Single(await db.InvoiceMatchResults.ToListAsync());
    }
    [Fact]
    public async Task A_new_claim_cannot_reuse_an_unchanged_previously_consumed_exposure_sequence()
    {
        var f=new InvoiceMatchExecutionEvidenceTests.Fixture();
        var claim=Assert.Single(f.Claims()); // Positive pure authority check first; no fabricated incomplete source rows.
        await using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,f);
        f.Exposure.Sequence=claim.ExposureSequence; f.Exposure.CoverageState="Verified";
        f.Exposure.OwnerReference=f.Owner.OwnerReference; f.Exposure.OwnerWatermark=f.Owner.Watermark;
        f.Exposure.OwnerEvidenceSha256=f.Owner.EvidenceSha256; f.Exposure.OwnerValidUntil=f.Owner.ValidUntil;
        f.Invoice.ActiveMatchClaimResultId=f.Result.Id;
        db.Attach(f.Invoice); db.Attach(f.Line); db.Attach(f.Mapping); db.Attach(f.Result); db.Attach(f.Exposure);
        Assert.Equal(EntityState.Unchanged,db.Entry(f.Exposure).State);
        db.AddScoped(claim);
        // All current original/mapping/Compared result/lease/source/amount bindings are valid.
        // Only the absence of a new tracked exposure advancement must deny this new claim.
        Assert.Equal("InvoiceAllocationAuthorityRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Unknown_checkpoint_cannot_invent_a_claim_sequence_without_immutable_claim_batch()
    {
        await using var db = Context(); var exposure = new InvoiceMatchExposureState { PurchaseOrderId=Guid.NewGuid(),IssuedVersionId=Guid.NewGuid(),SupplierId=Guid.NewGuid(),CurrencyCode="ZMW" };
        db.AddScoped(exposure); await db.SaveChangesAsync(); exposure.Sequence=1;
        Assert.Equal("InvoiceExposureClaimBatchRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Current_mapping_pointer_cannot_be_rebound_without_new_complete_mapping_fact()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20) };
        db.AddScoped(invoice); await db.SaveChangesAsync(); invoice.CurrentMappingVersionId=Guid.NewGuid(); invoice.MappingRevision=1;
        Assert.Equal("InvoiceMappingPointerFactRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
}
