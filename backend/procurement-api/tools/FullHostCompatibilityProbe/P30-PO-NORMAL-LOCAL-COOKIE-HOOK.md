# P30 PO normal local-cookie browser hook

This opt-in follows the real P30 PO HTTP and owning-store proof on a **new**
`proc_p30_http_po_*` PostgreSQL 17 database under the private Unix socket. Set
`P30_HTTP_VERIFY_PRINCIPAL_PO=1` and `P30_HTTP_SERVE_NORMAL_COOKIE=1` alongside
the existing private probe owner/result inputs. Other optional cohorts remain
off. The ordinary Program host, finite execution roles and Core local login
are used; browser requests never use the probe's subject header.

After the API proof, the hook confirms one approved source quantity remains,
the supplier is Active, and the earlier order is Issued. It activates only
the ten newly created synthetic workerless accounts with unused placeholder
passwords, then listens on an ephemeral loopback port. It writes a new 0600
`/p25-evidence/p30-po-local-cookie-fixture.private.json` and waits at most
15 minutes for a 0600 challenged completion at
`/p25-evidence/p30-po-local-cookie-browser-completion.private.json`.
The fixture contains the private URL, approved request/line, active supplier,
buyer account and protected synthetic credentials. Keep it out of logs and git.

The matching UI files are `reference-ui/scripts/p30-po-cookie-browser-proxy.mjs`,
`reference-ui/playwright.p30-po-cookie.config.ts`, and
`reference-ui/tests/normal-cookie/p30-po-cookie.spec.ts`. Run the proxy and
browser only inside the API network namespace against the protected fixture,
with a new TLS key/certificate and prebuilt UI bundle. The proxy must write
a separate 0600 receipt bound to the fixture SHA-256. The browser result binds
the create/cancel command keys and observed versions; an independent owning
store read must confirm the final order, allocation and receipts. A lifecycle
completion file, proxy receipt or passing browser process alone is not that
owner proof and never asserts supplier sending, Finance posting or payment.
