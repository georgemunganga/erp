namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>Database guard for the owning migration and reviewed classic extension.</summary>
public static class MobileWorkdaySchema
{
    public const string GuardSql="""
        CREATE OR REPLACE FUNCTION hrm.mobile_workday_evidence_guard() RETURNS trigger
        LANGUAGE plpgsql AS $guard$
        BEGIN
          IF TG_OP <> 'INSERT' THEN
            RAISE EXCEPTION 'Mobile workday evidence is append-only' USING ERRCODE='23514';
          END IF;
          IF NEW.revision < 1 OR NEW.kind NOT IN ('break-start','break-end','departure','return','report')
             OR NEW.state_after NOT IN ('working','on_break','away') OR NEW.is_archived THEN
            RAISE EXCEPTION 'Invalid mobile workday event' USING ERRCODE='23514';
          END IF;
          IF NOT EXISTS (
            SELECT 1 FROM hrm.attendance_records a JOIN hrm.work_locations l
              ON l.id=a.location_id AND l.tenant_id=a.tenant_id
            WHERE a.id=NEW.attendance_record_id AND a.tenant_id=NEW.tenant_id
              AND a.worker_id=NEW.worker_id AND a.location_id=NEW.location_id
              AND l.legal_entity_id=NEW.legal_entity_id AND NOT a.is_archived
              AND NOT l.is_archived AND a.source='mobile' AND a.clock_in IS NOT NULL
          ) THEN RAISE EXCEPTION 'Workday attendance scope mismatch' USING ERRCODE='23514'; END IF;
          IF NEW.revision <> 1 + COALESCE((SELECT max(revision) FROM hrm.mobile_workday_events
             WHERE tenant_id=NEW.tenant_id AND attendance_record_id=NEW.attendance_record_id),0)
          THEN RAISE EXCEPTION 'Workday revision changed' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $guard$;
        DO $install$ BEGIN
          IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid='hrm.mobile_workday_events'::regclass
             AND tgname='mobile_workday_evidence_guard' AND NOT tgisinternal) THEN
            CREATE TRIGGER mobile_workday_evidence_guard BEFORE INSERT OR UPDATE OR DELETE
              ON hrm.mobile_workday_events FOR EACH ROW EXECUTE FUNCTION hrm.mobile_workday_evidence_guard();
          END IF;
        END $install$;
        """;
}
