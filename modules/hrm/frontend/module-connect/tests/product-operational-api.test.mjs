import assert from "node:assert/strict";
import test from "node:test";
import {
  createOperationalClient,
  OperationalError,
} from "../src/modules/core/product-operational-api.ts";
const prefix = "/api/core/product-identity/operational";
const csrf = "c".repeat(43);
const row = {
  company_id: "11111111-1111-1111-1111-111111111111",
  link_id: "22222222-2222-2222-2222-222222222222",
  name: "Company A",
  role: "member",
};
const selected = { id: row.company_id, linkId: row.link_id, name: row.name, role: row.role };
const proof = () => ({
  verified: true,
  purpose: "erp-session-v1",
  product_code: "kuubu",
  csrf_token: csrf,
  operational_access_granted: false,
  next_step: "company_selection",
  proof_until: new Date(Date.now() + 29000).toISOString(),
  company_discovery_available: true,
});
const authorization = () =>
  "https://www.kuubuerp.com/account/authorize/?" +
  new URLSearchParams({
    client_id: row.company_id,
    response_type: "code",
    redirect_uri: "https://app.kuubuerp.com/api/core/product-identity/operational/callback",
    scope: "product:identity product:erp-session",
    state: "s".repeat(800),
    code_challenge_method: "S256",
    code_challenge: "k".repeat(43),
  });
test("identity session is bounded person continuity and never a claimed ERP grant", async () => {
  const api = createOperationalClient(async (url, options) => {
    assert.equal(url, prefix + "/session");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.redirect, "error");
    return Response.json(proof());
  });
  assert.deepEqual(await api.session(), { csrf, companiesAvailable: true });
  for (const extra of [
    { operational_access_granted: true },
    { purpose: "company-team-v1" },
    { proof_until: "not-a-date" },
    { access_token: "never" },
  ]) {
    await assert.rejects(
      createOperationalClient(async () => Response.json({ ...proof(), ...extra })).session(),
      (error) => error.status === 502,
    );
  }
});
test("start uses same-origin empty JSON and CSRF and only returns the fixed authorization URL", async () => {
  const url = authorization();
  const api = createOperationalClient(async (path, options) => {
    assert.equal(path, prefix + "/start");
    assert.equal(options.method, "POST");
    assert.equal(options.body, "{}");
    assert.equal(options.headers["X-Kuubu-Operational-CSRF"], csrf);
    return Response.json({ authorization_url: url });
  });
  assert.equal(await api.start(csrf), url);
  await assert.rejects(api.start("forged"), (error) => error.status === 403);
  for (const unsafe of [
    url.replace("www.kuubuerp.com", "foreign.invalid"),
    url + "&scope=product:license",
    url + "#token",
    url.replace("operational%2Fcallback", "callback"),
    url.replace("product%3Aerp-session", "product%3Aworkspace-setup"),
  ]) {
    await assert.rejects(
      createOperationalClient(async () => Response.json({ authorization_url: unsafe })).start(csrf),
      (error) => error.status === 502,
    );
  }
});
test("company discovery accepts explicit ordinary members and never emits tenant or payer selectors", async () => {
  const calls = [];
  const api = createOperationalClient(async (path, options) => {
    calls.push({ path, options });
    return Response.json({
      companies: [row],
      next_cursor: null,
      operational_access_granted: false,
    });
  });
  assert.deepEqual(await api.companies(), { rows: [selected], next: null });
  assert.equal(calls[0].path, prefix + "/companies");
  await assert.rejects(api.companies("customer-payer"), (error) => error.status === 400);
  assert.equal(calls.length, 1);
  for (const data of [
    { companies: [{ ...row, role: "erp_admin" }] },
    { companies: [row, row] },
    { operational_access_granted: true },
    { tenant_id: "foreign" },
  ])
    await assert.rejects(
      createOperationalClient(async () =>
        Response.json({
          companies: [row],
          next_cursor: null,
          operational_access_granted: false,
          ...data,
        }),
      ).companies(),
      (error) => error.status === 502,
    );
});
test("company review must match the exact selected company and link with informational module codes", async () => {
  const body = {
    company: row,
    licensed_modules: ["hrm"],
    operational_access_granted: false,
    next_step: "module_access_assignment",
  };
  const api = createOperationalClient(async (path) => {
    assert.equal(path, prefix + "/companies/" + row.company_id + "?link_id=" + row.link_id);
    return Response.json(body);
  });
  assert.deepEqual(await api.review(selected), { company: selected, modules: ["hrm"] });
  for (const change of [
    { company: { ...row, link_id: row.company_id } },
    { operational_access_granted: true },
    { licensed_modules: ["hrm", "hrm"] },
    { next_step: "erp_session" },
  ])
    await assert.rejects(
      createOperationalClient(async () => Response.json({ ...body, ...change })).review(selected),
      (error) => error.status === 502,
    );
});
test("abort propagates and service failures never become a preview identity or successful signout", async () => {
  const controller = new AbortController();
  controller.abort();
  await assert.rejects(
    createOperationalClient(async () => {
      throw new Error("aborted");
    }).companies(null, controller.signal),
  );
  await assert.rejects(
    createOperationalClient(async () => Response.json({}, { status: 503 })).session(),
    (error) => error instanceof OperationalError && error.status === 503,
  );
  await assert.rejects(
    createOperationalClient(async () => Response.json({}, { status: 403 })).logout(csrf),
    (error) => error.status === 403,
  );
});
