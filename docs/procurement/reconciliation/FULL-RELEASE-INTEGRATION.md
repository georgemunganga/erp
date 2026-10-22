# Combined Procurement release candidate

Source integration only; this is not a release or a statement that the full module is ready.

## Inputs

- Governed canonical schema/manual-invoice interoperability: `cf5d506` (migration remains `20261022052824_P21CanonicalGovernedProcurementFoundation`).
- Unused buying-policy draft retirement and wrapped PostgreSQL serialization conflict: `19d6b1e` / `2e6a0df`.
- Exact catalog read companion: `dc7ed10`.
- Exact request read/input and private HTTP probe: `0176d1c`.

Their individual prior evidence is retained in the execution board. It does not prove this combined candidate.

## Conflict resolution and domain ownership

Existing governed policy wrappers, current Core actor/author checks, publication concurrency helper, request catalog fencing and submitted workflow remain intact. Retirement now considers the additional approval/order submissions and receiving source/physical/inspection/service/acceptance references. Any operational/sourcing identity stage beyond Created prevents retirement. Immutable Created identity is retained authorship, with the existing author checks applied before retiring those specialized drafts. No identity evidence is deleted or rewritten.

Request input keeps the existing domain CommercialLineDecimalJsonConverter and the more specific7,4 RequestDiscountJsonConverter. The duplicate thin transport converter from the draft-only patch is removed. Exact read companions are additive; old numeric output stays compatible. No new table, key, owner ledger, authority grant, migration history adoption or feature activation is introduced by this source combination.

## Pending acceptance

Independent integration review; new historical-reference/author guards; combined mapped HTTP/domain cases; real private PostgreSQL draft and operational probes; browser acceptance on the final UI source; restricted backup clone compatibility; host startup/release/rollback smoke. Tier3 release gates remain in force. Source pushes and prior branch test results are not production deployment.
