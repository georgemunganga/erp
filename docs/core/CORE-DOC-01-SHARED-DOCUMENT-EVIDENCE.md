
### Owner lifecycle lease prerequisite — source only

[The shared lease extraction ADR](../adr/ADR-CORE-DOCUMENT-OWNER-LIFECYCLE-FENCE.md)
records the new Documents-owned pre-transaction fence, exact stable owner key,
bounded deterministic multi-owner acquisition/reverse cleanup and managed EF
transaction prerequisites. The proven branch/catalog lease implementation is moved
unchanged into shared Core persistence infrastructure; their key bytes and existing
semantics remain unchanged. No use-store/retirement/provider route is wired, no schema
or policy is added and no production protection is claimed until both owning reader
and lifecycle writer participate. Authored unit/opt-in private PostgreSQL probe
source is unrun; root owns serialized gates and activation remains unavailable.
