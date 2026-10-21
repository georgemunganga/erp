# ERP engineering standard

New Core, HRM, Procurement, and other ERP work follows [GW-ENG-STD-001](docs/engineering/GW-ENG-STD-001.md). This ERP programme is treated as Tier 3 because it handles employee and financial procurement data with tenant/entity boundaries. Apply the existing 28-section child-feature documentation where relevant.

Before enabling a live workflow, trace its UI actions to domain records, states/transitions, server permissions and data scope, business rules, API/events, audit, failure recovery, and acceptance evidence. Core owns shared identity, organisation, branding, access, audit, documents and integration contracts; each module owns its business decisions and records. Do not present browser-local Procurement data as persisted ERP transactions.

For multi-computer collaboration, publish reviewed prerequisite merges before assigning the next milestone. Maintain `MANUS_HANDOFF.md` with exact published ERP/UI main baselines, ownership, acceptance evidence and outstanding release holds. Do not give collaborators stale branch tips or unpublished commit IDs.
