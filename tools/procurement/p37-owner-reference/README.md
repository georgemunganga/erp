# P37 private owner verifier reference

`verify.py` is byte-identical to the verifier executed in the accepted private Owner06 read-only capture (SHA-256 `b04a1baa8b379d191ce177e512bfcbd69d50ef78bfe7474f37a0fe9c237134ff`). The [acceptance record](../../../docs/procurement/release/P37-NATIVE05-OWNER06-ACCEPTANCE-20261025.json) binds its reviewed plan, Native05 result and two matching capture hashes.

This file is a source reference, not a runnable production or generic database tool. It expects an exact private plan, specific source/result files and a disposable PostgreSQL container. Its private pure tests include persisted producer golden values and remain in the protected evidence directory; those identifiers and raw captures are excluded from Git.

Owner05's failed verifier and both raw captures remain unchanged. Owner06 corrected numeric canonicalization of the proposal SHA only. The verifier does not reconstruct original HTTP request bytes or independently baseline unrelated business rows.
