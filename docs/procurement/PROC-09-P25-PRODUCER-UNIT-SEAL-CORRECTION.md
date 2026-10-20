# P25 retained source unit-code compatibility

The protected receiving probe failure `ReceivingSealedEvidenceInvalid` was examined read-only in the disposable synthetic database `proc_approval_http_receiving_codes_20261020_135521`. The first goods-record command reached the receiving consumer after real PR approval, PO conversion/funding/approval/issue/delivery and independent receiving-policy publication. No acceptance was persisted.

Actual immutable producer evidence contains issued line `UnitCode: EACH` and source-request lineage `UnitCode: each`. PO draft input canonicalizes its own unit uppercase; PO commercial/source allocation guards already explicitly accept source unit case equivalence using `StringComparison.OrdinalIgnoreCase`. SourceSnapshot intentionally retains original request lineage bytes.

The receiving consumer incorrectly used ordinal equality for that lineage unit comparison, raising inner `SourceClassificationMismatch`. This correction aligns that single semantic comparison with its owning producer. Exact issued-line-to-pin comparison, original snapshot/lineage hashes, worker identity, source IDs, quantities, policy and classification evidence remain required. No evidence is rewritten, and `EA` / `EACH`, `BOX`, whitespace and unit conversion remain distinct/unsupported.

A regression now produces PR authority/allocation and PO submission/approval/issue/delivery through their existing business workflows, then feeds the actual retained producer snapshots to `ControlledReceiving.VerifySource`. It asserts source lowercase/issued uppercase acceptance, exact sealed-pin mismatch denial, and recomputed-lineage-digest denial for non-equivalent units. No hand-authored commercial or lineage JSON is used for the positive proof.

Source-only: no SDK, database mutation or runtime verification was performed for this patch. Parent owns fresh bounded verification; original failed database/log is retained.
