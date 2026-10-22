# Supplier review — Core principal and known employee aliases

Status: source candidate; mapped tests added but not run. No schema change or production deployment.

Current Core action grants and company scope remain authoritative. Distinct Core principals with no employee record can create, submit, return, revise and independently approve a supplier. Existing creator and latest status-decider principal restrictions remain.

The already retained `Supplier.ProposedByWorkerId` provides an additional known-alias veto: when both that original genuine nonzero worker ID and the current validated optional `scope.WorkerId` exist and match, another account cannot review the same person's proposal. Missing worker metadata is not fabricated; null plus null does not imply the same person. Apply this consistently to return, approval, rejection, qualification review, suspension, reactivation and corresponding detail action flags.

No submitter or hold-decider worker history exists in this record contract. Those actors remain protected by retained Core principal IDs; no worker ID is inferred from current names, emails or account changes, and no new cross-module identity store is added. This patch does not claim to detect unknown aliases.

Mapped test source covers distinct accounts sharing a genuine original proposer employee (403, hidden action flags, unchanged suppliers/qualifications/decisions/audit/outbox) and distinct workerless Core principals (return/revise/resubmit/approval success, self-approval denial, retained creator and decision history). All grants are explicitly persisted at test setup; request authentication helpers do not assign permissions. Root coordinator must build/run these tests and private supplier HTTP/browser proof before release.
