# P25 receiving probe purchase-type fixture correction

## Retained first failure

The actual receiving probe created approved PR demand, converted it to a PO (201) and recorded Finance confirmation (200), then PO submission returned 409 `OrderPolicyNeedsSourcingOrReview`. The source used purchaseType `Goods`, which produces `PolicyRulePreview.InvalidScenario`: procurement policy codes require uppercase. This was not a failed conversion or an atomic rollback defect. The retained failed disposable database/log must not be reused or erased as part of this correction.

## Narrow fixture correction

Receiving classification remains `Goods` / `Service`. The fixture now explicitly maps these to procurement purchase-type codes `GOODS` / `SERVICE`, consistently on the approved source request and independently reviewed receiving policy. Before creating any demand, both codes are evaluated against the real initial PO policy via `PolicyRulePreview`; unexpected status/stages stop the fixture with kind/code/status/issues diagnostics. Unsupported receiving kinds fail closed. All existing real PR, Finance, independent approval, issue and delivery routes remain required; no fabricated authority or weakened product validation.

This source change has not been compiled or executed. Parent owns the bounded tool rebuild and fresh guarded disposable runtime rehearsal.

## Separate product contract gap found by frontend audit

At the audited `procurement-final-ui-integration` source, `LiveRequests.tsx` assigns catalog `price.kind` directly to request `purchaseType` (line 202), while `LiveItems.tsx` uses `Goods`, `Service`, `Asset`, `Subscription` (lines 15/91). Noncatalog request Type is unrestricted text (`LiveRequests.tsx:251`). The backend `ProcurementRequestRoutes.cs` trims `input.PurchaseType` without canonical code validation, whereas `PolicyRulePreview.Evaluate` requires uppercase optional codes.

Therefore the fixture mismatch also exposes a real UI-to-policy contract gap: an ordinary catalog request can retain a mixed-case item classification as a policy code and fail downstream evaluation. Fixing that requires a separate coordinated intake/catalog/validation contract with regression tests and historical-data handling; this fixture commit does not silently normalize existing approved intent or change product guards.

## Follow-up: real intake canonicalization coverage (#32)

After editable request intake gained strict ASCII code normalization, the receiving probe now sends catalog-style display `Goods` / `Service` into the **real new PR draft route**, then completes real submission, Finance confirmation and independent PR approval. Immediately before PO conversion, it asserts the returned approved source line contains the explicit `GOODS` / `SERVICE` code. Policy fixtures and their preflight remain explicitly uppercase; no consumer guesses or historical approved-record updates are introduced. A failed assertion includes input, expected code and actual retained code, before any PO conversion for that source. Existing mapped boundary regressions cover invalid input, atomic failure and locked approved history; this additional probe assertion exercises the actual PostgreSQL owner pipeline. Source-only until the parent runs the fresh restricted-runtime probe.
