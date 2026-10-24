using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static class PrincipalApprovalRuntimePrivileges
{
    internal sealed record Connections(string Record,string Execution,string RecordRole,string ExecutionRole);
    internal static async Task<Connections> CreateAsync(string owner,PrincipalApprovalFixture fixture,bool productionPooling=false)
    {
        var suffix=Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var record="p25_record_"+suffix;var execution="p25_execute_"+suffix;
        var recordPassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var executionPassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var raw=new NpgsqlConnection(owner);await raw.OpenAsync();
        async Task Run(string text) {await using var command=new NpgsqlCommand(text,raw);await command.ExecuteNonQueryAsync();}
        // Password-bearing fixture DDL must never enter server statement logs,
        // including error context. These settings affect this owner session only.
        await Run("SET log_statement='none'; SET log_min_error_statement='panic'");
        // Unique private fixture logins; no membership/ownership/superuser/schema creation.
        foreach(var (role,password) in new[]{(record,recordPassword),(execution,executionPassword)})
        {
            await using(var exists=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role)",raw))
            {exists.Parameters.AddWithValue("role",role);PrincipalApprovalHttpVerification.Need(await exists.ExecuteScalarAsync() is false,"Private role collision; no reuse.");}
            await Run("CREATE ROLE "+Q(role)+" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '"+password+"'");
            await Run("GRANT CONNECT ON DATABASE "+Q(raw.Database)+" TO "+Q(role));
            await Run("GRANT USAGE ON SCHEMA procurement,core_approval,hrm TO "+Q(role));
        }
        await using var procurement=fixture.Procurement();var model=procurement.GetService<IDesignTimeModel>().Model;
        Type[] sources=[typeof(PurchaseRequest),typeof(PurchaseRequestLine),typeof(BudgetCheckSnapshot),typeof(ProcurementCategory),typeof(PolicyVersion),typeof(PolicyEvaluation),typeof(PurchaseRequestApprovalSubmission),typeof(PurchaseRequestAssignmentAuthorityEvidence),typeof(ProcurementSpendingAuthorityGrant),typeof(ProcurementSpendingAuthorityCategory),typeof(SpendingAuthorityIdentityRecord),typeof(PurchaseRequestApprovalOutcome),typeof(PurchaseRequestFinanceReleaseRequest),typeof(PurchaseRequestFinanceReleaseConfirmation),typeof(ProcurementApprovalCommandReceipt),typeof(AuditEvent),typeof(OutboxEvent)];
        foreach(var type in sources)
        {
            var entity=model.FindEntityType(type)??throw new InvalidOperationException("Missing fixture owning model.");
            var relation=Q(entity.GetSchema()!)+"."+Q(entity.GetTableName()!);
            await Run("GRANT SELECT ON "+relation+" TO "+Q(record)+","+Q(execution));
        }
        await Run("GRANT SELECT ON procurement.__procurement_migrations TO "+Q(record)+","+Q(execution));
        // Real PR draft validation reads these owning catalog tables, even for
        // an empty selected-ID page. This is a bounded record-route dependency,
        // not a grant to alter catalog data or Core/Finance records.
        foreach(var table in new[]{"catalog_items","catalog_entries"})
            await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(record));
        foreach(var table in new[]{"purchase_requests","purchase_request_lines","spending_authority_grants","policy_versions","procurement_categories"})
            await Run("GRANT INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(record));
        await Run("GRANT INSERT,DELETE ON procurement.spending_authority_categories TO "+Q(record));
        foreach(var table in new[]{"budget_check_snapshots","spending_authority_identity_evidence","policy_evaluations","audit_events","outbox_events"})
            await Run("GRANT INSERT ON procurement."+Q(table)+" TO "+Q(record));
        foreach(var table in new[]{"purchase_requests","purchase_request_lines"})await Run("GRANT UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
        foreach(var table in new[]{"purchase_request_approval_submissions","assignment_authority_evidence","purchase_request_approval_outcomes","purchase_request_finance_release_requests","purchase_request_finance_release_confirmations","approval_command_receipts","audit_events","outbox_events"})await Run("GRANT INSERT ON procurement."+Q(table)+" TO "+Q(execution));
        if(fixture.PrincipalRfqProfile)
        {
            // Separate P29-only finite manifest. The record role configures a
            // genuine buying method; only the execution role can hold/release
            // approved demand. Neither role owns schema, Core identity or Finance.
            await Run("GRANT SELECT,INSERT,UPDATE ON procurement."+Q("procurement_methods")+" TO "+Q(record));
            foreach(var table in new[]{"sourcing_events","sourcing_lines","sourcing_draft_evidence","sourcing_demand_bindings",
                "sourcing_command_receipts","purchase_request_allocations","procurement_methods","purchase_orders",
                "purchase_order_lines","catalog_items","catalog_entries","suppliers"})
                await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"sourcing_events","sourcing_lines","purchase_request_allocations"})
                await Run("GRANT INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"sourcing_draft_evidence","sourcing_demand_bindings","sourcing_command_receipts"})
                await Run("GRANT INSERT ON procurement."+Q(table)+" TO "+Q(execution));
        }
        if(fixture.PrincipalRfqProfile && Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RFQ_PUBLICATION")=="1")
        {
            foreach(var table in new[]{"suppliers","supplier_contacts","supplier_qualifications"})
                await Run("GRANT SELECT,INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(record));
            foreach(var table in new[]{"supplier_decisions","sourcing_policy_identity_evidence"})
                await Run("GRANT SELECT,INSERT ON procurement."+Q(table)+" TO "+Q(record));
            foreach(var table in new[]{"sourcing_publication_openings","sourcing_publication_decisions",
                "sourcing_publication_receipts","sourcing_manual_contacts","sourcing_manual_quote_versions",
                "sourcing_policy_identity_evidence"})
                await Run("GRANT SELECT,INSERT ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"sourcing_invitations","supplier_bids","supplier_bid_lines"})
                await Run("GRANT SELECT,INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
            await Run("GRANT SELECT ON procurement.supplier_qualifications TO "+Q(execution));
        }
        if(fixture.PrincipalPoProfile)
        {
            // P30 profile alone: vendor master setup uses the record role; the
            // execution role may write only owned PO/source/receipt evidence.
            foreach(var table in new[]{"suppliers","supplier_contacts","supplier_qualifications","procurement_methods"})
                await Run("GRANT SELECT,INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(record));
            await Run("GRANT SELECT,INSERT ON procurement.supplier_decisions TO "+Q(record));
            foreach(var table in new[]{"suppliers","supplier_qualifications","procurement_methods","catalog_items","catalog_entries",
                "purchase_orders","purchase_order_lines","principal_order_source_bindings","principal_order_draft_receipts",
                "principal_order_funding_evidence","principal_order_submissions","principal_order_assignments",
                "principal_order_decisions","principal_order_issued_versions","principal_order_acknowledgments",
                "principal_order_release_requests","principal_order_release_confirmations","purchase_request_allocations",
                "purchase_request_approval_submissions","purchase_request_approval_outcomes","budget_check_snapshots",
                "principal_receiving_sources"})
                await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"purchase_orders","purchase_order_lines","purchase_request_allocations"})
                await Run("GRANT INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"principal_order_source_bindings","principal_order_draft_receipts",
                "principal_order_funding_evidence","principal_order_submissions","principal_order_assignments",
                "principal_order_decisions","principal_order_issued_versions","principal_order_acknowledgments",
                "principal_order_release_requests","principal_order_release_confirmations"})
                await Run("GRANT INSERT ON procurement."+Q(table)+" TO "+Q(execution));
            if(Environment.GetEnvironmentVariable("P33_HTTP_VERIFY_PRINCIPAL_PO_EDIT")=="1")
            {
                // The separate P33 proof only adds its three immutable owned
                // families. Parent PO, allocation and receipt writes keep the
                // same finite P30 grants and current Core action checks.
                foreach(var table in new[]{"principal_order_commercial_origins",
                    "principal_order_draft_commercial_revisions","principal_order_effective_source_slices"})
                    await Run("GRANT SELECT,INSERT ON procurement."+Q(table)+" TO "+Q(execution));
            }
        }
        if(fixture.PrincipalReceivingProfile)
        {
            await Run("GRANT SELECT,INSERT ON procurement.operational_policy_identity_evidence TO "+Q(record));
            await Run("GRANT SELECT ON procurement.operational_policy_identity_evidence TO "+Q(execution));
            foreach(var table in new[]{"principal_receiving_sources","principal_receiving_drafts"})
                await Run("GRANT SELECT,INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"principal_receiving_facts","principal_receiving_revisions","principal_receiving_receipts","principal_receiving_line_bindings"})
                await Run("GRANT SELECT,INSERT ON procurement."+Q(table)+" TO "+Q(execution));
            // Read-only legacy completeness checks do not authorize legacy writes.
            foreach(var table in new[]{"receiving_source_states","goods_receipt_lines","service_acceptances"})
                await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(execution));
        }
        if(fixture.PrincipalInvoiceProfile)
        {
            // Intake remains a Procurement record command. Typed comparison,
            // Core review and exact exposure are confined to the execution role.
            foreach(var table in new[]{"supplier_invoices","supplier_invoice_lines","suppliers",
                "supplier_qualifications","supplier_credit_note_refs","attachment_refs"})
                await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(record)+","+Q(execution));
            foreach(var table in new[]{"supplier_invoices","supplier_invoice_lines"})
                await Run("GRANT INSERT,UPDATE ON procurement."+Q(table)+" TO "+Q(record));
            await Run("GRANT INSERT ON procurement.attachment_refs TO "+Q(record));
            foreach(var table in new[]{"supplier_invoices","supplier_invoice_lines","supplier_credit_note_refs",
                "invoice_match_results","invoice_match_allocations","goods_receipts","service_acceptances",
                "policy_versions","audit_events","outbox_events","attachment_refs"})
                await Run("GRANT SELECT ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"principal_invoice_states","principal_invoice_mapping_lines","principal_invoice_mappings",
                "principal_invoice_unit_reviews","principal_invoice_review_assignments",
                "principal_invoice_review_attestations","principal_invoice_review_outcomes","principal_invoice_comparisons",
                "principal_invoice_exposure_states","principal_invoice_claims",
                "principal_invoice_receipts","principal_invoice_discrepancies",
                "principal_invoice_discrepancy_actions"})
                await Run("GRANT SELECT,INSERT ON procurement."+Q(table)+" TO "+Q(execution));
            foreach(var table in new[]{"principal_invoice_states","principal_invoice_exposure_states",
                "principal_invoice_discrepancies"})
                await Run("GRANT UPDATE ON procurement."+Q(table)+" TO "+Q(execution));
            // Core owns document content and its scan/use history. No document
            // table DELETE/TRUNCATE, schema ownership or general user directory.
            await Run("GRANT USAGE ON SCHEMA core_document TO "+Q(execution));
            foreach(var table in new[]{"documents","versions","scan_evidence","record_events","use_sets","use_pins"})
                await Run("GRANT SELECT,INSERT ON core_document."+Q(table)+" TO "+Q(execution));
            await Run("GRANT UPDATE ON core_document.documents,core_document.versions TO "+Q(execution));
            await Run("GRANT SELECT ON core_document.\"__EFMigrationsHistory\" TO "+Q(execution));
        }
        await using var core=fixture.Core();var coreModel=core.GetService<IDesignTimeModel>().Model;
        // Explicit bounded read manifest for actual current-account/grant/branch/optional-manager queries.
        var reads=new Dictionary<Type,string[]> {
            [typeof(LegalEntity)]=["Id","TenantId","IsArchived","Currency"],
            [typeof(WorkLocation)]=fixture.PrincipalPoProfile
                ?new[]{"Id","TenantId","LegalEntityId","IsArchived","Name"}
                :new[]{"Id","TenantId","LegalEntityId","IsArchived"},
            [typeof(OrgUnit)]=["Id","TenantId","LegalEntityId","IsArchived","Status","EffectiveFrom","EffectiveTo"],
            [typeof(Worker)]=["Id","TenantId","IsArchived","Status","LocationId","OrgUnitId","ManagerId"],
            [typeof(LocalUser)]=["Id","TenantId","WorkerId","RolesCsv","LockedUntil","IsActive","IsArchived"],
            [typeof(HrUserBranchAssignment)]=["TenantId","UserId","LocationId","IsArchived"],
            [typeof(TenantRoleAssignment)]=["TenantId","Active","IsArchived","RoleKey","PermissionsCsv"]};
        foreach(var (type,names) in reads)
        {
            var entity=coreModel.FindEntityType(type)??throw new InvalidOperationException("Missing owning Core model.");var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
            var columns=string.Join(',',names.Select(n=>Q((entity.FindProperty(n)??throw new InvalidOperationException("Missing owning Core read property.")).GetColumnName(table)!)));
            await Run("GRANT SELECT ("+columns+") ON "+Q(table.Schema!)+"."+Q(table.Name)+" TO "+Q(record)+","+Q(execution));
        }
        foreach(var table in new[]{"cases","stages","assignments","decisions"})await Run("GRANT SELECT ON core_approval."+Q(table)+" TO "+Q(record)+","+Q(execution));
        // The retained-worker approval readiness probe reads this Core table;
        // the execution role needs SELECT only, while the record role stays unchanged.
        await Run("GRANT SELECT ON core_approval."+Q("delegations")+" TO "+Q(execution));
        await Run("GRANT SELECT ON core_approval.\"__EFMigrationsHistory\" TO "+Q(record)+","+Q(execution));
        foreach(var table in new[]{"cases","stages","assignments"})await Run("GRANT INSERT,UPDATE ON core_approval."+Q(table)+" TO "+Q(execution));
        await Run("GRANT INSERT ON core_approval.decisions TO "+Q(execution));
        return new(RuntimeConnection(owner,record,recordPassword,productionPooling),
            RuntimeConnection(owner,execution,executionPassword,productionPooling),record,execution);
    }
    internal static string RuntimeConnection(string owner,string role,string password,bool productionPooling)
        => new NpgsqlConnectionStringBuilder(owner)
        {
            Username=role,Password=password,Pooling=productionPooling,
            Multiplexing=false,NoResetOnClose=false,
        }.ConnectionString;

    internal static string CoreRuntimeConnection(string owner)
        => new NpgsqlConnectionStringBuilder(owner)
        {
            Pooling=true,Multiplexing=false,NoResetOnClose=false,
        }.ConnectionString;
    internal static string Q(string value)=>'"'+value.Replace("\"","\"\"")+ '"';
}
