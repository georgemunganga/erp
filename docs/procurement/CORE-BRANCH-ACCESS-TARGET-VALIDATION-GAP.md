# Core branch-access target validation — inherited gap

Status: open, independently tracked source finding; not changed by ADR-003 fencing.

The existing Core POST `/branch-access` validates nonempty `UserId`, duplicate assignment,
and target location existence. It does not validate that UserId resolves to an active,
tenant-owned canonical identity. DELETE acts on an existing tenant-filtered assignment.
The branch fence preserves this API behavior; it does not make arbitrary target IDs valid.

Before tightening the target rule, Core must confirm the supported identity contract for
local and SSO accounts. Forcing LocalUser-only targets could incorrectly reject established
external identities. The follow-up should define canonical target resolution, active/tenant
requirements, error behavior, historical assignments and tests for foreign/missing targets.
No assumption that a UUID alone establishes identity or authorization is approved here.

Owner: Core identity/access engineering. Release relevance: administrative data integrity;
separate from the bounded branch-write/execution linearization guarantee. No automatic
backfill or identity table duplication is proposed.
