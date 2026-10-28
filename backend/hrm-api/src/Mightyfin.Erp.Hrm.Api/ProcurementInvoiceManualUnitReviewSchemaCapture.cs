using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Filled only from Root's genuine migration and native/restore proof.
/// Native CHECK captured in receipt 2eab16a2; restore pin remains unset.
/// Pins grant no actor, policy or workflow authority.</summary>
internal static class ProcurementInvoiceManualUnitReviewSchemaCapture
{
    internal const string MigrationId="20261028211409_M6cManualInvoiceUnitAttestation";
    internal const string NativeOutcomeCheck="CHECK ((((state)::text = ANY ((ARRAY['Verified'::character varying, 'Returned'::character varying, 'Rejected'::character varying])::text[])) AND (evidence_schema_version = ANY (ARRAY[3, 4])) AND (final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((reviewer_worker_id IS NULL) OR (reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((reviewer_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((reviewer_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(evidence_json) >= 1) AND (octet_length(evidence_json) <= 524288)) AND isfinite(completed_at)))";
    internal const string RestoredOutcomeCheck="";
    internal static bool MatchesOutcome(string actual)=>NativeOutcomeCheck.Length>0
        && (actual==NativeOutcomeCheck || RestoredOutcomeCheck.Length>0 && actual==RestoredOutcomeCheck);
    internal static async Task<bool> IsInstalledAsync(ProcurementDbContext db,CancellationToken ct)=>
        MigrationId.Length>0 && NativeOutcomeCheck.Length>0
        && (await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal);
}
