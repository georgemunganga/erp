# Procurement UI demo on the ERP hostname

The Procurement button on `https://erp.amizopower.co.zm/` opens `/procurement` in a separately deployed frontend. This is the browser-local UI demo from `georgemunganga/module-connect`. It does not connect to the live HRM API, Finance, or payments.

## Runtime

- Source and build: `/home/amizo/procurement-ui`
- Service: `amipower-procurement-ui.service`, Node 22 on `127.0.0.1:3301`
- ERP HRM frontend: `amipower-erp-web.service` on `127.0.0.1:3300`
- Public nginx host: `/etc/nginx/conf.d/erp-amipower.conf`
- `/procurement*` and `/supplier*` proxy to Procurement; `/` and `/hrm*` stay on HRM.
- `/assets/*` serves a matching Procurement asset first, then falls back to the HRM frontend. This lets both applications share the hostname without changing their asset URLs.

`procurement-ui.service` and `nginx.snippet.conf` record the deployed configuration. The HRM entry tile and module switcher use a full page navigation, so the separate frontend handles its own routes.

## Updating

1. Pull the intended `module-connect` commit into `/home/amizo/procurement-ui`.
2. From that directory, run `PROCUREMENT_NODE_DEPLOY=1 npm run build` with Node 22. The environment flag selects the Node server preset; the default repository build still targets Lovable.
3. Restart `amipower-procurement-ui.service` and check `/procurement`, `/supplier`, a nested Procurement route, and both applications' assets through the public hostname.

The Procurement UI uses browser storage for demo transactions and role selection. Server authorization and ERP integrations must be implemented before operational use.

The first Vendor, Item and Purchase Request draft APIs are implemented in ERP source at commit `510a130`; see [PROC-BE-03](../../docs/procurement/PROC-BE-03-FIRST-CRUD-OPERATIONS.md). They have not been deployed or wired into this UI. The next frontend milestone must replace demo role/company IDs with the authenticated HRMS session and server-resolved Procurement scope before any screen sends writes. A failed live request must show an error rather than falling back to demo records.
