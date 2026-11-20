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
const relay = createSessionHandler({ upstream: 'http://localhost:8101', productClientId: manifest.client_id, operationalClientId: manifest.operational_client_id,
  proxySecret: 'LocalProductProxySecret12345678901234567890', requireProxySecret: true });
const appOrigin = 'https://app.kuubuerp.com'; const accountOrigin = 'https://www.kuubuerp.com';
const auth = { 'X-Kuubu-Test-Fixture': manifest.fixture_marker };
const moduleCommands = []; let dropRoleResponse = true;
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
      transportFailures.push({ kind: error.constructor.name });console.error(JSON.stringify({event:'dispatch_error',kind:error.constructor.name,actual:typeof error.actual==='number'?error.actual:undefined,expected:typeof error.expected==='number'?error.expected:undefined}));
    });
  });
  await cdp.send('Fetch.enable', { patterns: [{ urlPattern: '*', requestStage: 'Request' }] });
  async function dispatch(route) {
    const request = route.request(); const url = new URL(request.url());
    // Paths only: never record OAuth parameters, bodies, cookies or headers.
    console.log(JSON.stringify({ event: 'browser_request', origin: url.origin, path: url.pathname, elapsed_ms: Math.round(performance.now()-started) }));
    try {
    if (url.origin === appOrigin) {
      if (url.pathname.startsWith('/api/core/product-identity/') || url.pathname === '/erp-protected' || url.pathname === '/fixture/grant-decision') {
        const incoming = await request.allHeaders(); assert.ok(!incoming.authorization);
        assert.ok(!url.pathname.endsWith('/token'), 'Browser must not exchange provider tokens.');
        const response = await fetch('http://127.0.0.1:8121/request', { method: 'POST', headers: { ...auth, 'Content-Type': 'application/json' },
          body: JSON.stringify({ method: request.method(), path: url.pathname + url.search,
            headers: Object.fromEntries(Object.entries(incoming).map(([k, v]) => [k, [v]])), body: request.postDataBuffer()?.toString('base64') ?? null }) });
        console.log(JSON.stringify({event:'core_bridge_status',path:url.pathname,status:response.status}));assert.equal(response.status, 200); const result = await response.json();console.log(JSON.stringify({event:'core_response',path:url.pathname,status:result.status}));
        if (url.pathname.endsWith('/companies') && request.method() === 'POST') commands.push({ body: request.postDataJSON(), status: result.status });
        if (url.pathname.endsWith('/module-access/commands') && request.method() === 'POST') {
          const body = request.postDataJSON(); moduleCommands.push({ body, status: result.status });
          // Model response loss only AFTER the actual Core transaction committed.
          if (dropRoleResponse && body.operation === 'grant' && result.status === 200) {
            dropRoleResponse = false; return route.abort();
          }
        }
        return route.fulfill({ status: result.status, headers: headers(Object.entries(result.headers).map(([k, v]) => [k, v.join(k.toLowerCase() === 'set-cookie' ? '\n' : ', ')])), body: Buffer.from(result.body, 'base64') });
      }
      assert.ok(!url.pathname.startsWith('/api/'), 'No unreviewed ERP API transport.');
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

  const initial = await audit(); assert.equal(initial.companies,0); assert.equal(initial.versions,0); assert.equal(initial.sessions,0); assert.equal(initial.roles,0);
  async function setup(name, first) {
    stage='setup_'+(first?'first':'second');await page.goto(appOrigin+'/setup');await page.getByRole('link',{name:'Continue with Kuubu',exact:true}).click();
    if(first){
      await expect(page.getByRole('button',{name:'Continue with Google',exact:true})).toBeDisabled();
      await page.screenshot({path:join(output,'actual-kuubu-login.png'),fullPage:true});
      await page.getByLabel('Email address',{exact:true}).fill('owner@example.test');await page.getByLabel('Password',{exact:true}).fill('AcceptancePassword123!');
      await page.getByRole('button',{name:'Sign in',exact:false}).click();await page.getByRole('button',{name:'Join Kuubu',exact:true}).click();
    }
    const consent=page.locator('#workspace-setup-consent');await expect(consent).not.toBeChecked();await consent.check();await page.getByRole('button',{name:'Continue to Kuubu ERP',exact:false}).click();
    await page.getByRole('combobox',{name:'Billing account'}).click();await page.getByRole('option',{name:'Synthetic interop payer',exact:true}).click();await page.getByRole('button',{name:'Continue',exact:true}).click();
    await page.getByLabel('Company name',{exact:true}).fill(name);await page.getByRole('button',{name:'Review company',exact:false}).click();await expect(page.getByRole('button',{name:'Create company',exact:true})).toBeDisabled();
    await page.getByRole('checkbox').check();await page.getByRole('button',{name:'Create company',exact:true}).click();await expect(page.getByRole('heading',{name:'Your company is saved.',exact:true})).toBeVisible();
    await page.getByRole('button',{name:'Return to Kuubu',exact:true}).click();await page.waitForURL(accountOrigin+'/');
  }
  await setup('Browser grant company',true);await setup('Browser second company',false);assert.equal((await audit()).companies,2);assert.equal((await audit()).versions,0);
  stage='explicit_operational_sign_in';await page.goto(appOrigin+'/workspace-access');await page.getByRole('button',{name:'Continue with Kuubu',exact:true}).click();
  const consent=page.locator('#operational-session-consent');await expect(consent).not.toBeChecked();await expect(page.getByRole('button',{name:'Continue to Kuubu ERP',exact:false})).toBeDisabled();
  await consent.check();await page.getByRole('button',{name:'Continue to Kuubu ERP',exact:false}).click();
  await page.getByRole('button',{name:'Review Browser grant company',exact:true}).click();
  const panel=page.getByRole('region',{name:'Module permissions'});await panel.getByRole('combobox',{name:'Team member'}).click();await page.getByRole('option',{name:'Workspace owner',exact:true}).click();
  await expect(panel).toContainText('No module permissions have been assigned');assert.equal((await audit()).versions,0);
  async function choose(){await panel.getByRole('checkbox',{name:'HR operations',exact:true}).check();await panel.getByRole('checkbox',{name:'Use HR and payroll',exact:true}).check();await panel.getByRole('checkbox',{name:'Head office',exact:true}).check();}
  async function confirm(){await panel.getByRole('button',{name:'Review permissions',exact:true}).click();await expect(page.getByRole('alertdialog')).toContainText('Head office');await page.getByRole('button',{name:'Confirm permissions',exact:true}).click();}
  stage='actual_grant_response_loss';await choose();await confirm();await expect(panel).toContainText('couldn\u2019t confirm whether the save finished');
  assert.equal((await audit()).versions,1);assert.equal((await audit()).receipts,1);await panel.getByRole('button',{name:'Retry reviewed change',exact:true}).click();await expect(panel).toContainText('Permission decision recorded');
  assert.equal(moduleCommands.length,2);assert.deepEqual(moduleCommands[0].body,moduleCommands[1].body);assert.equal((await audit()).versions,1);assert.equal((await audit()).changes,1);
  const command=moduleCommands[0].body;
  async function decision(){return page.evaluate(async body=>{const response=await fetch('/fixture/grant-decision?'+new URLSearchParams({company_id:body.company_id,link_id:body.link_id}),{cache:'no-store'});return {status:response.status,...(response.ok?await response.json():{})};},command);}
  assert.deepEqual(await decision(),{status:200,authorized:true,branch_count:1,all_branches:false,entry:true});
  assert.equal(await page.evaluate(async()=> (await fetch('/erp-protected',{cache:'no-store'})).status),401);
  stage='actual_revocation';await panel.getByRole('button',{name:'Revoke module permissions',exact:true}).click();await page.getByRole('button',{name:'Confirm revocation',exact:true}).click();await expect(panel).toContainText('Module permissions were revoked');assert.equal((await decision()).status,403);
  async function replay(body){return page.evaluate(async body=>{const proof=await(await fetch('/api/core/product-identity/operational/session',{cache:'no-store'})).json();return(await fetch('/api/core/product-identity/operational/module-access/commands',{method:'POST',headers:{'Content-Type':'application/json','X-Kuubu-Operational-CSRF':proof.csrf_token},body:JSON.stringify(body)})).status;},body);}
  stage='historical_replay_cannot_restore';assert.equal(await replay(command),200);assert.equal((await decision()).status,403);assert.equal((await audit()).versions,2);
  stage='actual_regrant';await choose();await confirm();await expect(panel).toContainText('Permission decision recorded');assert.equal((await decision()).status,200);assert.equal((await audit()).versions,3);
  stage='foreign_company_link_denial';const other=await page.evaluate(async()=> (await(await fetch('/api/core/product-identity/operational/companies',{cache:'no-store'})).json()).companies.find(x=>x.name==='Browser second company'));
  assert.ok(other);assert.equal(await page.evaluate(async({other,command})=>(await fetch('/api/core/product-identity/operational/module-access/review?'+new URLSearchParams({company_id:other.company_id,link_id:command.link_id,membership_id:command.membership_id,module:'hrm'}),{cache:'no-store'})).status,{other,command}),403);
  await page.setViewportSize({width:390,height:844});await page.screenshot({path:join(output,'actual-postgres-permissions-mobile.png'),fullPage:true});
  stage='actual_license_suspension';const suspended=await fetch('http://127.0.0.1:8121/suspend-fixture',{method:'POST',headers:auth});assert.equal(suspended.status,204);assert.equal((await decision()).status,403);
  assert.equal(await replay(moduleCommands[4].body),403); // grant, exact retry, revoke, historical replay, regrant
  assert.deepEqual(await audit(),{companies:2,versions:3,receipts:3,changes:3,sessions:0,roles:0});
  stage='sign_out_clears_editor';await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page.getByRole('button',{name:'Continue with Kuubu',exact:true})).toBeVisible();await expect(panel).toHaveCount(0);
  assert.deepEqual(transportFailures,[]);
  const storage=await page.evaluate(()=>({local:Object.fromEntries(Object.keys(localStorage).map(k=>[k,localStorage.getItem(k)])),session:Object.fromEntries(Object.keys(sessionStorage).map(k=>[k,sessionStorage.getItem(k)]))}));
  assert.deepEqual(storage.local,{});
  const scrollKey='tsr-scroll-restoration-v1_3';assert.deepEqual(Object.keys(storage.session).filter(key=>key!==scrollKey),[]);
  const scroll=JSON.parse(storage.session[scrollKey]||'{}');assert.ok(scroll && typeof scroll==='object' && !Array.isArray(scroll));
  for(const [key,targets] of Object.entries(scroll)){
    assert.match(key,/^[A-Za-z0-9]{1,32}$/);assert.ok(targets && typeof targets==='object' && !Array.isArray(targets));
    for(const [target,position] of Object.entries(targets)){
      assert.equal(target,'window');assert.ok(position && typeof position==='object' && !Array.isArray(position));assert.deepEqual(Object.keys(position).sort(),['scrollX','scrollY']);
      assert.ok(Object.values(position).every(value=>typeof value==='number' && Number.isFinite(value)));
    }
  }
  await writeFile(join(output,'storage-summary.json'),JSON.stringify({local_keys:0,session_keys:Object.keys(storage.session),scroll_history_entries:Object.keys(scroll).length,only_numeric_scroll_positions:true,credential_or_grant_storage:false},null,2),{flag:'wx'});
  await writeFile(join(output,'result.json'),JSON.stringify({passed:true,actual_provider_relay:true,actual_Core_API:true,actual_PostgreSQL:true,companies:2,grant_versions:3,grant_receipts:3,committed_response_lost:1,exact_retry:true,historical_replay_did_not_restore:true,current_license_suspension_denied:true,foreign_company_link_denied:true,ERP_sessions:0,browser_transport:'CDP adapter; no deployed browser TLS/ingress claim'},null,2),{flag:'wx'});
  console.log('Real branded setup, operational consent, PostgreSQL grant/revoke/replay and current-license denial passed.');
}catch(error){
  if(page)await page.screenshot({path:join(output,'failed.png'),fullPage:true}).catch(()=>{});
  await writeFile(join(output,'private-failure.json'),JSON.stringify({stage,kind:error.constructor.name,message:error.message,stack:error.stack},null,2),{flag:'wx'});
  console.error(JSON.stringify({stage,kind:error.constructor.name}));process.exitCode=1;
}finally{if(browser)await browser.close();}
