using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceOwnershipModelTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Stable_composite_keys_reject_cross_invoice_order_review_and_claim_reparenting()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope("p31-owner", Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        void Has<TChild, TParent>(params string[] fields)
            where TChild : ProcurementRecord where TParent : ProcurementRecord
        {
            var entity = db.Model.FindEntityType(typeof(TChild))!;
            Assert.Contains(entity.GetForeignKeys(), key =>
                key.PrincipalEntityType.ClrType == typeof(TParent)
                && key.Properties.Select(x => x.Name).SequenceEqual(fields));
        }

        Has<PrincipalInvoiceMappingVersion, PrincipalOrderIssuedVersion>("TenantId", "LegalEntityId",
            "PrincipalIssuedVersionId", "PurchaseOrderId");
        Has<PrincipalInvoiceMappingLine, PrincipalReceivingSourceState>("TenantId", "LegalEntityId",
            "PrincipalReceivingSourceStateId", "PurchaseOrderId", "PrincipalIssuedVersionId", "PurchaseOrderLineId");
        Has<PrincipalInvoiceMappingLine, PrincipalReceivingFact>("TenantId", "LegalEntityId",
            "PrincipalAcceptanceFactId", "PrincipalReceivingSourceStateId", "AcceptanceSequence");
        Has<PrincipalInvoiceMappingLine, PrincipalInvoiceMappingVersion>("TenantId", "LegalEntityId",
            "MappingVersionId", "SupplierInvoiceId", "PurchaseOrderId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceMappingLine, SupplierInvoiceLine>("TenantId", "LegalEntityId",
            "SupplierInvoiceLineId", "SupplierInvoiceId");
        Has<PrincipalInvoiceMappingLine, PurchaseOrderLine>("TenantId", "LegalEntityId",
            "PurchaseOrderLineId", "PurchaseOrderId");
        Has<PrincipalInvoiceUnitReviewSource, PrincipalInvoiceMappingVersion>("TenantId", "LegalEntityId",
            "MappingVersionId", "SupplierInvoiceId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceUnitReviewOutcome, PrincipalInvoiceUnitReviewSource>("TenantId", "LegalEntityId",
            "ReviewSourceId", "CoreCaseId");
        Has<PrincipalInvoiceUnitReviewAttestation, PrincipalInvoiceUnitReviewAssignment>("TenantId", "LegalEntityId",
            "CoreAssignmentId", "ReviewSourceId", "CoreCaseId");
        Has<PrincipalInvoiceComparison, PrincipalInvoiceMappingVersion>("TenantId", "LegalEntityId",
            "MappingVersionId", "SupplierInvoiceId", "PurchaseOrderId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceComparison, PrincipalInvoiceUnitReviewSource>("TenantId", "LegalEntityId",
            "ReviewSourceId", "MappingVersionId", "SupplierInvoiceId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceComparison, PrincipalInvoiceUnitReviewOutcome>("TenantId", "LegalEntityId",
            "ReviewOutcomeId", "ReviewSourceId");
        Has<PrincipalInvoiceClaim, PrincipalInvoiceComparison>("TenantId", "LegalEntityId",
            "ComparisonId", "MappingVersionId", "SupplierInvoiceId", "PurchaseOrderId",
            "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceClaim, PrincipalInvoiceMappingLine>("TenantId", "LegalEntityId",
            "MappingVersionId", "SupplierInvoiceLineId", "PurchaseOrderLineId",
            "SupplierInvoiceId", "PurchaseOrderId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceClaim, PrincipalReceivingFact>("TenantId", "LegalEntityId",
            "PrincipalAcceptanceFactId", "PrincipalReceivingSourceStateId", "AcceptanceSequence");
        Has<PrincipalInvoiceClaim, PrincipalReceivingSourceState>("TenantId", "LegalEntityId",
            "PrincipalReceivingSourceStateId", "PurchaseOrderId", "PrincipalIssuedVersionId", "PurchaseOrderLineId");
        Has<PrincipalInvoiceDiscrepancy, PrincipalInvoiceMappingVersion>("TenantId", "LegalEntityId",
            "MappingVersionId", "SupplierInvoiceId", "PurchaseOrderId", "PrincipalIssuedVersionId");
        Has<PrincipalInvoiceDiscrepancy, SupplierInvoiceLine>("TenantId", "LegalEntityId",
            "SupplierInvoiceLineId", "SupplierInvoiceId");
        Has<PrincipalInvoiceDiscrepancyAction, PrincipalInvoiceDiscrepancy>("TenantId", "LegalEntityId",
            "DiscrepancyId", "SupplierInvoiceId", "MappingVersionId");
        Has<PrincipalInvoiceDiscrepancyAction, PrincipalInvoiceComparison>("TenantId", "LegalEntityId",
            "ResolvingComparisonId", "SupplierInvoiceId", "MappingVersionId");
    }

    [Fact]
    public void P31_parent_alternate_keys_have_exact_short_names_and_unchanged_tuples()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope("p31-owner", Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        void Named<T>(string expected, params string[] fields) where T : ProcurementRecord
        {
            var entity = db.Model.FindEntityType(typeof(T))!;
            var key = Assert.Single(entity.GetKeys().Where(x => !x.IsPrimaryKey()
                && x.Properties.Select(p => p.Name).SequenceEqual(fields)));
            Assert.Equal(expected, key.GetName());
            Assert.True(expected.Length <= 63);
        }
        Named<PrincipalOrderIssuedVersion>("ak_pi_issued_order", "TenantId", "LegalEntityId", "Id", "PurchaseOrderId");
        Named<PrincipalReceivingSourceState>("ak_pi_receiving_source", "TenantId", "LegalEntityId", "Id",
            "PurchaseOrderId", "IssuedVersionId", "PurchaseOrderLineId");
        Named<PrincipalReceivingFact>("ak_pi_receiving_fact", "TenantId", "LegalEntityId", "Id",
            "SourceStateId", "Sequence");
    }
}
