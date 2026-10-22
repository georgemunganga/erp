# Core document scanner probe

Runs synthetic clean and EICAR antivirus fixtures through the actual `ClamdDocumentScanner` adapter. Exit 0 requires Clean for the first fixture and Infected for EICAR, using non-stale loaded definition evidence. It does not upload ERP documents, authorize an invoice, or enable production downloads.

```sh
dotnet build backend/core-api/tools/CoreDocumentScannerProbe
dotnet backend/core-api/tools/CoreDocumentScannerProbe/bin/Debug/net10.0/CoreDocumentScannerProbe.dll /private/scanner/clamd.sock
```

The scanner's local timezone must match the configured timezone used to interpret ClamD's definition timestamp. This probe uses UTC; run the daemon with `TZ=UTC`. An unavailable socket, stale/missing definition data, content mismatch, timeout or ambiguous reply fails the check.

## Isolated engine rehearsal

The `rehearsal/` configurations are for a disposable container test, not the production runtime. They run as root inside that isolated test container; production must use the reviewed service UID and private socket permissions. Configure Core's document runtime independently from HRM employee files.

Verified engine artifact: `clamav/clamav:1.5.4_base`, pinned as `clamav/clamav@sha256:751df8dc649cc977696f107c80db4960b6317a7cb511aebc8066f3b91db6b631`. Re-evaluate supported versions/security updates before production release.

1. Create a private disposable directory with `db/` and `socket/`, each mode 0700. Copy the rehearsal configurations there.
2. Run the pinned image's `freshclam` entrypoint with the directory mounted at `/work`, `TZ=UTC`, and `--config-file=/work/freshclam.conf --stdout`. Its downloads and database validation require network access. Retain the definition versions and exit result. The initial 384 MB test limit was insufficient; the update passed within a 1 GB bound.
3. Start the pinned image's `clamd` entrypoint with `/work` read-only, the `socket/` directory mounted at `/socket`, `TZ=UTC`, `--network none`, `--read-only`, a writable private `/tmp` tmpfs, a 1400 MB memory bound and `--config-file=/work/clamd.conf`. No TCP port is published. Limit CPU priority relative to ERP traffic.
4. Wait for its socket and run the built probe with that socket mounted into the runner. The probe requires no network and uses no business files. A timeout waiting for startup is not proof that the daemon stopped: inspect the same container before taking another action.
5. Retain probe output and engine resource evidence, stop/remove the test daemon, and remove transient sockets. Keep definition files in the protected rehearsal cache if further tests need them; never commit them.

The configuration flags encrypted content and exceeded scanner limits so a skipped inspection does not become a Clean outcome. Type signatures in private storage do not replace malware scanning or validate a complete PDF/image structure.

Sources: [official ClamD protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html), [official container guidance](https://docs.clamav.net/manual/Installing/Docker.html), and [1.5.4 configuration reference](https://github.com/Cisco-Talos/clamav/blob/clamav-1.5.4/etc/clamd.conf.sample).
