using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public enum CommercialRoundSchemaPresence { Absent, Complete, Partial }
public static class CommercialRoundPlanSchema
{
    public static readonly string[] Tables = [ "commercial_round_enrollments", "commercial_round_plans",
        "commercial_round_plan_revisions", "commercial_round_criteria", "commercial_round_participants", "commercial_round_plan_receipts" ];
    public static async Task<CommercialRoundSchemaPresence> PresenceAsync(ProcurementDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("RoundPlanOwningPostgresRequired");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
        command.CommandText = "SELECT count(*) FROM unnest(ARRAY[" + string.Join(',', Tables.Select(x => "'procurement." + x + "'"))
            + "]::text[]) n WHERE to_regclass(n) IS NOT NULL";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        return count == 0 ? CommercialRoundSchemaPresence.Absent : count == Tables.Length ? CommercialRoundSchemaPresence.Complete : CommercialRoundSchemaPresence.Partial;
    }
}

/// <summary>Attach to the genuinely generated migration, not a hand-numbered migration.
/// Functions are security-invoker; current finite roles never own these objects.</summary>
public static class CommercialRoundPlanRetentionSql
{
    public const string Install = """
CREATE FUNCTION procurement.commercial_round_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Commercial round evidence is append only' USING ERRCODE='55000'; END $$;
CREATE FUNCTION procurement.commercial_round_enroll_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE source procurement.sourcing_events%ROWTYPE;
BEGIN
 SELECT * INTO STRICT source FROM procurement.sourcing_events WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND id=NEW.sourcing_event_id FOR UPDATE;
 IF source.execution_version<>2 OR source.status<>'Draft' OR source.version<>NEW.source_event_version
  OR source.branch_ref IS DISTINCT FROM NEW.branch_id OR source.source_evidence_id IS DISTINCT FROM NEW.source_evidence_id
  OR source.owner_subject_id<>NEW.original_maker_principal_id OR source.approval_instance_ref IS NOT NULL OR source.opens_at IS NOT NULL OR source.closes_at IS NOT NULL
  OR EXISTS(SELECT 1 FROM procurement.sourcing_publication_openings WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_publication_decisions WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_publication_receipts WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_manual_contacts WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_manual_quote_versions WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_awards WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.sourcing_invitations WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR EXISTS(SELECT 1 FROM procurement.supplier_bids WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND sourcing_event_id=source.id)
  OR NOT EXISTS(SELECT 1 FROM procurement.sourcing_draft_evidence WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND id=NEW.source_evidence_id AND sourcing_event_id=source.id AND schema_version=2 AND payload_sha256=NEW.source_sha256)
 THEN RAISE EXCEPTION 'Round source is no longer untouched' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE FUNCTION procurement.commercial_round_event_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF EXISTS(SELECT 1 FROM procurement.commercial_round_enrollments WHERE tenant_id=OLD.tenant_id AND legal_entity_id=OLD.legal_entity_id AND sourcing_event_id=OLD.id) THEN
  IF (OLD.status='Cancelled' AND NEW.status<>'Cancelled') OR (to_jsonb(NEW)-ARRAY['status','cancellation_reason','updated_at','updated_by','version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','cancellation_reason','updated_at','updated_by','version'])
   OR NEW.status NOT IN ('Draft','Cancelled')
  THEN RAISE EXCEPTION 'Commercial round publication is unavailable' USING ERRCODE='55000'; END IF;
  IF NEW.status='Cancelled' AND OLD.status<>'Cancelled' AND NOT EXISTS(
   SELECT 1 FROM procurement.commercial_round_plans p JOIN procurement.commercial_round_plan_revisions r ON r.tenant_id=p.tenant_id AND r.legal_entity_id=p.legal_entity_id AND r.plan_id=p.id AND r.result_version=p.version
   JOIN procurement.commercial_round_plan_receipts c ON c.tenant_id=r.tenant_id AND c.legal_entity_id=r.legal_entity_id AND c.plan_id=r.plan_id AND c.revision_id=r.id AND c.result_version=r.result_version
   WHERE p.tenant_id=OLD.tenant_id AND p.legal_entity_id=OLD.legal_entity_id AND p.sourcing_event_id=OLD.id AND p.status='Cancelled' AND r.result_status='Cancelled' AND r.operation='Cancel' AND c.operation='Cancel')
  THEN RAISE EXCEPTION 'Cancel round plan before source cancellation' USING ERRCODE='55000'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE FUNCTION procurement.commercial_round_complete() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE plan_id_value uuid; current_plan procurement.commercial_round_plans%ROWTYPE; current_revision procurement.commercial_round_plan_revisions%ROWTYPE;
BEGIN
 IF TG_TABLE_NAME='commercial_round_enrollments' THEN
  SELECT id INTO STRICT plan_id_value FROM procurement.commercial_round_plans WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND enrollment_id=NEW.id;
 ELSIF TG_TABLE_NAME='commercial_round_plans' THEN plan_id_value=NEW.id;
 ELSE plan_id_value=NEW.plan_id; END IF;
 SELECT * INTO STRICT current_plan FROM procurement.commercial_round_plans WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND id=plan_id_value;
 SELECT * INTO STRICT current_revision FROM procurement.commercial_round_plan_revisions WHERE tenant_id=current_plan.tenant_id AND legal_entity_id=current_plan.legal_entity_id AND plan_id=current_plan.id AND result_version=current_plan.version;
 IF TG_TABLE_NAME='commercial_round_plan_revisions' THEN
  IF NEW.id<>current_revision.id THEN RAISE EXCEPTION 'Round revision is not the current result' USING ERRCODE='23514'; END IF;
 ELSIF TG_TABLE_NAME IN ('commercial_round_criteria','commercial_round_participants','commercial_round_plan_receipts') THEN
  IF NEW.revision_id<>current_revision.id THEN RAISE EXCEPTION 'Round evidence is not the current result' USING ERRCODE='23514'; END IF;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM procurement.commercial_round_enrollments WHERE tenant_id=current_plan.tenant_id AND legal_entity_id=current_plan.legal_entity_id AND id=current_plan.enrollment_id AND enrolled_by_principal_id=current_plan.maker_principal_id)
  OR current_revision.result_status<>current_plan.status OR current_revision.content_sha256<>encode(sha256(convert_to(current_revision.content_json,'UTF8')),'hex')
  OR current_revision.criterion_count<>(SELECT count(*) FROM procurement.commercial_round_criteria WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND revision_id=current_revision.id)
  OR current_revision.participant_count<>(SELECT count(*) FROM procurement.commercial_round_participants WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND revision_id=current_revision.id)
  OR NOT EXISTS(SELECT 1 FROM procurement.commercial_round_plan_receipts WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND plan_id=current_plan.id AND revision_id=current_revision.id AND result_version=current_plan.version AND operation=current_revision.operation AND actor_principal_id=current_revision.actor_principal_id AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex'))
 THEN RAISE EXCEPTION 'Round plan graph incomplete' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE FUNCTION procurement.commercial_round_plan_update() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Cancelled') OR NEW.version<>OLD.version+1
  OR (to_jsonb(NEW)-ARRAY['status','updated_at','updated_by','version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','updated_at','updated_by','version'])
 THEN RAISE EXCEPTION 'Round plan identity/state transition invalid' USING ERRCODE='55000'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER commercial_round_plan_update BEFORE UPDATE ON procurement.commercial_round_plans FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_plan_update();
CREATE TRIGGER commercial_round_plan_delete BEFORE DELETE ON procurement.commercial_round_plans FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_immutable();
CREATE TRIGGER commercial_round_enroll_guard BEFORE INSERT ON procurement.commercial_round_enrollments FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_enroll_guard();
CREATE TRIGGER commercial_round_event_barrier BEFORE UPDATE ON procurement.sourcing_events FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_event_barrier();
CREATE CONSTRAINT TRIGGER commercial_round_enroll_complete AFTER INSERT ON procurement.commercial_round_enrollments DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_complete();
CREATE CONSTRAINT TRIGGER commercial_round_plan_complete AFTER INSERT OR UPDATE ON procurement.commercial_round_plans DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_complete();
""";
    public static string AppendOnlyTriggers => string.Join('\n', CommercialRoundPlanSchema.Tables.Where(x => x != "commercial_round_plans")
        .Select(x => $"CREATE TRIGGER commercial_round_append_only BEFORE UPDATE OR DELETE ON procurement.{x} FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_immutable();"))
        + "\n" + string.Join('\n', CommercialRoundPlanSchema.Tables.Where(x => x is not ("commercial_round_plans" or "commercial_round_enrollments"))
            .Select(x => $"CREATE CONSTRAINT TRIGGER commercial_round_evidence_complete AFTER INSERT ON procurement.{x} DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.commercial_round_complete();"));
    public const string RetainedDownGuard = """
DO $$ BEGIN IF EXISTS(SELECT 1 FROM procurement.commercial_round_enrollments) OR EXISTS(SELECT 1 FROM procurement.commercial_round_plans) OR EXISTS(SELECT 1 FROM procurement.commercial_round_criteria) OR EXISTS(SELECT 1 FROM procurement.commercial_round_participants) OR EXISTS(SELECT 1 FROM procurement.commercial_round_plan_revisions) OR EXISTS(SELECT 1 FROM procurement.commercial_round_plan_receipts) OR EXISTS(SELECT 1 FROM procurement.audit_events WHERE record_type IN ('CommercialRoundEnrollment','CommercialRoundPlan','CommercialRoundPlanRevision','CommercialRoundCriterion','CommercialRoundParticipant','CommercialRoundPlanReceipt')) OR EXISTS(SELECT 1 FROM procurement.outbox_events WHERE event_type LIKE 'commercial_round.plan_%') THEN RAISE EXCEPTION 'Retain round enrollment, history and publication barrier; destructive Down denied' USING ERRCODE='55000'; END IF; END $$;
""";
    // Execute only AFTER RetainedDownGuard in the genuinely generated Down, and
    // before generated table drops. No populated rollback disables the barrier.
    public static string UninstallEmpty => "DROP TRIGGER commercial_round_event_barrier ON procurement.sourcing_events;\n"
        + string.Join('\n', CommercialRoundPlanSchema.Tables.Where(x => x != "commercial_round_plans")
            .Select(x => $"DROP TRIGGER commercial_round_append_only ON procurement.{x};"))
        + "\nDROP TRIGGER commercial_round_plan_update ON procurement.commercial_round_plans;"
        + "\nDROP TRIGGER commercial_round_plan_delete ON procurement.commercial_round_plans;"
        + "\nDROP TRIGGER commercial_round_enroll_guard ON procurement.commercial_round_enrollments;"
        + "\nDROP TRIGGER commercial_round_enroll_complete ON procurement.commercial_round_enrollments;"
        + "\nDROP TRIGGER commercial_round_plan_complete ON procurement.commercial_round_plans;\n"
        + string.Join('\n', CommercialRoundPlanSchema.Tables.Where(x => x is not ("commercial_round_plans" or "commercial_round_enrollments"))
            .Select(x => $"DROP TRIGGER commercial_round_evidence_complete ON procurement.{x};"))
        + "\nDROP FUNCTION procurement.commercial_round_immutable();"
        + "\nDROP FUNCTION procurement.commercial_round_enroll_guard();"
        + "\nDROP FUNCTION procurement.commercial_round_event_barrier();"
        + "\nDROP FUNCTION procurement.commercial_round_complete();"
        + "\nDROP FUNCTION procurement.commercial_round_plan_update();";

}
