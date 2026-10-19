# Procurement policy v1 rule contract

**Status:** Design draft with a structural validator implemented, 19 October 2026
**Owner:** Procurement governance and ERP Core
**Depends on:** [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md), [ADR-002](ADR-002-SHARED-APPROVAL-BOUNDARY.md)

## Purpose

Define a bounded, testable policy format before a draft can be published or used to approve spending. The policy-draft API stores a JSON object for review. `POST /api/procurement/v1/configuration/policies/validate` now checks the proposed v1 structure and returns field-specific issues without saving or activating it. The resolver and publication gates below remain design requirements. This is not an approved Amizo Power purchasing policy.

ERPNext documents workflow states, transition rules and conditional approvals in its [workflow guide](https://docs.frappe.io/erpnext/workflows). GreenWebb should use those concepts while keeping company scope, a typed rule schema and domain guards explicit. No policy field will contain executable code or database expressions.

## Envelope and ownership

Each published version belongs to one tenant, legal entity and `policyKey`. It has a unique integer version, effective start/end, author, independent reviewer, review reason, published timestamp and immutable rule document. Procurement owns the rule content. ERP Core owns the approval task/decision mechanics. Finance owns budget balances and reservation confirmation. Transactions pin both the policy ID/version and their own submitted record version.

The first policy keys are `PURCHASE_REQUEST` and `PURCHASE_ORDER`. Other transaction types need a separate documented schema profile before they are enabled. The company's selected currency is the default for authoring; a rule must declare its currency for monetary comparisons. A transaction in another currency requires an approved conversion source or a different applicable rule and cannot silently compare raw amounts.

## Candidate JSON shape

```json
{
  "schemaVersion": 1,
  "transactionType": "PurchaseRequest",
  "currencyCode": "ZMW",
  "amountBasis": "EstimatedGross",
  "rules": [
    {
      "code": "EXAMPLE_RULE",
      "priority": 10,
      "when": {
        "minimumAmountInclusive": null,
        "maximumAmountExclusive": null,
        "categoryCodes": [],
        "purchaseTypes": [],
        "branchIds": []
      },
      "result": {
        "budgetReservation": "Required",
        "sourcingMethodCode": null,
        "minimumQuoteCount": null,
        "approvalStages": [
          { "code": "NEED", "assignee": "LineManager", "mode": "One" }
        ]
      }
    }
  ]
}
```

The example contains **no amount or sourcing threshold**. Business owners must set those values and authorize the policy before publication. `null` amount bounds mean unbounded; an empty dimension list means no restriction for that dimension. A rule with no conditions can serve as an explicit fallback. The editor must display these meanings in plain language.

The structural validator limits documents to 64 KB and 100 rules, rejects unsupported fields, invalid amount bounds and duplicate rule or stage codes, and requires explicit control results. A successful validation response only means the JSON shape is acceptable. It does not verify referenced categories, branches, methods, reviewer authority, rule coverage or overlap; those checks belong to the publication workflow.

The live Procurement Settings editor now builds this JSON from labeled fields for amount bands, categories, purchase type, quote count, budget handling and approvers. It uses the selected company's currency and validates before saving a draft. It refuses to edit a draft with unsupported detailed conditions so those conditions cannot be silently discarded. Publication and decision preview are still absent.

A draft can now be sent to `PendingReview` by its author with a reason and current record version. That transition validates the v1 shape, workflow key, selected company currency and active category, branch and buying-method references; it records the requester and timestamp. Review evidence columns have been added for this step and future publication. `PendingReview` has no spend authority, and published policy resolution is not implemented yet.

Reviewers can run a synthetic preview against a saved draft or pending version. The preview returns the winning rule, budget requirement, buying method, quote count, approval stages, and coverage issues, with `isAuthority: false`. Coverage analysis requires one unrestricted fallback below specific rules and flags same-priority overlap. This is explanatory only: it does not check a real request, reserve budget, open approval tasks or publish a policy. Branch-specific samples still need a fuller UI input, though the evaluator accepts a branch ID through its API contract.

## Deterministic evaluation

1. Resolve the one Published policy version effective at the decision timestamp for the tenant, company and key. Missing or overlapping published versions block the decision.
2. Validate transaction currency and amount basis; reject unknown categories, methods and company references.
3. Evaluate each active PR line against rules in descending priority. Exactly one rule must win for each line. Equal-priority overlapping matches are a publication error, not a runtime tie-break.
4. Return a decision explanation containing policy ID/version, transaction version, line ID, matched rule, required sourcing route, budget requirement, approval stages and rejected/unmatched reasons. Preserve it as a policy evaluation record.
5. Merge required approval stages across lines through a documented union rule; do not silently remove a stricter stage because another line has a lower value. The final stage list is opened in ERP Core only after current Finance evidence and employee/organization references are verified.

Money comparisons use decimal arithmetic in the declared currency. Start bounds are inclusive; end bounds are exclusive. Effective times are UTC. A scheduled successor does not rewrite decisions already pinned to an earlier version.

## Publish and approval gates

Publication requires structural validation, reference checks, amount-range coverage, overlap detection, a synthetic preview, a reason and a reviewer distinct from the author. Every approval stage needs an assignable Core role/relationship and a positive quorum. `NoApproval` is an explicit policy result only where governance authorizes it and never bypasses a required Finance reservation or legal/PO control. Published rules are immutable; change creates a successor version.

The resolver can return `MissingPolicy`, `CurrencyMismatch`, `NoMatchingRule`, `AmbiguousRule`, `InactiveReference` or `Valid`. These are business results, not server errors. No failure state grants purchase authority.

## Test obligations

Test rule boundaries, categories and mixed-line requests; no fallback, multiple matches, currency mismatch, expired and overlapping versions; stale PR version, Finance result and policy version; creator/reviewer separation; cross-company IDs; delegation and quorum; concurrent publication; retry/idempotency; and a prior request retaining its original decision after a successor policy is published. Test the UI with plain-language preview, denied access and correction paths before enabling publication in production.
