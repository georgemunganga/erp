import assert from "node:assert/strict";
import test from "node:test";
import { createModuleAccessClient } from "../src/modules/core/product-module-access-api.ts";
const company = {
  id: "11111111-1111-1111-1111-111111111111",
  linkId: "22222222-2222-2222-2222-222222222222",
  name: "Company",
  role: "owner",
};
const member = {
  membership_id: "33333333-3333-3333-3333-333333333333",
  display_name: "Alice",
  role: "member",
  membership_active: true,
  identity_available: true,
  revision: 1,
};
const branch = {
  location_id: "44444444-4444-4444-4444-444444444444",
  name: "Lusaka",
  available: true,
};
const version = "55555555-5555-5555-5555-555555555555";
const csrf = "c".repeat(43);
const change = () => ({
  company,
  member: {
    id: member.membership_id,
    name: "Alice",
    role: "member",
    active: true,
    identityAvailable: true,
    revision: 1,
  },
  module: "hrm",
  expectedRevision: 0,
  commandKey: "66666666-6666-6666-6666-666666666666",
  revoke: false,
  roles: ["hr_ops"],
  actions: ["hrm-access"],
  scope: "locations",
  locations: [branch.location_id],
  allCompanyConfirmed: false,
});
const review = () => ({
  member,
  module: "hrm",
  roles: ["hr_ops", "hr_admin"],
  actions: ["hrm-access", "hrm-admin"],
  grant: null,
  operational_access_granted: false,
});
const result = () => ({
  status: "Changed",
  version_id: version,
  revision: 1,
  state: "granted",
  operational_access_granted: false,
});
const reply = (value) => new Response(JSON.stringify(value), { status: 200 });
test("owner discovery uses exact same-origin company/link selectors with bounded transport", async () => {
  let request;
  const api = createModuleAccessClient(async (url, options) => {
    request = { url, options };
    return reply({ members: [member], next_cursor: null, operational_access_granted: false });
  });
  assert.equal((await api.members(company, null)).rows[0].id, member.membership_id);
  const url = new URL(request.url, "https://app.kuubuerp.com");
  assert.deepEqual([...url.searchParams.keys()], ["company_id", "link_id"]);
  assert.equal(request.options.credentials, "same-origin");
  assert.equal(request.options.cache, "no-store");
  assert.equal(request.options.redirect, "error");
  assert.ok(request.options.signal);
});
test("current review binds the exact member/module and accepts a missing grant", async () => {
  const api = createModuleAccessClient(async () => reply(review()));
  const data = await api.review(company, member.membership_id, "hrm");
  assert.equal(data.grant, null);
  assert.equal(data.member.revision, 1);
});
for (const fault of [
  "wrong-member",
  "wrong-module",
  "extra-selector",
  "admission",
  "duplicate-location",
  "revoked-actions",
])
  test("review rejects " + fault, async () => {
    const data = review();
    if (fault === "wrong-member") data.member = { ...member, membership_id: branch.location_id };
    if (fault === "wrong-module") data.module = "procurement";
    if (fault === "extra-selector") data.tenant_id = "foreign";
    if (fault === "admission") data.operational_access_granted = true;
    if (fault === "duplicate-location" || fault === "revoked-actions")
      data.grant = {
        version_id: version,
        revision: 1,
        membership_revision: 1,
        state: fault === "revoked-actions" ? "revoked" : "granted",
        roles: ["hr_ops"],
        actions: ["hrm-access"],
        scope: "locations",
        locations: [branch, ...(fault === "duplicate-location" ? [branch] : [])],
        requires_reconfirmation: false,
      };
    await assert.rejects(
      createModuleAccessClient(async () => reply(data)).review(
        company,
        member.membership_id,
        "hrm",
      ),
      (error) => error.status === 502,
    );
  });
test("branch directory cannot disclose unavailable names or duplicated rows", async () => {
  for (const locations of [[branch, branch], [{ ...branch, available: false }]])
    await assert.rejects(
      createModuleAccessClient(async () =>
        reply({ locations, next_cursor: null, operational_access_granted: false }),
      ).locations(company, null),
    );
});
test("catalog cannot reintroduce global admin or omit module entry authorization", async () => {
  for (const data of [
    { ...review(), roles: ["erp_admin"] },
    { ...review(), actions: ["hrm-admin"] },
  ])
    await assert.rejects(
      createModuleAccessClient(async () => reply(data)).review(
        company,
        member.membership_id,
        "hrm",
      ),
      (error) => error.status === 502,
    );
});
for (const fault of [
  "empty-branches",
  "implicit-company",
  "duplicate-branch",
  "missing-entry",
  "hr-admin",
  "invalid-company",
  "invalid-key",
])
  test("invalid reviewed change makes no request: " + fault, async () => {
    let calls = 0;
    const value = change();
    if (fault === "empty-branches") value.locations = [];
    if (fault === "implicit-company") {
      value.scope = "company";
      value.locations = [];
    }
    if (fault === "duplicate-branch") value.locations.push(value.locations[0]);
    if (fault === "missing-entry") value.actions = [];
    if (fault === "hr-admin") value.actions.push("hrm-admin");
    if (fault === "invalid-company") value.company = { ...company, id: "foreign" };
    if (fault === "invalid-key") value.commandKey = "invented";
    await assert.rejects(
      createModuleAccessClient(async () => {
        calls++;
        return reply(result());
      }).save(value, csrf),
    );
    assert.equal(calls, 0);
  });
test("confirmed POST contains exact fields and explicit company-wide confirmation", async () => {
  let request;
  const value = { ...change(), scope: "company", locations: [], allCompanyConfirmed: true };
  await createModuleAccessClient(async (url, options) => {
    request = { url, options };
    return reply(result());
  }).save(value, csrf);
  const body = JSON.parse(request.options.body);
  assert.equal(Object.keys(body).length, 14);
  assert.equal(body.confirmed, true);
  assert.equal(body.all_company_locations_confirmed, true);
  assert.deepEqual(body.location_ids, []);
  assert.equal(request.options.headers["X-Kuubu-Operational-CSRF"], csrf);
  assert.equal(request.options.method, "POST");
  assert.ok(!("tenant_id" in body) && !("enrollment_id" in body));
});
test("uncertain save is never retried automatically; explicit retry retains the reviewed key and payload", async () => {
  const bodies = [];
  const api = createModuleAccessClient(async (_, options) => {
    bodies.push(options.body);
    return new Response("{}", { status: 503 });
  });
  const value = change();
  await assert.rejects(api.save(value, csrf));
  assert.equal(bodies.length, 1);
  await assert.rejects(api.save(value, csrf));
  assert.equal(bodies.length, 2);
  assert.equal(bodies[0], bodies[1]);
});
test("revoke sends no roles, actions or branch scope", async () => {
  let body;
  const value = {
    ...change(),
    revoke: true,
    expectedRevision: 1,
    roles: [],
    actions: [],
    scope: "none",
    locations: [],
  };
  await createModuleAccessClient(async (_, options) => {
    body = JSON.parse(options.body);
    return reply({ ...result(), revision: 2, state: "revoked" });
  }).save(value, csrf);
  assert.deepEqual([body.roles, body.actions, body.location_ids], [[], [], []]);
  assert.equal(body.operation, "revoke");
  assert.equal(body.scope, "none");
});
test("success cannot claim ERP admission or a mismatched historical result", async () => {
  for (const data of [
    { ...result(), operational_access_granted: true },
    { ...result(), state: "revoked" },
    { ...result(), revision: 8 },
  ])
    await assert.rejects(
      createModuleAccessClient(async () => reply(data)).save(change(), csrf),
      (error) => error.status === 502,
    );
});
