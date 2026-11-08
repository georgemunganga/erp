// Synthetic API for UI qualification only. Never imported by production code.
import { createServer } from 'node:http';
if (process.env.KUUBU_SETUP_UI_FIXTURE !== '1') throw new Error('Explicit UI fixture opt-in required.');
const prefix = '/api/core/product-identity/setup';
const customer = '01arz3ndektsv4rrffq69g5fav';
const uuid = '54f26fe8-23cf-4234-8a48-bd61ce188b44';
let state;
function reset(scenario = 'ready') {
  state = { scenario, subject: '17', authorized: scenario !== 'signed-out', calls: [], receipts: new Map(), cancelCalls: 0 };
}
reset();
function json(res, data, status = 200) {
  res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Qualification-Fixture': 'synthetic-setup-api' });
  res.end(JSON.stringify(data));
}
async function body(req) {
  let text = '';
  for await (const chunk of req) { text += chunk; if (text.length > 8192) throw new Error('Oversized fixture input'); }
  return text ? JSON.parse(text) : {};
}
createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://127.0.0.1:3187');
    if (url.pathname === '/_fixture/reset' && req.method === 'POST') {
      const input = await body(req); reset(input.scenario); return json(res, { ready: true });
    }
    if (url.pathname === '/_fixture/state') {
      if (req.method === 'POST') {
        const input = await body(req);
        if (['17', '18'].includes(input.subject)) state.subject = input.subject;
        if (typeof input.authorized === 'boolean') state.authorized = input.authorized;
        if (['ready', 'expired'].includes(input.scenario)) state.scenario = input.scenario;
      }
      return json(res, { ...state, receipts: state.receipts.size });
    }
    if (url.pathname.startsWith(prefix)) {
      const path = url.pathname.slice(prefix.length);
      if (state.scenario === 'unavailable') return json(res, {}, 503);
      if (path === '/start') { state.authorized = true; state.scenario = 'ready'; res.writeHead(302, { Location: '/setup' }); return res.end(); }
      if (!state.authorized || state.scenario === 'expired') return json(res, {}, 401);
      if (path === '/session') return json(res, { verified: true, subject: state.subject, csrf_token: 'x'.repeat(43), expires_at: new Date(Date.now() + 300000).toISOString(), operational_access_granted: false });
      if (path === '/payers') {
        let customers = [{ customer_id: customer, name: 'Synthetic billing account', workspace_limit: 2 }];
        let next_cursor = null;
        if (state.scenario === 'empty') customers = [];
        if (state.scenario === 'paged') {
          const alphabet = '0123456789abcdefghjkmnpqrstvwxyz';
          const rows = Array.from({ length: 22 }, (_, i) => ({ customer_id: '01arz3ndektsv4rrffq69g5fa' + alphabet[i], name: `Synthetic account ${i + 1}`, workspace_limit: 2 }));
          const after = url.searchParams.get('after');
          customers = rows.filter(row => !after || row.customer_id > after).slice(0, 20);
          if (!after) next_cursor = customers.at(-1).customer_id;
        }
        return json(res, { customers, next_cursor });
      }
      if (path === '/companies' && req.method === 'POST') {
        const data = await body(req);
        if (req.headers['x-kuubu-setup-csrf'] !== 'x'.repeat(43) || data.confirm !== true) return json(res, {}, 403);
        if (req.headers.authorization) throw new Error('Browser bearer not allowed');
        state.calls.push(data);
        if (state.scenario === 'capacity') return json(res, { status: 'CapacityExhausted' }, 409);
        const key = state.subject + ':' + data.idempotency_key;
        const previous = state.receipts.has(key);
        state.receipts.set(key, data);
        if (state.scenario === 'ambiguous' && !previous) return json(res, {}, 503);
        return json(res, { status: previous ? 'Existing' : 'Created', company_id: uuid, binding_id: uuid, operational_access_granted: false }, previous ? 200 : 201);
      }
      if (path === '/cancel' && req.method === 'POST') { state.cancelCalls++; state.authorized = false; res.writeHead(204); return res.end(); }
      return json(res, {}, 404);
    }
    const upstream = await fetch('http://127.0.0.1:3186' + url.pathname + url.search, { redirect: 'manual' });
    res.writeHead(upstream.status, Object.fromEntries(upstream.headers));
    res.end(Buffer.from(await upstream.arrayBuffer()));
  } catch { if (!res.headersSent) json(res, { error: 'fixture_request_failed' }, 500); else res.end(); }
}).listen(3187, '127.0.0.1', () => console.log('Synthetic setup API/SSR fixture listening on 127.0.0.1:3187'));
