# Core schema startup and runtime ownership

Production and other non-Development environments now verify the owning Core/HRM model and migration history at startup. They do not execute migration DDL by default. An unscaffolded model or pending owning migration prevents startup with an actionable error. Development retains automatic migration unless configured otherwise.

Apply approved migrations using the owning deployment process and appropriate migration credentials before starting a finite runtime. The existing `--apply-migrations-only` path explicitly performs Core/HRM owning migrations and exits before normal bootstrap, outbox or routes. It does not migrate Procurement or shared approvals; those retain their existing owning processes.

`ERP:Database:MigrateOnStartup` is an explicit boolean override (environment key `ERP__Database__MigrateOnStartup`). Production operators should leave it unset or false. Runtime credentials need schema usage and read access to owning migration history, plus the separately reviewed application privileges. They do not need DDL rights for this startup verification. Changing this switch does not revoke any existing over-privileged deployment account.

Release verification must include a fully migrated finite-role startup, pending-migration refusal, and the owning migration rehearsal. Pure startup mode tests do not establish a successful production deployment. This change has not been deployed to production as part of the payment implementation checkpoint.
