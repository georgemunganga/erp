using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicitly enabled scoped provider. Constants were captured from the isolated PostgreSQL17
/// P25 rehearsal; exact schema and finite-role validation remain mandatory at the point of use.</summary>
public sealed class ProcurementPrincipalApprovalStoreReadiness(IApprovalScope coreScope,HrmDbContext coreIdentity):IProcurementPrincipalApprovalSchemaReadiness
{
    // Actual capture: procurement-p25-private-capture-20261022_browser03/result.json
    // SHA256 99990a425f07fc1f3c8135bd1b3f116ded9e6e8e3f140a366a149777408737fd.
    // PostgreSQL definitions are literal: no whitespace, cast or expression normalization.
    internal const string MigrationId="20261022161353_P25CorePrincipalRequestApproval";
    internal static readonly IReadOnlyDictionary<string,string> CheckDefinitions=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.assignment_authority_evidence.ck_pr_assignment_authority_identity"]="CHECK (((identity_schema_version = ANY (ARRAY[1, 2])) AND ((identity_schema_version = 1) OR ((principal_id IS NOT NULL) AND ((principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text))) AND (core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (grant_revision > 0) AND (authority_version > 0) AND (length(btrim((principal_id)::text)) > 0) AND ((assignee_kind)::text = ANY ((ARRAY['LineManager'::character varying, 'ProcurementManager'::character varying])::text[])) AND ((evidence_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((assignee_worker_id IS NULL) OR (assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
        ["procurement.budget_check_snapshots.ck_budget_checker_identity"]="CHECK ((((checker_identity_schema_version = 0) AND (checked_by_worker_id IS NULL) AND (checker_identity_sha256 IS NULL)) OR ((checker_identity_schema_version = 1) AND (checked_by_worker_id IS NOT NULL) AND (checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (checker_identity_sha256 IS NOT NULL) AND ((checker_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text)) OR ((checker_identity_schema_version = 2) AND ((checked_by_worker_id IS NULL) OR (checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (checked_by_subject_id IS NOT NULL) AND ((checked_by_subject_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((checked_by_subject_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (checker_identity_sha256 IS NOT NULL) AND ((checker_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text))))",
        ["procurement.purchase_request_approval_outcomes.ck_pr_approval_outcome_evidence"]="CHECK ((((outcome)::text = ANY ((ARRAY['Approved'::character varying, 'Rejected'::character varying, 'Returned'::character varying])::text[])) AND (submitted_business_version > 0) AND (core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (length(btrim((decision_command_key)::text)) > 0) AND (length(btrim((effect_key)::text)) > 0) AND (length(btrim((actor_subject_id)::text)) > 0)))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_principal_identity"]="CHECK ((((snapshot_schema_version = ANY (ARRAY[1, 2])) AND (requester_principal_id IS NULL) AND (requester_identity_sha256 IS NULL) AND (principal_identity_sha256 IS NULL)) OR ((snapshot_schema_version = 3) AND (requester_principal_id IS NOT NULL) AND (requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (maker_subject_id IS NOT NULL) AND ((maker_subject_id)::text = (requester_principal_id)::text) AND (requester_identity_sha256 IS NOT NULL) AND ((requester_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND (principal_identity_sha256 IS NOT NULL) AND ((principal_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text))))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_submission_amount"]="CHECK (((estimated_amount >= (0)::numeric) AND ((amount_basis)::text = 'EstimatedLineBase'::text) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text)))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_submission_digest"]="CHECK ((((request_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((finance_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((policy_decision_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((policy_rules_sha256)::text ~ '^[0-9a-f]{64}$'::text)))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_submission_finance_mode"]="CHECK (((((finance_evidence_mode)::text = 'AutomatedFinance'::text) AND (manual_evidence_authority_ref IS NULL)) OR (((finance_evidence_mode)::text = 'ApprovedManualEvidence'::text) AND (manual_evidence_authority_ref IS NOT NULL) AND (length(btrim((manual_evidence_authority_ref)::text)) > 0))))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_submission_identity"]="CHECK (((snapshot_schema_version = ANY (ARRAY[1, 2, 3])) AND (submitted_business_version > 0) AND (policy_version_number > 0) AND (core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["procurement.purchase_request_approval_submissions.ck_pr_approval_worker_identity"]="CHECK ((((snapshot_schema_version = 1) AND (maker_worker_id IS NULL) AND (requester_worker_id IS NULL) AND (worker_identity_sha256 IS NULL)) OR ((snapshot_schema_version = 2) AND (maker_worker_id IS NOT NULL) AND (requester_worker_id IS NOT NULL) AND (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (worker_identity_sha256 IS NOT NULL) AND ((worker_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text)) OR ((snapshot_schema_version = 3) AND (worker_identity_sha256 IS NULL) AND ((maker_worker_id IS NULL) OR (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((requester_worker_id IS NULL) OR (requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))))",
        ["procurement.purchase_request_approval_submissions.ck_pr_finance_identity_version"]="CHECK ((((finance_identity_schema_version = 0) AND (snapshot_schema_version = ANY (ARRAY[1, 2]))) OR ((finance_identity_schema_version = 1) AND (snapshot_schema_version = 2)) OR ((finance_identity_schema_version = 2) AND (snapshot_schema_version = 3))))",
        ["procurement.purchase_request_finance_release_confirmations.ck_pr_finance_release_confirmation"]="CHECK (((released_amount >= (0)::numeric) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (finance_confirmed_at <= recorded_at) AND (length(btrim((evidence_ref)::text)) > 0) AND (length(btrim((evidence_source)::text)) > 0) AND (length(btrim((verified_by_subject_id)::text)) > 0) AND (length(btrim((effect_key)::text)) > 0)))",
        ["procurement.purchase_request_finance_release_requests.ck_pr_finance_release_request"]="CHECK ((((outcome)::text = ANY ((ARRAY['Rejected'::character varying, 'Returned'::character varying])::text[])) AND (reserved_amount >= (0)::numeric) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (length(btrim((reservation_ref)::text)) > 0) AND (length(btrim((effect_key)::text)) > 0) AND (length(btrim((owner_queue)::text)) > 0) AND (length(btrim((requested_by_subject_id)::text)) > 0)))",
        ["procurement.purchase_request_lines.ck_pr_line_catalog_ref"]="CHECK ((((catalog_entry_id IS NULL) AND (catalog_published_version IS NULL)) OR ((catalog_entry_id IS NOT NULL) AND (catalog_published_version > 0))))",
        ["procurement.purchase_request_lines.ck_pr_line_nonnegative"]="CHECK (((requested_quantity > (0)::numeric) AND (estimated_unit_price >= (0)::numeric) AND (approved_quantity >= (0)::numeric) AND (allocated_quantity >= (0)::numeric) AND (ordered_quantity >= (0)::numeric) AND (accepted_quantity >= (0)::numeric) AND (cancelled_quantity >= (0)::numeric) AND ((discount_percent IS NULL) OR ((discount_percent >= (0)::numeric) AND (discount_percent <= (100)::numeric)))))",
        ["procurement.purchase_requests.ck_pr_original_principal_identity"]="CHECK ((((requester_identity_schema_version = 0) AND (requester_core_worker_id IS NULL) AND (requester_identity_sha256 IS NULL)) OR ((requester_identity_schema_version = 1) AND (requester_principal_id IS NOT NULL) AND (requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((requester_core_worker_id IS NULL) OR (requester_core_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (created_by IS NOT NULL) AND ((created_by)::text = (requester_principal_id)::text) AND (source_channel IS NOT NULL) AND ((source_channel)::text = 'ERP'::text) AND ((on_behalf_of_subject_id IS NULL) OR (length(btrim(on_behalf_of_subject_id)) = 0)) AND (requester_identity_sha256 IS NOT NULL) AND ((requester_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text))))",
        ["procurement.purchase_requests.ck_purchase_request_identity"]="CHECK ((((requester_principal_id IS NULL) OR (requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((requester_worker_id IS NULL) OR (requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((requester_principal_id IS NOT NULL) OR (requester_worker_id IS NOT NULL))))",
        ["procurement.spending_authority_identity_evidence.ck_authority_identity_evidence"]="CHECK (((schema_version = ANY (ARRAY[1, 2])) AND ((schema_version = 2) OR ((actor_worker_id IS NOT NULL) AND (recipient_worker_id IS NOT NULL))) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (grant_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((schema_version = 1) OR (((action)::text = 'PurchaseRequest.Approve'::text) AND ((permission)::text = 'procurement-approval-decide'::text))) AND (authority_version > 0) AND (grant_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((recipient_worker_id IS NULL) OR (recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (length(btrim((authority_reference)::text)) > 0) AND (length(btrim((permission)::text)) > 0) AND (length(btrim((action)::text)) > 0) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((recipient_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((recipient_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((configuration_owner_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((configuration_owner_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (business_fingerprint IS NOT NULL) AND ((business_fingerprint)::text ~ '^[0-9a-fA-F]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (grant_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Activated'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))",
    };
    // Exact PostgreSQL 17 definitions observed after the isolated P29 backup/restore.
    // Only these two captured varchar-array spellings differ from the P25 originals.
    internal static readonly IReadOnlyDictionary<string,string> RestoredCheckDefinitions=
        new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.purchase_request_approval_outcomes.ck_pr_approval_outcome_evidence"]="CHECK ((((outcome)::text = ANY (ARRAY[('Approved'::character varying)::text, ('Rejected'::character varying)::text, ('Returned'::character varying)::text])) AND (submitted_business_version > 0) AND (core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (length(btrim((decision_command_key)::text)) > 0) AND (length(btrim((effect_key)::text)) > 0) AND (length(btrim((actor_subject_id)::text)) > 0)))",
        ["procurement.purchase_request_finance_release_requests.ck_pr_finance_release_request"]="CHECK ((((outcome)::text = ANY (ARRAY[('Rejected'::character varying)::text, ('Returned'::character varying)::text])) AND (reserved_amount >= (0)::numeric) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (length(btrim((reservation_ref)::text)) > 0) AND (length(btrim((effect_key)::text)) > 0) AND (length(btrim((owner_queue)::text)) > 0) AND (length(btrim((requested_by_subject_id)::text)) > 0)))",
    };
    internal static bool MatchesCapturedCheck(string key,string expected,string actual) =>
        CheckDefinitions.TryGetValue(key,out var captured) && expected==captured
            && (actual==captured || RestoredCheckDefinitions.TryGetValue(key,out var restored)
                && actual==restored);

    internal static readonly IReadOnlyDictionary<string,string> PartialPredicates=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.spending_authority_grants.IX_spending_authority_grants_tenant_id_legal_entity_id_princip~"]="((state)::text = 'Active'::text)",
        ["procurement.spending_authority_identity_evidence.ux_authority_identity_activation"]="((stage)::text = 'Activated'::text)",
        ["procurement.spending_authority_identity_evidence.ux_authority_identity_origin"]="((stage)::text = 'Created'::text)",
    };
    internal static bool ManifestCaptured=>MigrationId.Length>0 && CheckDefinitions.Count>0 && PartialPredicates.Count>0;
    internal static readonly Type[] SourceTypes=[typeof(PurchaseRequest),typeof(PurchaseRequestLine),typeof(BudgetCheckSnapshot),
        typeof(ProcurementCategory),typeof(PolicyVersion),typeof(PolicyEvaluation),typeof(PurchaseRequestApprovalSubmission),typeof(PurchaseRequestAssignmentAuthorityEvidence),
        typeof(ProcurementSpendingAuthorityGrant),typeof(ProcurementSpendingAuthorityCategory),typeof(SpendingAuthorityIdentityRecord),
        typeof(PurchaseRequestApprovalOutcome),typeof(PurchaseRequestFinanceReleaseRequest),typeof(PurchaseRequestFinanceReleaseConfirmation),
        typeof(ProcurementApprovalCommandReceipt),typeof(AuditEvent),typeof(OutboxEvent)];
    private static readonly string[] AppendTables=["budget_check_snapshots","spending_authority_identity_evidence","purchase_request_approval_submissions",
        "assignment_authority_evidence","purchase_request_approval_outcomes","purchase_request_finance_release_requests","purchase_request_finance_release_confirmations",
        "approval_command_receipts","audit_events","outbox_events"];
    // Only fields read by the existing current-account/grant/branch and optional
    // HR-manager queries. Catalog validation may inspect model column metadata;
    // the actual finite role never needs credential, pay or personal-field SELECT.
    internal static readonly IReadOnlyDictionary<Type,string[]> CoreIdentityReadProperties=new Dictionary<Type,string[]>
    {
        [typeof(LegalEntity)]=[nameof(LegalEntity.Id),nameof(LegalEntity.TenantId),nameof(LegalEntity.IsArchived),nameof(LegalEntity.Currency)],
        [typeof(WorkLocation)]=[nameof(WorkLocation.Id),nameof(WorkLocation.TenantId),nameof(WorkLocation.LegalEntityId),nameof(WorkLocation.IsArchived)],
        [typeof(OrgUnit)]=[nameof(OrgUnit.Id),nameof(OrgUnit.TenantId),nameof(OrgUnit.LegalEntityId),nameof(OrgUnit.IsArchived),nameof(OrgUnit.Status),nameof(OrgUnit.EffectiveFrom),nameof(OrgUnit.EffectiveTo)],
        [typeof(Worker)]=[nameof(Worker.Id),nameof(Worker.TenantId),nameof(Worker.IsArchived),nameof(Worker.Status),nameof(Worker.LocationId),nameof(Worker.OrgUnitId),nameof(Worker.ManagerId)],
        [typeof(LocalUser)]=[nameof(LocalUser.Id),nameof(LocalUser.TenantId),nameof(LocalUser.WorkerId),nameof(LocalUser.RolesCsv),nameof(LocalUser.LockedUntil),nameof(LocalUser.IsActive),nameof(LocalUser.IsArchived)],
        [typeof(HrUserBranchAssignment)]=[nameof(HrUserBranchAssignment.TenantId),nameof(HrUserBranchAssignment.UserId),nameof(HrUserBranchAssignment.LocationId),nameof(HrUserBranchAssignment.IsArchived)],
        [typeof(TenantRoleAssignment)]=[nameof(TenantRoleAssignment.TenantId),nameof(TenantRoleAssignment.Active),nameof(TenantRoleAssignment.IsArchived),nameof(TenantRoleAssignment.RoleKey),nameof(TenantRoleAssignment.PermissionsCsv)],
    };
    private static readonly Type[] CoreTypes=[typeof(ApprovalCase),typeof(ApprovalStage),typeof(ApprovalAssignment),typeof(ApprovalDecision)];
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,0,ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,1,ct);
    public Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,2,ct);

    private async Task<bool> Check(ProcurementDbContext owner,int mode,CancellationToken ct)
    {
        if(!ManifestCaptured || !owner.Database.IsNpgsql())return false;
        var opened=false;
        try
        {
            if(owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal))return false;
            if(owner.Database.GetDbConnection().State!=ConnectionState.Open){await owner.Database.OpenConnectionAsync(ct);opened=true;}
            await using var command=owner.Database.GetDbConnection().CreateCommand();
            command.Transaction=owner.Database.CurrentTransaction?.GetDbTransaction();command.CommandTimeout=10;
            async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct) is true;}
            var model=owner.GetService<IDesignTimeModel>().Model;
            var entities=SourceTypes.Select(t=>model.FindEntityType(t)).ToArray();
            if(entities.Any(x=>x is null))return false;
            var expectedChecks=entities.SelectMany(x=>x!.GetCheckConstraints().Select(c=>x.GetSchema()+"."+x.GetTableName()+"."+c.Name)).ToHashSet(StringComparer.Ordinal);
            var expectedPredicates=entities.SelectMany(x=>x!.GetIndexes().Where(i=>i.IsUnique && i.GetFilter() is not null)
                .Select(i=>x.GetSchema()+"."+x.GetTableName()+"."+i.GetDatabaseName())).ToHashSet(StringComparer.Ordinal);
            if(!expectedChecks.SetEquals(CheckDefinitions.Keys) || !expectedPredicates.SetEquals(PartialPredicates.Keys))return false;
            foreach(var entity in entities)
            {
                if(entity!.GetSchema()!="procurement" || !await Inspect(entity,true))return false;
            }
            // Metadata comes from the existing Core owner model; reads/privilege
            // discovery still use ONLY the original Procurement connection/transaction.
            var identityModel=coreIdentity.GetService<IDesignTimeModel>().Model;
            foreach(var type in CoreIdentityReadProperties.Keys)
            {
                var entity=identityModel.FindEntityType(type);
                if(entity is null || entity.GetSchema()!="hrm")return false;
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                command.CommandText=CoreIdentitySelectSql(entity);await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True(ColumnsSql(entity,table)))return false;
            }
            var relations=entities.Select(x=>x!.GetTableName()!).Append("__procurement_migrations").Distinct().ToArray();
            if(!await True("SELECT NOT has_schema_privilege(current_user,'procurement','CREATE')")
                || !await True("SELECT "+ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql)
                || !await True("SELECT "+ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql)
                || !await True(NoOwnersSql(relations)) || !await True(CoreOwnershipSql))return false;
            foreach(var table in AppendTables)if(!await True(AppendSql(table,false)))return false;
            if(!await True("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relname::text=ANY("
                +Columns(relations)+") AND has_table_privilege(current_user,c.oid,'TRUNCATE'))"))return false;
            if(!await True(DefaultSql("purchase_requests","requester_identity_schema_version","0"))
                || !await True(DefaultSql("budget_check_snapshots","checker_identity_schema_version","0"))
                || !await True(DefaultSql("purchase_request_approval_submissions","finance_identity_schema_version","0"))
                || !await True(DefaultSql("assignment_authority_evidence","identity_schema_version","1")))return false;
            if(mode==1)
            {
                if(!await True(NoCoreWorkflowDmlSql))return false;
                foreach(var table in new[]{"purchase_requests","purchase_request_lines","spending_authority_grants"})
                    if(!await True(RequiredSql("procurement."+table,"SELECT","INSERT","UPDATE")))return false;
                if(!await True(RequiredSql("procurement.spending_authority_categories","SELECT","INSERT","DELETE")))return false;
                foreach(var table in new[]{"budget_check_snapshots","spending_authority_identity_evidence","audit_events","outbox_events"})
                    if(!await True(AppendSql(table,true)))return false;
            }
            if(mode==2)
            {
                // Use the real shared Core model solely as metadata. All catalog commands
                // remain on the original owner's connection/current transaction.
                await using var core=new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>()
                    .UseNpgsql(owner.Database.GetDbConnection()).Options,coreScope);
                var coreModel=core.GetService<IDesignTimeModel>().Model;
                foreach(var type in CoreTypes)
                {
                    var entity=coreModel.FindEntityType(type);
                    if(entity is null || entity.GetSchema()!="core_approval" || !await Inspect(entity,false))return false;
                }
                if(!await True("SELECT EXISTS(SELECT 1 FROM core_approval.\"__EFMigrationsHistory\" WHERE \"MigrationId\"='20261019183218_P01CoreApprovalFoundation')"))return false;
                foreach(var table in new[]{"cases","stages","assignments"})
                    if(!await True(RequiredSql("core_approval."+table,"SELECT","INSERT","UPDATE")
                        +" AND NOT has_table_privilege(current_user,"+Literal("core_approval."+table)+",'DELETE,TRUNCATE')"))return false;
                if(!await True(ProcurementInvoiceUnitReviewSchemaReadiness.CoreDecisionPrivilegesSql)
                    || !await True(NoOtherCoreWorkflowDmlSql))return false;
                foreach(var table in new[]{"purchase_requests","purchase_request_lines"})
                    if(!await True(RequiredSql("procurement."+table,"SELECT","UPDATE")))return false;
                foreach(var table in AppendTables.Where(x=>x is not ("budget_check_snapshots" or "spending_authority_identity_evidence")))
                    if(!await True(AppendSql(table,true)))return false;
            }
            return true;

            async Task<bool> Inspect(IEntityType entity,bool procurement)
            {
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                command.CommandText=entity.ClrType==typeof(ProcurementCategory)?CategorySelectSql(entity):SelectAllColumnsSql(entity,table);await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True(ColumnsSql(entity,table)))return false;
                foreach(var check in entity.GetCheckConstraints())
                {
                    if(!procurement || !CheckDefinitions.TryGetValue(table.Schema+"."+table.Name+"."+check.Name,out var expected))return false;
                    command.CommandText=CheckSql(table,check.Name!);
                    if(await command.ExecuteScalarAsync(ct) is not string actual
                        || !MatchesCapturedCheck(table.Schema+"."+table.Name+"."+check.Name,expected,actual))return false;
                }
                foreach(var key in entity.GetKeys())
                    if(!await True(KeySql(table,key.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),key.GetName()!,key.IsPrimaryKey())))return false;
                foreach(var foreign in entity.GetForeignKeys())
                {
                    var parent=StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,foreign.PrincipalEntityType.GetSchema());
                    if(foreign.DeleteBehavior!=DeleteBehavior.Restrict
                        || !await True(ForeignKeySql(table,parent,foreign.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),
                            foreign.PrincipalKey.Properties.Select(p=>p.GetColumnName(parent)!).ToArray(),foreign.GetConstraintName()))
                        || !await True(KeySql(parent,foreign.PrincipalKey.Properties.Select(p=>p.GetColumnName(parent)!).ToArray(),
                            foreign.PrincipalKey.GetName()!,foreign.PrincipalKey.IsPrimaryKey())))return false;
                }
                foreach(var index in entity.GetIndexes().Where(x=>x.IsUnique))
                {
                    string? predicate=null;
                    if(index.GetFilter() is not null && (!procurement || !PartialPredicates.TryGetValue(table.Schema+"."+table.Name+"."+index.GetDatabaseName(),out predicate)))return false;
                    if(!await True(UniqueIndexSql(table,index.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),index.GetDatabaseName(),predicate)))return false;
                }
                return true;
            }
        }
        catch(Exception error) when(error is not OperationCanceledException){return false;}
        finally{if(opened)await owner.Database.CloseConnectionAsync();}
    }

    internal static string CategorySelectSql(IEntityType entity)
    {
        var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
        if(entity.ClrType!=typeof(ProcurementCategory) || table.Schema!="procurement")
            throw new InvalidOperationException("PrincipalApprovalCategoryProjectionUnsupported");
        string[] names=[nameof(ProcurementCategory.TenantId),nameof(ProcurementCategory.LegalEntityId),nameof(ProcurementCategory.Code),nameof(ProcurementCategory.Status)];
        return "SELECT "+string.Join(',',names.Select(name=>Quote((entity.FindProperty(name)
            ?? throw new InvalidOperationException("PrincipalApprovalCategoryPropertyMissing")).GetColumnName(table)!)))
            +" FROM "+Quote(table.Schema)+"."+Quote(table.Name)+" LIMIT 0";
    }

    internal static string CoreIdentitySelectSql(IEntityType entity)
    {
        var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
        if(!CoreIdentityReadProperties.TryGetValue(entity.ClrType,out var names) || table.Schema!="hrm")
            throw new InvalidOperationException("PrincipalApprovalCoreReadProjectionUnsupported");
        return "SELECT "+string.Join(',',names.Select(name=>Quote((entity.FindProperty(name)
            ?? throw new InvalidOperationException("PrincipalApprovalCoreReadPropertyMissing")).GetColumnName(table)!)))
            +" FROM "+Quote(table.Schema)+"."+Quote(table.Name)+" LIMIT 0";
    }

    internal static string RequiredSql(string relation,params string[] privileges)=>"SELECT "+string.Join(" AND ",privileges.Select(p=>
        "has_table_privilege(current_user,"+Literal(relation)+","+Literal(p)+")"));
    internal static string AppendSql(string table,bool insert)=>RequiredSql("procurement."+table,insert?new[]{"SELECT","INSERT"}:new[]{"SELECT"})
        +" AND NOT has_any_column_privilege(current_user,"+Literal("procurement."+table)+",'UPDATE')"
        +" AND NOT has_table_privilege(current_user,"+Literal("procurement."+table)+",'DELETE,TRUNCATE')";
    private static string DefaultSql(string table,string column,string value)=>"SELECT EXISTS(SELECT 1 FROM pg_attrdef d JOIN pg_attribute a ON a.attrelid=d.adrelid AND a.attnum=d.adnum WHERE d.adrelid="
        +Literal("procurement."+table)+"::regclass AND a.attname="+Literal(column)+" AND pg_get_expr(d.adbin,d.adrelid)="+Literal(value)+")";
    internal static string NoOwnersSql(string[] tables)=>"SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relname::text=ANY("+Columns(tables)
        +") AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal const string CoreOwnershipSql="SELECT NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname='core_approval' AND (has_schema_privilege(current_user,n.oid,'CREATE') OR pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET'))) AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal const string NoCoreWorkflowDmlSql="SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relkind IN ('r','p','v','f') AND (has_any_column_privilege(current_user,c.oid,'INSERT') OR has_any_column_privilege(current_user,c.oid,'UPDATE') OR has_table_privilege(current_user,c.oid,'DELETE,TRUNCATE')))";
    internal const string NoOtherCoreWorkflowDmlSql="SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relkind IN ('r','p','v','f') AND c.relname NOT IN ('cases','stages','assignments','decisions') AND (has_any_column_privilege(current_user,c.oid,'INSERT') OR has_any_column_privilege(current_user,c.oid,'UPDATE') OR has_table_privilege(current_user,c.oid,'DELETE,TRUNCATE')))";

}
