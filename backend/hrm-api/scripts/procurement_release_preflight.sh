#!/usr/bin/env bash
set -euo pipefail

# Read-only check of a deployed ERP host and its shared PostgreSQL instance.
# Usage: procurement_release_preflight.sh API_CONTAINER DB_CONTAINER [DB_USER] [DB_NAME]
if [[ $# -lt 2 || $# -gt 4 ]]; then
  printf 'Usage: %s API_CONTAINER DB_CONTAINER [DB_USER] [DB_NAME]\n' "$0" >&2
  exit 64
fi

proc_api_container=$1
proc_db_container=$2
proc_db_user=${3:-erp}
proc_db_name=${4:-erp}
proc_script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
proc_migration_dir="$proc_script_dir/../../procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations"
proc_blockers=0

if [[ ! -d "$proc_migration_dir" ]]; then
  printf 'BLOCKED: Procurement migration sources were not found.\n' >&2
  exit 2
fi
for proc_container in "$proc_api_container" "$proc_db_container"; do
  if ! docker inspect "$proc_container" >/dev/null 2>&1; then
    printf 'BLOCKED: Container %s is unavailable.\n' "$proc_container" >&2
    exit 2
  fi
done

if docker exec "$proc_api_container" sh -lc 'test -n "$(printenv ConnectionStrings__Procurement)"' >/dev/null 2>&1; then
  printf 'OK: ERP API has a non-empty Procurement connection setting.\n'
else
  printf 'BLOCKED: ERP API has no Procurement connection setting.\n'
  proc_blockers=$((proc_blockers + 1))
fi

proc_sql="select count(*) from information_schema.schemata where schema_name = 'procurement'"
if ! proc_schema_count=$(docker exec "$proc_db_container" psql -X -q -t -A -v ON_ERROR_STOP=1 \
    -U "$proc_db_user" -d "$proc_db_name" -c "$proc_sql" 2>/dev/null); then
  printf 'BLOCKED: Cannot read the shared ERP database schema.\n'
  exit 2
fi
if [[ "$proc_schema_count" != "1" ]]; then
  printf 'BLOCKED: Procurement schema is absent.\n'
  proc_blockers=$((proc_blockers + 1))
else
  printf 'OK: Procurement schema exists.\n'
  proc_sql="select count(*) from information_schema.tables where table_schema = 'procurement' and table_name = '__procurement_migrations'"
  proc_history_count=$(docker exec "$proc_db_container" psql -X -q -t -A -v ON_ERROR_STOP=1 \
    -U "$proc_db_user" -d "$proc_db_name" -c "$proc_sql" 2>/dev/null) || {
      printf 'BLOCKED: Cannot read Procurement migration history metadata.\n'
      exit 2
    }
  if [[ "$proc_history_count" != "1" ]]; then
    printf 'BLOCKED: Procurement migration history table is absent.\n'
    proc_blockers=$((proc_blockers + 1))
  else
    proc_sql='select "MigrationId" from procurement.__procurement_migrations order by "MigrationId"'
    proc_applied=$(docker exec "$proc_db_container" psql -X -q -t -A -v ON_ERROR_STOP=1 \
      -U "$proc_db_user" -d "$proc_db_name" -c "$proc_sql" 2>/dev/null) || {
        printf 'BLOCKED: Cannot read applied Procurement migration IDs.\n'
        exit 2
      }
    proc_expected=$(find "$proc_migration_dir" -maxdepth 1 -regextype posix-extended \
      -regex '.*/[0-9]{14}_[^/]+\.cs' ! -name '*.Designer.cs' -printf '%f\n' | sed 's/\.cs$//' | sort)
    if [[ -z "$proc_expected" ]]; then
      printf 'BLOCKED: No versioned Procurement migrations were found in source.\n'
      proc_blockers=$((proc_blockers + 1))
    else
      proc_pending=$(comm -23 <(printf '%s\n' "$proc_expected") <(printf '%s\n' "$proc_applied" | sort))
      proc_unknown=$(comm -13 <(printf '%s\n' "$proc_expected") <(printf '%s\n' "$proc_applied" | sort))
      if [[ -n "$proc_pending" ]]; then
        printf 'BLOCKED: Source migrations not applied:\n%s\n' "$proc_pending"
        proc_blockers=$((proc_blockers + 1))
      fi
      if [[ -n "$proc_unknown" ]]; then
        printf 'BLOCKED: Database has migration IDs absent from this checkout:\n%s\n' "$proc_unknown"
        proc_blockers=$((proc_blockers + 1))
      fi
      if [[ -z "$proc_pending" && -z "$proc_unknown" ]]; then
        printf 'OK: Database migration IDs match this checkout.\n'
      fi
    fi
  fi
fi

if (( proc_blockers > 0 )); then
  printf 'Procurement live-data preflight blocked (%d finding(s)). No changes were made.\n' "$proc_blockers"
  exit 2
fi
printf 'Procurement schema preflight passed. Session, authorization, backup and UI checks remain separate release gates.\n'
