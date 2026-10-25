# P36/P37 offline physical-capture importer

This tool renders candidate capture classes outside the repository only after a pinned actual successful joint owner roundtrip. It never connects to a database, edits application source, changes options, registers routes or grants permissions. The example plan is intentionally incomplete; no observed definitions have been populated.

The actual physical capture must equal the corresponding first-Up PostgreSQL catalog projections byte for byte. Names must match existing source inventories; P36 owned index names also derive from its pinned genuine migration PK/AK/CreateIndex operations. P37 must contain its exact 19-index inventory. Every FK/key must be validated, immediate except the explicitly deferred P37 current-revision owner FK. CHECK, FK, key and index strings are copied verbatim, without normalization. Missing, extra, mutated or incorrectly deferred objects fail before rendering.

Root must bind and review actual result/capture/catalog hashes after the private schema run succeeds. The output consists of two candidate C# files plus provenance in a fresh protected backup directory. Applying the candidate is a separate reviewed source change. Any readiness tests that currently assert empty captures must be updated to assert exact populated admission and negative mismatches when that later change occurs. Actual capture does not prove finite ACLs, business workflows, capability activation or production readiness.

Pure tests use deliberately artificial `unit_fixture` definitions solely to prove rejection paths. They are not database evidence. Run `python3 -m unittest discover -s tools/procurement/schema-capture -p 'test_*.py'`. No SDK, database or browser execution is involved.
