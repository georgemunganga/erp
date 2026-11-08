# Company setup UI qualification

This suite exercises the real built `/setup` screen against an explicitly
synthetic, loopback-only API. It does not prove Passport/PKCE, Core persistence,
production authorization, or the complete Laravel-to-Core journey. Those have
separate backend/provider qualification gates.

From the module-connect directory, install the locked dependencies and build:

```powershell
npm ci
npm run test:setup-contracts
npm run build
```

Start the built SSR server in one terminal:

```powershell
$env:NITRO_HOST='127.0.0.1'
$env:NITRO_PORT='3186'
node .output/server/index.mjs
```

Start the explicitly opted-in API fixture/proxy in a second terminal:

```powershell
$env:KUUBU_SETUP_UI_FIXTURE='1'
node tests/fixtures/setup-api-server.mjs
```

Run `npm run test:setup-ui` in a third terminal. This configuration uses the
existing Chrome installation, without the user's profile or signed-in sessions.
For retained qualification, supply a fresh exclusive output directory with
`-- --output=<absolute-new-directory>` and retain each run's log separately.
Never rebuild a running SSR server's output; stop both local processes first.

The fixture exposes only synthetic subjects/accounts/receipts and accepts no
merchant keys or real credentials. Its API response header identifies it as
`synthetic-setup-api`. Tests intercept return navigation to the public website;
no account, billing or operational action is sent there. Desktop/mobile screenshots
are saved in the successful test output. Keep failed attempts and screenshots.

Coverage includes explicit review/confirmation, no operational auth/branding
fetches, safe retries after a lost response and reload, fresh consent recovery,
identity mismatch, storage failure, account pagination/empty state, capacity
denial, expiry, cancellation, successful acknowledgement and mobile control sizes.
