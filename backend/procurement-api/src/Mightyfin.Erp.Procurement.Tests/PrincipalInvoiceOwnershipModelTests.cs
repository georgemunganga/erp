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
    }
}
