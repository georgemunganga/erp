# ADR — Separate original-unit review authority from matching and Finance history

Status: Proposed, 20 October 2026. Related specification: `docs/procurement/PROC-11-ORIGINAL-UNIT-REVIEW-AND-EXPOSURE-OWNER.md`. No implementation/activation accompanies this record.

## Context

P26 production uses unavailable original-unit review and complete-history owners. Core documents prove stored content/version/hash/scan status, not commercial line meaning. Existing Finance references and payroll integration operations do not own complete Procurement AP exposure. Current review envelope conflates historical reviewer with current comparison caller, so it cannot simply be reused for independent persisted review.

## Proposed decision

Implement a bounded governed Procurement original-document/unit review using the existing Core approval/document/access services. Retain independent reviewer authority and exact original/mapping/document/policy pins, separately check the current matcher identity and scope. Keep immutable proposal/review history; choose the smallest relational representation after review of existing mapping evidence fields. Do not copy a historic approval into a new caller's identity.

Keep the Finance exposure owner unavailable until an actual authenticated versioned complete-history/cutover/lease/revocation/reconciliation contract is supplied. Local absence, funding evidence and operator CSV reconciliation cannot replace completeness. A review outcome does not approve an invoice, create AP liability, qualify a capacity claim or execute payment.

## Alternatives and consequences

Auto-verifying submitted units/scan status would fabricate authority. Treating local empty claims as complete history risks duplicate consumption. Copying a payroll integration service duplicates the wrong domain and grants irrelevant access. Building a second AP ledger is outside Procurement ownership. Independent unit review adds a useful real capability now, while truthful blocked Finance status remains until its genuine owner exists. Source, security, model/migration and actual API/PG/UI gates remain mandatory before release.
