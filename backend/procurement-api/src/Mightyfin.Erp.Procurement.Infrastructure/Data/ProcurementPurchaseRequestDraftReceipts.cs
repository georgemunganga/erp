using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using DraftReceipts = Mightyfin.Erp.Procurement.Application.PurchaseRequestDraftCommandReceipts;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PurchaseRequestDraftCommandReceipt> PurchaseRequestDraftCommandReceipts => Set<PurchaseRequestDraftCommandReceipt>();

    // Source-only model increment: genuine additive migration/readiness/finite-role
    // proof remain pending. No route, provider or live capability is enabled here.
    private void ConfigurePurchaseRequestDraftReceipts(ModelBuilder model)
    {
        ConfigureBase<PurchaseRequestDraftCommandReceipt>(model,"purchase_request_draft_command_receipts");
        var receipt=model.Entity<PurchaseRequestDraftCommandReceipt>();
        receipt.HasKey(x=>x.Id).HasName("pk_pr_draft_receipt");
        receipt.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id}).HasName("ak_pr_draft_receipt_scope");
        receipt.Property(x=>x.Operation).HasMaxLength(20);
        receipt.Property(x=>x.CommandFingerprint).HasMaxLength(64);
        receipt.Property(x=>x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x=>x.ResponseJson).HasColumnType("text");
        // Actor is intentionally excluded: one owned operation/key can complete once,
        // even when another current account subsequently presents the same UUID.
        receipt.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.Operation,x.CommandKey})
            .IsUnique().HasDatabaseName("ux_pr_draft_receipt_command");
        receipt.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.PurchaseRequestId}).HasDatabaseName("ix_pr_draft_receipt_request");
        receipt.HasOne<PurchaseRequest>().WithMany()
            .HasForeignKey(x=>new {x.TenantId,x.LegalEntityId,x.PurchaseRequestId})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pr_draft_receipt_request");
        receipt.ToTable(t=>
        {
            t.HasCheckConstraint("ck_pr_draft_receipt_identity",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND legal_entity_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND purchase_request_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(tenant_id)) > 0");
            t.HasCheckConstraint("ck_pr_draft_receipt_scope",
                "(context_branch_ref IS NULL OR (context_branch_ref <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND original_branch_ref IS NOT NULL AND context_branch_ref=original_branch_ref)) " +
                "AND (context_department_ref IS NULL OR (context_department_ref <> '00000000-0000-0000-0000-000000000000'::uuid " +
                "AND original_department_ref IS NOT NULL AND context_department_ref=original_department_ref)) " +
                "AND (original_branch_ref IS NULL OR original_branch_ref <> '00000000-0000-0000-0000-000000000000'::uuid) " +
                "AND (original_department_ref IS NULL OR original_department_ref <> '00000000-0000-0000-0000-000000000000'::uuid) " +
                "AND (operation <> 'Create' OR (context_branch_ref IS NOT DISTINCT FROM original_branch_ref " +
                "AND context_department_ref IS NOT DISTINCT FROM original_department_ref))");
            // Cast BEFORE addition, so predicate evaluation order cannot overflow bigint.
            t.HasCheckConstraint("ck_pr_draft_receipt_version",
                "(operation='Create' AND expected_version IS NULL AND result_version=1 AND response_status_code=201) " +
                "OR (operation IN ('Update','Submit') AND expected_version IS NOT NULL AND expected_version > 0 " +
                "AND expected_version < 9223372036854775807 AND result_version::numeric=expected_version::numeric+1 AND response_status_code=200)");
            t.HasCheckConstraint("ck_pr_draft_receipt_completion",
                "fingerprint_schema_version=1 AND response_schema_version=1 " +
                "AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' " +
                "AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex') " +
                "AND length(response_json)>0 AND isfinite(completed_at) AND completed_at > '0001-01-01 00:00:00+00'::timestamptz " +
                "AND completed_at <= created_at AND created_by=actor_principal_id::text " +
                "AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
        });
    }

    private void GuardPurchaseRequestDraftReceipts()
    {
        var changes=ChangeTracker.Entries<PurchaseRequestDraftCommandReceipt>()
            .Where(x=>x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if(changes.Any(x=>x.State!=EntityState.Added))
            throw new InvalidOperationException("RequestDraftCommandReceiptAppendOnly");
        foreach(var entry in changes)
        {
            var receipt=entry.Entity;
            DraftReceipts.ValidateCompleted(receipt);
            // The host owns current Core authentication/actions. Storage binds to
            // that shared validated scope; it never duplicates Core role services.
            if(!ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
                ||receipt.ActorPrincipalId.ToString("D")!=scope.SubjectId)
                throw new InvalidOperationException("RequestDraftCommandReceiptActorInvalid");
            if(receipt.TenantId!=scope.TenantId||receipt.LegalEntityId!=scope.LegalEntityId
                ||receipt.ContextBranchRef!=scope.WorkLocationId||receipt.ContextDepartmentRef!=scope.OrgUnitId
                ||scope.IsConfined&&scope.WorkLocationId is null)
                throw new InvalidOperationException("RequestDraftCommandReceiptScopeInvalid");
            // Normal shared stamping still supplies actual CreatedAt/CreatedBy/Version.
            // Reject a forged append-only envelope before that stamping could conceal it.
            if(receipt.CreatedBy!=scope.SubjectId||receipt.CreatedAt!=receipt.CompletedAt||receipt.Version!=1
                ||receipt.UpdatedAt is not null||receipt.UpdatedBy is not null||receipt.CompletedAt>DateTimeOffset.UtcNow)
                throw new InvalidOperationException("RequestDraftCommandReceiptMetadataInvalid");
            var tracked=ChangeTracker.Entries<PurchaseRequest>().SingleOrDefault(x=>x.Entity.Id==receipt.PurchaseRequestId);
            var stored=PurchaseRequests.AsNoTracking().SingleOrDefault(x=>x.Id==receipt.PurchaseRequestId);
            var request=tracked is {State:EntityState.Added or EntityState.Modified}?tracked.Entity:stored;
            if(request is null||tracked?.State==EntityState.Deleted||request.TenantId!=scope.TenantId
                ||request.LegalEntityId!=scope.LegalEntityId||!PurchaseRequestOwnership.IsOwner(request,receipt.ActorPrincipalId))
                throw new InvalidOperationException("RequestDraftCommandReceiptOwnerInvalid");
            long resultVersion;
            if(tracked?.State==EntityState.Added)
            {
                if(receipt.Operation!=DraftReceipts.Create)throw new InvalidOperationException("RequestDraftCommandReceiptOwnerVersionInvalid");
                resultVersion=1;
            }
            else if(tracked?.State==EntityState.Modified)
            {
                if(stored is null||receipt.Operation==DraftReceipts.Create||stored.Version==long.MaxValue
                    ||tracked.OriginalValues.GetValue<long>(nameof(ProcurementRecord.Version))!=stored.Version
                    ||receipt.ExpectedVersion!=stored.Version)
                    throw new InvalidOperationException("RequestDraftCommandReceiptOwnerVersionInvalid");
                resultVersion=checked(stored.Version+1);
            }
            else resultVersion=request.Version;
            if(receipt.ResultVersion!=resultVersion||receipt.OriginalBranchRef!=request.BranchRef
                ||receipt.OriginalDepartmentRef!=request.DepartmentRef)
                throw new InvalidOperationException("RequestDraftCommandReceiptOwnerVersionInvalid");
            // Only linkage metadata is inspected; existing route DTO/business-line
            // validation remains owning. Historical receipts are never revalidated
            // against today's advancing parent or rewritten to new scope.
            using var json=JsonDocument.Parse(receipt.ResponseJson);var response=json.RootElement;
            static Guid? NullableId(JsonElement value)=>value.ValueKind==JsonValueKind.Null?null:value.GetGuid();
            if(NullableId(response.GetProperty("requesterPrincipalId"))!=request.RequesterPrincipalId
                ||NullableId(response.GetProperty("requesterWorkerId"))!=request.RequesterWorkerId
                ||response.GetProperty("status").GetString()!=request.Status
                ||response.GetProperty("currencyCode").GetString()!=request.CurrencyCode
                ||receipt.Operation==DraftReceipts.Submit&&response.GetProperty("approvalState").GetString()!=request.ApprovalState)
                throw new InvalidOperationException("RequestDraftCommandReceiptOwnerResultInvalid");
        }
    }
}
