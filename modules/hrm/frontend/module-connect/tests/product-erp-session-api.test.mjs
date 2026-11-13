import assert from "node:assert/strict";
import test from "node:test";
import {
  createErpSessionClient,
  createErpSelectionAttempt,
} from "../src/modules/core/product-erp-session-api.ts";
const prefix = "/api/core/product-identity/operational/erp-session";
const company = {
  id: "11111111-1111-1111-1111-111111111111",
  linkId: "22222222-2222-2222-2222-222222222222",
  name: "Company A",
  role: "member",
};
const version = "33333333-3333-3333-3333-333333333333";
const location = "44444444-4444-4444-4444-444444444444";
const csrf = "c".repeat(43);
const review = (extra = {}) => ({
  company_id: company.id,
  module: "hrm",
  grant_version_id: version,
  grant_revision: 1,
  scope: "locations",
  location_ids: [location],
  roles: ["hr_ops"],
  actions: ["hrm-access"],
  session_established: false,
  operational_access_granted: false,
  ...extra,
});
const selected = (extra = {}) => ({
  company_id: company.id,
  module: "hrm",
  session_established: true,
  expires_at: new Date(Date.now() + 3600000).toISOString(),
  operational_access_granted: false,
  next_step: "erp_access_pending",
  ...extra,
});
async function grant(extra) {
  return createErpSessionClient(async () => Response.json(review(extra))).review(company, "hrm");
}
test("ordinary member self-review uses exact company/link/module and cookie-only transport", async () => {
  const result = await createErpSessionClient(async (url, options) => {
    assert.equal(
      url,
      `${prefix}/review?company_id=${company.id}&link_id=${company.linkId}&module=hrm`,
    );
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.deepEqual(options.headers, { Accept: "application/json" });
    return Response.json(review());
  }).review(company, "hrm");
  assert.equal(result.scope, "locations");
  assert.deepEqual(result.locations, [location]);
  assert.ok(Object.isFrozen(result) && Object.isFrozen(result.locations));
});
test("invalid selectors fail before making a request", async () => {
  let calls = 0;
  const api = createErpSessionClient(async () => {
    calls++;
    return Response.json({});
  });
  await assert.rejects(api.review({ ...company, id: "payer" }, "hrm"), { status: 400 });
  await assert.rejects(api.review(company, "payroll"), { status: 400 });
  assert.equal(calls, 0);
});
test("review rejects foreign company/module, missing or extra authority fields", async () => {
  for (const change of [
    { company_id: version },
    { module: "procurement" },
    { access_token: "secret" },
    { session_established: true },
    { operational_access_granted: true },
    { grant_revision: 1.5 },
    { grant_version_id: "" },
  ])
    await assert.rejects(grant(change), { status: 502 });
});
test("finite and company-wide scopes cannot become empty or ambiguous", async () => {
  for (const change of [
    { location_ids: [] },
    { location_ids: [location, location] },
    { scope: "company" },
    { scope: "all" },
    { location_ids: ["00000000-0000-0000-0000-000000000000"] },
    { roles: [] },
    { actions: [] },
  ])
    await assert.rejects(grant(change), { status: 502 });
  const all = await grant({ scope: "company", location_ids: [] });
  assert.equal(all.scope, "company");
});
test("company-wide confirmation must be explicit; finite scope cannot claim all branches", async () => {
  const all = await grant({ scope: "company", location_ids: [] });
  assert.throws(() => createErpSelectionAttempt(all, false), { status: 400 });
  const finite = await grant();
  assert.throws(() => createErpSelectionAttempt(finite, true), { status: 400 });
  assert.equal(
    JSON.parse(createErpSelectionAttempt(all, true).body).all_company_locations_confirmed,
    true,
  );
});
test("selection is a frozen exact command with CSRF and no credential or tenant selectors", async () => {
  const attempt = createErpSelectionAttempt(await grant(), false);
  let calls = 0;
  const api = createErpSessionClient(async (url, options) => {
    calls++;
    assert.equal(url, prefix + "/select");
    assert.equal(options.body, attempt.body);
    assert.equal(options.method, "POST");
    assert.equal(options.headers["X-Kuubu-Operational-CSRF"], csrf);
    const body = JSON.parse(options.body);
    assert.equal(body.confirmed, true);
    assert.equal(body.scope, "locations");
    assert.equal(body.grant_version_id, version);
    assert.equal(body.company_id, company.id);
    assert.deepEqual(
      Object.keys(body).sort(),
      [
        "company_id",
        "link_id",
        "module",
        "grant_version_id",
        "grant_revision",
        "scope",
        "location_ids",
        "all_company_locations_confirmed",
        "confirmed",
        "command_key",
      ].sort(),
    );
    if (calls === 1) throw new Error("lost response");
    return Response.json(selected());
  });
  await assert.rejects(api.select(attempt, csrf), { status: 503 });
  assert.equal(calls, 1);
  assert.equal((await api.select(attempt, csrf)).companyId, company.id);
  assert.equal(calls, 2);
  assert.ok(Object.isFrozen(attempt));
});
test("bad CSRF never issues a command", async () => {
  let calls = 0;
  const api = createErpSessionClient(async () => {
    calls++;
    return Response.json({});
  });
  await assert.rejects(api.select(createErpSelectionAttempt(await grant(), false), "bad"), {
    status: 403,
  });
  assert.equal(calls, 0);
});
test("selection response cannot establish foreign, expired or claimed broad operational access", async () => {
  const attempt = createErpSelectionAttempt(await grant(), false);
  for (const change of [
    { company_id: version },
    { module: "procurement" },
    { expires_at: "yesterday" },
    { expires_at: new Date(0).toISOString() },
    { operational_access_granted: true },
    { access_token: "never" },
  ])
    await assert.rejects(
      createErpSessionClient(async () => Response.json(selected(change))).select(attempt, csrf),
      { status: 502 },
    );
});
test("current session validates scope and retains no browser-visible session token", async () => {
  const data = { ...selected(), scope: "locations", location_ids: [location] };
  const result = await createErpSessionClient(async () => Response.json(data)).session();
  assert.deepEqual(result.locations, [location]);
  assert.equal(Object.hasOwn(result, "token"), false);
  for (const change of [{ location_ids: [] }, { scope: "company" }, { token: "never" }])
    await assert.rejects(
      createErpSessionClient(async () => Response.json({ ...data, ...change })).session(),
      { status: 502 },
    );
});
test("logout only succeeds on confirmed no-content and service denials never trigger a retry", async () => {
  let count = 0;
  const api = createErpSessionClient(async (url, options) => {
    count++;
    assert.equal(url, prefix + "/logout");
    assert.equal(options.body, "{}");
    return new Response(null, { status: 503 });
  });
  await assert.rejects(api.logout(csrf), { status: 503 });
  assert.equal(count, 1);
  await assert.rejects(createErpSessionClient(async () => Response.json({})).logout(csrf), {
    status: 502,
  });
  await createErpSessionClient(async () => new Response(null, { status: 204 })).logout(csrf);
});
