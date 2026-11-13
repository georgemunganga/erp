# Invitation delivery operations

Core owns this local command and its delivery audit. It is an operator capability,
not a customer resend API. The normal invitation worker remains responsible for
automatic delivery. All operations flags default off. No production activation
is implied by the presence of this command.

## Authority and configuration

Run the versioned API executable under a dedicated, authorized OS/service account:

```text
dotnet Mightyfin.Erp.Hrm.Api.dll --run-invitation-delivery-operations --operation=report
```

Supply configuration through the deployment's protected environment/secret store;
never put connection strings, SMTP credentials or keys in shell arguments.
Set `ERP:CompanyInvitationOperations:Enabled=true` and the dedicated
`ConnectionStrings:InvitationOperations`. There is deliberately no fallback to
the ordinary application connection. Report mode needs no SMTP, identity or
licensing configuration. Its PostgreSQL role should have only CONNECT on the
selected database, USAGE on `hrm`, and SELECT on `hrm.outbox_messages`.

The process opens no HTTP listener, starts no hosted worker, applies no migration
and bootstraps no identity, including in Development or with startup migration
configuration present. Reporting uses a repeatable-read, read-only transaction.
The database owner must provision the schema separately before operations run.

Retry additionally requires `ERP:CompanyInvitationOperations:RetryEnabled=true`,
every reviewed identity/licensing/team/invitation/acceptance/delivery prerequisite
in [README.md](README.md), the original deployment's readable Data Protection
key ring and the ordinary mandatory STARTTLS SMTP configuration. Automatic key
generation is disabled in this process: do not create a replacement key ring to
make old invitations readable. Use a separately authorized database role with
the existing delivery path's required reads/writes. Do not grant monitor-role
write permissions just to enable retries.

Set `ERP:CompanyInvitationOperations:Operator` in trusted service configuration
to an accountable stable identifier, 8–64 lowercase ASCII letters, digits,
underscores or hyphens, starting with a letter or digit. Email addresses and
free-text identities are rejected. This field is not authentication: restrict
who can execute the command, read its secrets and change its configuration.
Use distinct service identities when accountability requires distinct operators.

## Monitoring contract

Report output is one JSON object with `schema_version:1`, `status`,
`delivery_enabled` and `report`. The report contains aggregate gauges `Queued`,
`RetryDue`, `RetryWaiting`, `Accepted`, `Suppressed`, `Invalid`, `Exhausted`,
`Unrecognized` and `OldestDueAt`. It spans the selected deployment/database;
it is not a tenant/customer endpoint. It exposes no recipient, invitation URL,
payload, SMTP settings or raw transport error. Gauges count retained non-archived
delivery intents; they are not interval throughput or inbox-delivery metrics.
`Unrecognized` can overlap state gauges for inconsistent metadata.

`status:attention` signals invalid/exhausted/unrecognized rows or due work older
than `ERP:CompanyInvitationOperations:StaleAfterSeconds` (default 600; allowed
30–86400). The deployment owner must select its alert threshold and schedule,
route alerts to its support owner and investigate historical terminal counts
rather than assuming each poll is a new incident. `delivery_enabled` reports
configured flags only; it does not prove a worker is running or SMTP is healthy.
Suppression requires separate authority investigation; SMTP acceptance does not
prove inbox receipt. Do not publish operator reports to customer analytics.

Exit codes: `0` healthy report or accepted retry; `2` disabled/invalid arguments
or missing dedicated connection; `3` attention/non-accepted outcome; `4`
unavailable/unconfirmed execution. A 45-second command budget bounds work.
Errors use fixed status values without exception details. Alert on failed or
missing observations as well as `attention`.

## One selected due retry

Inspect the owned delivery record through an authorized internal process. Then
use canonical lowercase nonzero GUIDs and the observed attempt count:

```text
dotnet Mightyfin.Erp.Hrm.Api.dll --run-invitation-delivery-operations --operation=retry --tenant=<tenant-guid> --event=<delivery-public-guid> --command-key=<new-command-guid> --expected-attempts=1 --reason=relay-recovered
```

Allowed reasons: `relay-recovered`, `connectivity-restored`, `operator-verified`.
There is no bulk retry, reschedule, backoff override or counter reset. Only a
matching due `failed` row with 1–7 previous attempts, the fixed unconfirmed
transport outcome and a current pending/unexpired invitation is eligible.
The existing delivery path rechecks the original inviter, company, license,
envelope/receipt binding and protected link. It retains the same row, link and
SMTP Message-ID, and stops after at most eight attempts. Invalid, suppressed,
accepted, exhausted, revoked and expired targets cannot be revived here.

The row lock serializes normal and manual workers; a competing worker skips it.
The normal delivery outcome and a separate
`core.invitation.delivery.operator.retry` audit receipt commit atomically.
The receipt includes stable operator identity, hashed command correlation,
event ID, allowlisted reason, expected attempt count and recorded outcome.
It contains no address or delivery link. A cancellation rolls back database
changes; mail already accepted by SMTP cannot be recalled.

If the result is lost, repeat the **same** operator identity, tenant, event,
command key, expected attempt count and reason. A matching receipt returns
`Existing` with `historical:true` and makes no new attempt. A changed body for
that scoped key conflicts. An existing receipt describes the recorded attempt,
not a fresh authority check or current invitation state. `BusyOrMissing` does
not distinguish a locked record from an absent/foreign target. After an
unconfirmed result, reconcile the receipt/current state before selecting any
new command. At-least-once SMTP semantics still permit duplicate mail after an
acceptance/commit crash; Message-ID is not an exactly-once guarantee.

Terminal recovery requires diagnosis and an explicitly authorized normal
invitation workflow, including revocation/new issuance where appropriate.
Never modify outbox/audit rows, reset counters or revive expired links manually.
Retain operator evidence under the deployment's audit/retention policy.

## Qualification and release

Run `CoreCompanyInvitationOperationsTests`, the existing invitation mail/store
suite, `CoreSchemaStartupTests` and the private SMTP wire gate against fresh
isolated PostgreSQL fixtures. Preserve failed attempts and immutable binary
inputs. Also qualify the actual executable's report using a SELECT-only role,
its disabled/rejected startup paths, and unchanged data/identity/schema state.
Private qualification does not establish production privileges, SMTP sender
deliverability, live operator authorization, monitoring ownership or recovery
readiness. Keep activation off until those deployment-specific checks pass.

The separate [operator executable composition](OPERATOR-EXECUTABLE-QUALIFICATION.md)
exercises the actual CLI against private Laravel licensing and SMTP, including
historical replay, conflicting arguments and actual subscription suspension.
Its test gateway/seeded setup scope and deployment-specific limits are explicit.
