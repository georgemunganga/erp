# ERP-wide sign-in restoration

The live ERP service was still serving the HR-focused sign-in from the f658f417 source line. The current integration branch already contained ERP-wide sign-in, but that frontend had not been released to the running root service.

This isolated frontend release preserves the six existing local HRM frontend modifications in its baseline commit, then restores the existing ERP-wide split sign-in and shared email-domain component. The original dirty workspace and backend remain untouched. No database, permissions, account, or Procurement deployment changes are included.

The page offers the configured company domain, Gmail, Outlook, Yahoo and a full custom email address. Local sign-in and password reset use existing identity endpoints. Empty branding and Radix empty form-sync values cannot hide the domain picker. Internal return paths reject control characters, off-site destinations and a sign-in reload loop. The module chooser does not require an employee identity or treat legacy Procurement role names as the server's authorization policy; the destination module/API remains authoritative. Core directory pages retain a separate admin gate.

Verification: production frontend build, browser checks against real anonymous capabilities/branding reads with intercepted credential/reset mutations, desktop/mobile layout, email composition, custom addresses, reset composition, safe return/module selection, custom-role module entry and Core denial, and blank company-domain fallback. These browser checks do not log into production or send reset emails.

Deployment switches only `amipower-erp-web.service` to this isolated built release through `/etc/systemd/system/amipower-erp-web.service.d/30-signin-release.conf`. Existing old assets are retained alongside new hashed assets for in-flight tabs. Production API and Procurement static routes are unchanged.

Rollback: remove that specific service override, run `systemctl daemon-reload`, then `systemctl restart amipower-erp-web`. The previous executable and source remain in place. The running service's post-release HTTP/browser results are recorded in the private operational evidence directory and summarized in the acceptance note.
