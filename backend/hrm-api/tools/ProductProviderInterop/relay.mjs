// Qualification only: actual Kuubu account client/relay, bound to loopback.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, join, relative, isAbsolute } from 'node:path';
import { pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import assert from 'node:assert/strict';

const args = Object.fromEntries(process.argv.slice(2).map((value, index, values) =>
  index % 2 === 0 ? [value, values[index + 1]] : null).filter(Boolean));
const workspace = resolve(args['--workspace']);
const website = resolve(args['--website']);
const backend = resolve(args['--backend']);
const manifest = JSON.parse(await readFile(join(workspace, 'core-interop.json'), 'utf8'));
const env = JSON.parse(await readFile(join(workspace, 'private-environment.json'), 'utf8'));
assert.equal(manifest.fixture_scope, 'greenwebb-loopback-interop-v1');
assert.equal(env.APP_ENV, 'local'); assert.equal(env.DB_CONNECTION, 'sqlite');
assert.equal(env.MAIL_MAILER, 'array'); assert.equal(env.PAYMENTS_ENABLED, 'false');
assert.equal(resolve(env.DB_DATABASE), join(workspace, 'fixture.sqlite'));
const child = relative(workspace, resolve(env.LARAVEL_STORAGE_PATH));
assert.ok(child && !child.startsWith('..') && !isAbsolute(child));
const { createSessionHandler } = await import(pathToFileURL(join(website, 'server/session.js')));
const { createAccountClient } = await import(pathToFileURL(join(website, 'src/auth/client.js')));
const origin = 'https://www.kuubuerp.com';
const providerFetch = async (url, options) => {
  const started = performance.now();
  try {
    const response = await fetch(url, options);
    console.log(JSON.stringify({ event: 'provider_http', method: options.method,
      path: new URL(url).pathname, status: response.status, elapsed_ms: Math.round(performance.now()-started) }));
    return response;
  } catch (error) {
    console.error(JSON.stringify({ event: 'provider_transport_failed', kind: error.name,
      elapsed_ms: Math.round(performance.now()-started) }));
    throw error;
  }
};
const relay = createSessionHandler({ upstream: 'http://localhost:8101', productClientId: manifest.client_id,
  proxySecret: 'LocalProductProxySecret12345678901234567890', requireProxySecret: true, fetcher: providerFetch });
let cookie = '';
const account = createAccountClient(async (path, options) => {
  const response = await relay(new Request(new URL(path, origin), { ...options,
    headers: { ...options.headers, ...(options.method === 'POST' ? { Origin: origin } : {}),
      'X-Forwarded-For': '203.0.113.125', Cookie: cookie } }));
  const issued = response.headers.getSetCookie().find(value => value.startsWith('__Host-kuubu_session='));
  if (issued) cookie = issued.split(';')[0];
  return response;
});
let authorized = false;
function reply(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(body));
}
const server = createServer(async (request, response) => {
  let stage = 'fixture_dispatch';
  try {
    if (request.headers['x-kuubu-test-fixture'] !== manifest.fixture_marker) return reply(response, 403, { error: 'fixture_required' });
    if (request.method === 'GET' && request.url === '/health') return reply(response, 200, { fixture_marker: manifest.fixture_marker });
    if (request.method === 'GET' && request.url === '/audit') {
      const result = spawnSync(args['--python'] || 'python', ['-c',
        "import json,pathlib,sqlite3,sys; db=sqlite3.connect(pathlib.Path(sys.argv[1]).as_uri()+'?mode=ro',uri=True); names=['users','customers','customer_memberships','identity_product_enrollments','product_enrollments','subscriptions']; data={name:db.execute('SELECT count(*) FROM '+name).fetchone()[0] for name in names}; data['subscription_status']=db.execute('SELECT status FROM subscriptions').fetchone()[0]; print(json.dumps(data))", env.DB_DATABASE], { encoding: 'utf8' });
      assert.equal(result.status, 0);
      return reply(response, 200, JSON.parse(result.stdout));
    }
    if (request.method === 'POST' && request.url === '/suspend') {
      assert.equal(authorized, true);
      const result = spawnSync(args['--php'], [join(workspace, 'core-interop-suspend.php')],
        { cwd: backend, env: { ...process.env, ...env, FIXTURE_WORKSPACE: workspace }, encoding: 'utf8' });
      assert.equal(result.status, 0);
      return reply(response, 200, { suspended: true });
    }
    if (request.method === 'POST' && request.url === '/authorize') {
      assert.equal(authorized, false, 'Use a fresh retained fixture for each run.');
      let size = 0; const chunks = [];
      for await (const chunk of request) { size += chunk.length; assert.ok(size <= 16384); chunks.push(chunk); }
      const intent = JSON.parse(Buffer.concat(chunks).toString());
      stage = 'login';
      const person = await account.login('staff@example.test', 'AcceptancePassword123!');
      stage = 'identity_choices';
      assert.equal(String(person.id), manifest.staff_subject); assert.equal(person.email_verified, true);
      assert.deepEqual(await account.identityProducts(), []);
      stage = 'pre_enrollment_denial';
      await assert.rejects(account.authorizeKuubu(intent), error => error.status === 403);
      stage = 'explicit_enrollment';
      await account.enrollKuubu(true);
      stage = 'authorization';
      const destination = await account.authorizeKuubu(intent);
      authorized = true;
      return reply(response, 200, { redirect_uri: destination, pre_enrollment_denied: true });
    }
    reply(response, 404, { error: 'fixture_action_unavailable' });
  } catch (error) {
    // Never echo tokens, protected state, credentials or provider request bodies.
    console.error(JSON.stringify({ event: 'fixture_action_failed', stage, kind: error.constructor.name,
      status: Number.isInteger(error.status) ? error.status : null }));
    reply(response, 500, { error: 'fixture_action_failed' });
  }
});
server.listen(8120, '127.0.0.1', () => console.log('Synthetic Kuubu relay bridge listening on loopback8120.'));
