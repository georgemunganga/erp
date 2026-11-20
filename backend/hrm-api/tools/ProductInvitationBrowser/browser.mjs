// Explicit browser qualification only. Never imported by a deployed application.
import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, join, sep, isAbsolute } from 'node:path';
import { pathToFileURL } from 'node:url';

const args = Object.fromEntries(process.argv.slice(2).map((v, i, all) => i % 2 === 0 ? [v, all[i + 1]] : null).filter(Boolean));
for (const key of ['--manifest', '--website', '--output', '--playwright']) assert.ok(isAbsolute(args[key]));
const manifest = JSON.parse(await readFile(args['--manifest'], 'utf8'));
assert.equal(manifest.fixture_scope, 'greenwebb-loopback-interop-v1');
assert.equal(manifest.setup_owner_enabled, true);
const website = resolve(args['--website']); const output = resolve(args['--output']);
await mkdir(output);
const { chromium, expect: defaultExpect } = await import(pathToFileURL(args['--playwright']));
const expect = defaultExpect.configure({timeout:30000});
const { createSessionHandler } = await import(pathToFileURL(join(website, 'server/session.js')));
const relay = createSessionHandler({ upstream: 'http://localhost:8101', productClientId: manifest.client_id,
  proxySecret: 'LocalProductProxySecret12345678901234567890', requireProxySecret: true });
const appOrigin = 'https://app.kuubuerp.com'; const accountOrigin = 'https://www.kuubuerp.com';
const auth = { 'X-Kuubu-Test-Fixture': manifest.fixture_marker };
const teamCommands = []; let dropRoleResponse = false; const invitationCommands = []; let dropInvitationResponse = true;
const commands = []; const transportFailures = []; const started = performance.now(); let stage = 'launch'; let browser; let page;
function headers(entries) {
  return Object.fromEntries(entries.filter(([name]) => !['content-length', 'content-encoding', 'transfer-encoding', 'connection'].includes(name.toLowerCase())));
}
async function audit() {
  const response = await fetch('http://127.0.0.1:8121/audit', { headers: auth });
  assert.equal(response.status, 200); return response.json();
}
try {
  // A missed interception must fail closed, never resolve the deployed sites.
  browser = await chromium.launch({ channel: 'chrome', headless: true,
    args: ['--host-resolver-rules=MAP * ~NOTFOUND'] });
  const context = await browser.newContext({ viewport: { width: 1360, height: 900 }, serviceWorkers: 'block' });
  page = await context.newPage();
  page.on('pageerror', error => {transportFailures.push({kind:'page_error'});console.error(JSON.stringify({event:'page_error',kind:error.name}));});
  const cdp = await context.newCDPSession(page);
  // Playwright routing skips subsequent redirect URLs. Use Fetch interception
  // directly so every navigation hop stays inside this qualification transport.
  // The browser still processes original statuses, redirects and Set-Cookie.
  cdp.on('Fetch.requestPaused', event => {
    const incoming = event.request;
    const body = incoming.postDataEntries?.length
      ? Buffer.concat(incoming.postDataEntries.map(part => Buffer.from(part.bytes || '', 'base64')))
      : incoming.postData === undefined ? null : Buffer.from(incoming.postData);
    const request = {
      url: () => incoming.url, method: () => incoming.method,
      allHeaders: async () => Object.fromEntries(Object.entries(incoming.headers).map(([k, v]) => [k.toLowerCase(), v])),
      postDataBuffer: () => body, postDataJSON: () => JSON.parse(body.toString()),
    };
    const route = {
      request: () => request,
      abort: () => cdp.send('Fetch.failRequest', { requestId: event.requestId, errorReason: 'Aborted' }),
      fulfill: ({ status, headers: outgoing = {}, contentType, body: responseBody }) => cdp.send('Fetch.fulfillRequest', {
        requestId: event.requestId, responseCode: status,
        responseHeaders: Object.entries({ ...outgoing, ...(contentType ? { 'content-type': contentType } : {}) })
          .flatMap(([name, value]) => name.toLowerCase() === 'set-cookie' ? value.split('\n').map(value => ({ name, value })) : [{ name, value }]),
        body: Buffer.from(responseBody || '').toString('base64'),
      }),
    };
    dispatch(route).catch(error => {
      transportFailures.push({ kind: error.constructor.name });
    });
  });
  await cdp.send('Fetch.enable', { patterns: [{ urlPattern: '*', requestStage: 'Request' }] });
  async function dispatch(route) {
    const request = route.request(); const url = new URL(request.url());
    // Paths only: never record OAuth parameters, bodies, cookies or headers.
    console.log(JSON.stringify({ event: 'browser_request', origin: url.origin, path: url.pathname, elapsed_ms: Math.round(performance.now()-started) }));
    try {
    if (url.origin === appOrigin) {
      if (url.pathname.startsWith('/api/core/product-identity/') || url.pathname === '/erp-protected') {
        const incoming = await request.allHeaders(); assert.ok(!incoming.authorization);
        assert.ok(!url.pathname.endsWith('/token'), 'Browser must not exchange provider tokens.');
        const response = await fetch('http://127.0.0.1:8121/request', { method: 'POST', headers: { ...auth, 'Content-Type': 'application/json' },
          body: JSON.stringify({ method: request.method(), path: url.pathname + url.search,
            headers: Object.fromEntries(Object.entries(incoming).map(([k, v]) => [k, [v]])), body: request.postDataBuffer()?.toString('base64') ?? null }) });
        assert.equal(response.status, 200); const result = await response.json();
        if (url.pathname.endsWith('/companies') && request.method() === 'POST') commands.push({ body: request.postDataJSON(), status: result.status });
        if (url.pathname.endsWith('/team/commands') && request.method() === 'POST') {
          const body = request.postDataJSON(); teamCommands.push({ body, status: result.status });
          // Model response loss only AFTER the actual Core transaction committed.
          if (dropRoleResponse && body.operation === 'role-change' && result.status === 200) {
            dropRoleResponse = false; return route.abort();
          }
        }
        if (url.pathname.endsWith('/invitation/accept') && request.method() === 'POST') {
          invitationCommands.push({ body: request.postDataJSON(), status: result.status });
          if (dropInvitationResponse && result.status === 200) { dropInvitationResponse = false; return route.abort(); }
        }
        return route.fulfill({ status: result.status, headers: headers(Object.entries(result.headers).map(([k, v]) => [k, v.join(k.toLowerCase() === 'set-cookie' ? '\n' : ', ')])), body: Buffer.from(result.body, 'base64') });
      }
      assert.equal(request.method(), 'GET');
      const response = await fetch('http://127.0.0.1:3186' + url.pathname + url.search, { redirect: 'manual' });
      return route.fulfill({ status: response.status, headers: headers([...response.headers]), body: Buffer.from(await response.arrayBuffer()) });
    }
    if (url.origin === accountOrigin) {
      if (url.pathname.replace(/\/$/, '') === '/api/session') {
        const incoming = new Headers(await request.allHeaders()); incoming.set('X-Forwarded-For', '203.0.113.124');
        const response = await relay(new Request(url, { method: request.method(), headers: incoming,
          ...(request.method() === 'POST' ? { body: request.postDataBuffer() } : {}) }));
        console.log(JSON.stringify({event:'relay_response',action:url.searchParams.get('action'),status:response.status,elapsed_ms:Math.round(performance.now()-started)}));
      const outgoing = headers([...response.headers].filter(([k]) => k !== 'set-cookie'));
        const cookies = response.headers.getSetCookie(); if (cookies.length) outgoing['set-cookie'] = cookies.join('\n');
        return route.fulfill({ status: response.status, headers: outgoing, body: Buffer.from(await response.arrayBuffer()) });
      }
      assert.equal(request.method(), 'GET');
      const path = resolve(website, 'dist', '.' + decodeURIComponent(url.pathname), url.pathname.endsWith('/') ? 'index.html' : '');
      assert.ok(path.startsWith(join(website, 'dist') + sep));
      const type = path.endsWith('.html') ? 'text/html' : path.endsWith('.js') ? 'text/javascript' : path.endsWith('.css') ? 'text/css' : path.endsWith('.svg') ? 'image/svg+xml' : 'application/octet-stream';
      let content;
      try { content = await readFile(path); }
      catch (error) {
        if (error.code !== 'ENOENT') throw error;
        // Normal static-server behavior (including optional favicon requests).
        return route.fulfill({ status: 404, contentType: 'text/plain', body: 'Not found' });
      }
      return route.fulfill({ status: 200, contentType: type, body: content });
    }
    // No user profile or off-fixture network is used, including external fonts.
    return route.abort('blockedbyclient');
    } catch (error) {
      transportFailures.push({ origin: url.origin, path: url.pathname, kind: error.constructor.name });
      console.error(JSON.stringify({ event: 'browser_transport_failed', origin: url.origin, path: url.pathname, kind: error.constructor.name }));
      return route.abort('failed');
    }
  }
  assert.deepEqual(await audit(), { companies: 0, sessions: 0, roles: 0 });
  stage = 'start_setup';
  await page.goto(appOrigin + '/setup');
  await page.getByRole('link', { name: 'Continue with Kuubu', exact: true }).click();
  stage = 'own_branded_login';
  await expect(page.getByRole('button', { name: 'Continue with Google', exact: true })).toBeDisabled({timeout:30000});
  await page.screenshot({ path: join(output, 'real-provider-login.png'), fullPage: true });
  await page.getByLabel('Email address', { exact: true }).fill('owner@example.test');
  await page.getByLabel('Password', { exact: true }).fill('AcceptancePassword123!');
  await page.getByRole('button', { name: 'Sign in', exact: false }).click();
  stage = 'explicit_join';
  await page.getByRole('button', { name: 'Join Kuubu', exact: true }).click();
  stage = 'explicit_setup_consent';
  const consent = page.locator('#workspace-setup-consent');
  await expect(consent).not.toBeChecked();
  await expect(page.getByRole('button', { name: 'Continue to Kuubu ERP', exact: false })).toBeDisabled();
  await page.screenshot({ path: join(output, 'real-provider-consent.png'), fullPage: true });
  await consent.check();
  await page.getByRole('button', { name: 'Continue to Kuubu ERP', exact: false }).click();
  stage = 'protected_setup_callback';
  await expect(page.getByRole('combobox', { name: 'Billing account' })).toBeVisible({ timeout: 30000 });
  assert.deepEqual(await audit(), { companies: 0, sessions: 0, roles: 0 });
  stage = 'company_review';
  await page.getByRole('combobox', { name: 'Billing account' }).click();
  await page.getByRole('option', { name: 'Synthetic interop payer', exact: true }).click();
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  await page.getByLabel('Company name', { exact: true }).fill('Browser acceptance company');
  await page.getByRole('button', { name: 'Review company', exact: false }).click();
  await expect(page.getByRole('button', { name: 'Create company', exact: true })).toBeDisabled();
  assert.equal((await audit()).companies, 0);
  await page.screenshot({ path: join(output, 'real-provider-review.png'), fullPage: true });
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Create company', exact: true }).click();
  stage = 'company_saved';
  await expect(page.getByRole('heading', { name: 'Your company is saved.', exact: true })).toBeVisible({ timeout: 30000 });
  assert.deepEqual(await audit(), { companies: 1, sessions: 0, roles: 0 });
  const cookies = await context.cookies();
  for (const [name, domain] of [['__Host-kuubu_session', 'www.kuubuerp.com'], ['__Host-kuubu_product_identity', 'app.kuubuerp.com']]) {
    const cookie = cookies.find(row => row.name === name); assert.ok(cookie?.httpOnly && cookie.secure); assert.equal(cookie.domain, domain);
  }
  stage = 'reload_and_retry';
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Check your previous request.', exact: true })).toBeVisible();
  await expect(page.getByRole('checkbox')).not.toBeChecked();
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Retry reviewed request', exact: true }).click();
  await expect(page.getByText('Your earlier request was confirmed. No additional company was created.', { exact: true })).toBeVisible({ timeout: 30000 });
  assert.equal(commands.length, 2); assert.equal(commands[0].status, 201); assert.equal(commands[1].status, 200);
  assert.deepEqual(commands[0].body, commands[1].body);
  assert.deepEqual(await audit(), { companies: 1, sessions: 0, roles: 0 });
  assert.equal(await page.evaluate(async () => (await fetch('/erp-protected', { cache: 'no-store' })).status), 401);
  await page.screenshot({ path: join(output, 'real-provider-saved.png'), fullPage: true });
  stage = 'fresh_team_authorization_without_preaccepted_staff';
  await page.getByRole('link', { name: 'Manage company team', exact: true }).click();
  await page.getByRole('link', { name: 'Continue with Kuubu', exact: true }).click();
  await page.getByRole('button', { name: 'Continue to Kuubu ERP', exact: false }).click();
  await page.getByRole('button', { name: 'Invite teammate', exact: true }).click();
  const sender = page.getByRole('dialog');
  await sender.getByLabel('Recipient email').fill('staff@example.test');
  await sender.getByRole('checkbox').check();
  await sender.getByRole('button', { name: 'Create invitation', exact: true }).click();
  await expect(page.getByRole('list', { name: 'Company invitations', exact: true })).toContainText('Awaiting acceptance');
  async function invitationAudit() {
    const response = await fetch('http://127.0.0.1:8121/invitation-audit', { headers: auth });
    assert.equal(response.status, 200); return response.json();
  }
  assert.deepEqual(await invitationAudit(), { members: 1, receipts: 1, accepted: 0, sessions: 0, roles: 0 });
  stage = 'inspect_test_delivery_intent';
  const delivery = await fetch('http://127.0.0.1:8121/delivery-preview', { headers: auth });
  assert.equal(delivery.status, 200);
  const link = new URL((await delivery.json()).review_url);
  assert.equal(link.origin, appOrigin); assert.equal(link.pathname, '/accept-invitation');
  assert.equal(link.search, ''); assert.match(link.hash, /^#token=[A-Za-z0-9_-]{43}$/);
  const secret = link.hash.slice(7);
  stage = 'recipient_landing_and_explicit_handoff';
  await page.goto(link.href);
  await expect(page.getByRole('button', { name: 'Continue with Kuubu', exact: true })).toBeVisible();
  await expect(page).toHaveURL(appOrigin + '/accept-invitation');
  await page.getByRole('button', { name: 'Continue with Kuubu', exact: true }).click();
  await expect(page.locator('#invitation-recipient-consent')).not.toBeChecked();
  await page.getByRole('button', { name: 'Use another account', exact: true }).click();
  stage = 'recipient_own_login';
  await expect(page.getByRole('button', { name: 'Continue with Google', exact: true })).toBeDisabled();
  await page.getByLabel('Email address', { exact: true }).fill('staff@example.test');
  await page.getByLabel('Password', { exact: true }).fill('AcceptancePassword123!');
  await page.getByRole('button', { name: 'Sign in', exact: false }).click();
  await page.getByRole('button', { name: 'Join Kuubu', exact: true }).click();
  stage = 'separate_recipient_verification_consent';
  const consentRecipient = page.locator('#invitation-recipient-consent');
  const continueRecipient = page.getByRole('button', { name: 'Continue to Kuubu ERP', exact: false });
  await expect(consentRecipient).not.toBeChecked(); await expect(continueRecipient).toBeDisabled();
  assert.deepEqual(await invitationAudit(), { members: 1, receipts: 1, accepted: 0, sessions: 0, roles: 0 });
  await consentRecipient.check(); await expect(continueRecipient).toBeEnabled();
  await consentRecipient.uncheck(); await expect(continueRecipient).toBeDisabled();
  await consentRecipient.check();
  await page.screenshot({ path: join(output, 'real-recipient-consent.png'), fullPage: true });
  await continueRecipient.click();
  stage = 'actual_recipient_proof_and_company_review';
  await expect(page.getByRole('heading', { name: 'Browser acceptance company', exact: true })).toBeVisible();
  await expect(page).toHaveURL(appOrigin + '/accept-invitation');
  const accept = page.getByRole('button', { name: 'Accept company invitation', exact: true });
  await expect(page.getByRole('checkbox')).not.toBeChecked(); await expect(accept).toBeDisabled();
  assert.deepEqual(await invitationAudit(), { members: 1, receipts: 1, accepted: 0, sessions: 0, roles: 0 });
  const beforeText = await page.locator('html').innerText(); assert.ok(!beforeText.includes(secret));
  await page.getByRole('checkbox').check();
  await page.screenshot({ path: join(output, 'real-recipient-company-review.png'), fullPage: true });
  await accept.click();
  stage = 'committed_acceptance_response_loss_and_exact_retry';
  await expect(page.getByRole('alert')).toContainText('couldn’t confirm');
  assert.deepEqual(await invitationAudit(), { members: 2, receipts: 2, accepted: 1, sessions: 0, roles: 0 });
  await page.getByRole('button', { name: 'Retry the same acceptance request', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your invitation was accepted.', exact: true })).toBeVisible();
  assert.equal(invitationCommands.length, 2); assert.deepEqual(invitationCommands[0].body, invitationCommands[1].body);
  assert.deepEqual(invitationCommands.map(x => x.status), [200, 200]);
  const finalCookies = await context.cookies();
  for (const [name, domain] of [['__Host-kuubu_session', 'www.kuubuerp.com'], ['__Host-kuubu_product_identity', 'app.kuubuerp.com'], ['__Host-kuubu_invitation_intent', 'app.kuubuerp.com']]) {
    const cookie = finalCookies.find(row => row.name === name); assert.ok(cookie?.secure && cookie.httpOnly); assert.equal(cookie.domain, domain);
  }
  assert.equal(await page.evaluate(async () => (await fetch('/erp-protected', { cache: 'no-store' })).status), 401);
  stage = 'actual_subscription_suspension_and_acceptance_replay_denial';
  const suspended = await fetch('http://127.0.0.1:8121/suspend-fixture', { method: 'POST', headers: auth }); assert.equal(suspended.status, 200);
  const denied = await page.evaluate(async body => {
    const proof = await (await fetch('/api/core/product-identity/invitation/session', { cache: 'no-store' })).json();
    return (await fetch('/api/core/product-identity/invitation/accept', { method: 'POST', headers: { 'Content-Type': 'application/json',
      'X-Kuubu-Invitation-CSRF': proof.csrf_token }, body: JSON.stringify(body) })).status;
  }, invitationCommands[0].body);
  assert.equal(denied, 403); assert.deepEqual(invitationCommands.map(x => x.status), [200, 200, 403]);
  assert.deepEqual(await invitationAudit(), { members: 2, receipts: 2, accepted: 1, sessions: 0, roles: 0 });
  assert.deepEqual(transportFailures, []);
  const storage = await page.evaluate(() => ({ local: Object.entries(localStorage), session: Object.entries(sessionStorage) }));
  assert.deepEqual(storage.local, []); assert.ok(!JSON.stringify(storage).includes(secret));
  await page.screenshot({ path: join(output, 'real-recipient-accepted.png'), fullPage: true });
  await writeFile(join(output, 'result.json'), JSON.stringify({ passed: true, actual_recipient_acceptance_tested: true,
    preaccepted_member_fixture: false, members: 2, invitation_receipts: 2, accepted: 1,
    dropped_committed_response: 1, license_replay_denied: true, operational_sessions: 0, module_roles: 0,
    actual_mail_delivery_tested: false, transport: 'CDP scoped origins to actual loopback services; no deployed TLS claim' }, null, 2), { flag: 'wx' });
  console.log('Actual provider login, explicit enrollment/recipient consent, company acceptance/exact retry and license denial passed.');
} catch (error) {
  if (page) await page.screenshot({ path: join(output, 'failed.png'), fullPage: true }).catch(() => {});
  console.error(JSON.stringify({ stage, kind: error.constructor.name })); process.exitCode = 1;
} finally { if (browser) await browser.close(); }
