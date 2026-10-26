# Core account-link delivery expiry correction

Maintenance reference: CORE-MAIL-EXPIRY-20261026. GitHub Issues is disabled for this repository; the focused pull request and this record provide change traceability.

The release audit found an old failed account access-link event being retried by the existing publisher. SMTP fallback checked event type and recipient but never checked the payload expiry. If mail service recovered, an unusable credential link could be sent.

Core remains owner of authentication and notifications. This correction checks `expires_at` at each SMTP delivery attempt for `hrm.account.access-link`; missing, malformed, non-string or elapsed expiry fails before SMTP configuration or network access. The boundary is exclusive: expiry equal to current UTC time is expired. A controlled clock makes retry timing testable.

The error is fixed and contains neither link nor recipient. `CanDeliver` retains its template/recipient meaning: returning false there would make the current publisher acknowledge the event as `smtp-skipped`. Expired events therefore remain visible failures under the existing retry contract. A terminal expired-message lifecycle is separate work; no status/schema migration or outbox rewrite is included.

Acceptance requires expired/equal/offset-equivalent expiry, missing/null/non-string/malformed expiry and delayed retry to fail before SMTP access. A future link and ordinary leave notification must continue to the existing transport-configuration boundary. These tests use synthetic values and no mail server. Full host compatibility, independent review and a genuine API publication are release gates. Source review passed; execution evidence is pending at this source checkpoint. No production deployment or mail-provider repair is claimed.
