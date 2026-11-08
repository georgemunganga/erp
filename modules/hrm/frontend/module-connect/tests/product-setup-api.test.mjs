import { test } from "node:test";
import assert from "node:assert/strict";
import {
  createProductSetupClient,
  ProductSetupError,
} from "../src/modules/core/product-setup-api.ts";

const customer = "01arz3ndektsv4rrffq69g5fav";
const id = "54f26fe8-23cf-4234-8a48-bd61ce188b44";
const session = { subject: "17", csrfToken: "x".repeat(43), expiresAt: "2026-11-08T15:00:00Z" };
const command = {
  customerId: customer,
  companyName: "Chosen company",
  idempotencyKey: "synthetic_request_0000000000000001",
};
const payer = { customer_id: customer, name: "Owned account", workspace_limit: 2 };
const reply = (data, status = 200) =>
  new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json" } });
const validCompany = {
  status: "Created",
  company_id: id,
  binding_id: id,
  operational_access_granted: false,
};

test("setup reads and commands use fixed same-origin, uncached, redirect-rejecting requests", async () => {
  const calls = [];
  const client = createProductSetupClient(async (url, options) => {
    calls.push({ url, options });
    if (url.endsWith("/session"))
      return reply({
        verified: true,
        subject: session.subject,
        csrf_token: session.csrfToken,
        expires_at: session.expiresAt,
        operational_access_granted: false,
      });
    if (url.includes("/payers")) return reply({ customers: [payer], next_cursor: null });
    if (url.endsWith("/cancel")) return new Response(null, { status: 204 });
    return reply(validCompany, 201);
  });
  assert.deepEqual(await client.session(), session);
  assert.deepEqual(await client.payers(), {
    customers: [{ customerId: customer, name: "Owned account", workspaceLimit: 2 }],
    nextCursor: null,
  });
  assert.equal((await client.create(session, command)).companyId, id);
  await client.cancel(session);
  for (const { url, options } of calls) {
    assert.ok(url.startsWith("/api/core/product-identity/setup/"));
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.mode, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.headers.Authorization, undefined);
    assert.ok(!url.includes(session.csrfToken));
  }
  assert.deepEqual(JSON.parse(calls[2].options.body), {
    customer_id: customer,
    company_name: command.companyName,
    idempotency_key: command.idempotencyKey,
    confirm: true,
  });
  assert.equal(calls[2].options.headers["X-Kuubu-Setup-CSRF"], session.csrfToken);
});

test("retry preserves the reviewed request and handles an existing company without an operational grant", async () => {
  const bodies = [];
  const client = createProductSetupClient(async (_, options) => {
    bodies.push(options.body);
    if (bodies.length === 1) throw new TypeError("network interruption");
    return reply({ ...validCompany, status: "Existing" });
  });
  await assert.rejects(client.create(session, command), TypeError);
  assert.equal((await client.create(session, command)).status, "Existing");
  assert.equal(bodies[0], bodies[1]);
});

for (const status of [401, 403, 413, 503])
  test(`HTTP ${status} is never accepted as successful setup`, async () => {
    const client = createProductSetupClient(
      async () => new Response("private upstream details", { status }),
    );
    await assert.rejects(
      client.create(session, command),
      (error) =>
        error instanceof ProductSetupError &&
        error.status === status &&
        !error.message.includes("private"),
    );
  });
for (const code of ["Conflict", "CapacityExhausted"])
  test(`conflict ${code} retains its bounded recovery code`, async () => {
    const client = createProductSetupClient(async () => reply({ status: code }, 409));
    await assert.rejects(
      client.create(session, command),
      (error) => error.status === 409 && error.code === code,
    );
  });
for (const patch of [
  { operational_access_granted: true },
  { status: "Existing" },
  { company_id: "unknown" },
  { binding_id: null },
]) {
  test(`invalid company result fails closed: ${JSON.stringify(patch)}`, async () => {
    const client = createProductSetupClient(async () => reply({ ...validCompany, ...patch }, 201));
    await assert.rejects(
      client.create(session, command),
      (error) => error.code === "invalid_setup_response",
    );
  });
}
test("HTML successful response cannot masquerade as a created company", async () => {
  const client = createProductSetupClient(
    async () => new Response("<html>Sign in</html>", { headers: { "Content-Type": "text/html" } }),
  );
  await assert.rejects(
    client.create(session, command),
    (error) => error.code === "invalid_setup_response",
  );
});
for (const data of [
  { customers: [payer, payer], next_cursor: null },
  { customers: [{ ...payer, workspace_limit: 0 }], next_cursor: null },
  { customers: [{ ...payer, workspace_limit: 1.5 }], next_cursor: null },
  { customers: [payer], next_cursor: customer },
  { customers: [], next_cursor: "invalid" },
])
  test(`malformed payer page fails closed: ${JSON.stringify(data)}`, async () => {
    const client = createProductSetupClient(async () => reply(data));
    await assert.rejects(client.payers(), (error) => error.code === "invalid_setup_response");
  });
test("invalid cursor fails before a network request", async () => {
  const client = createProductSetupClient(() => assert.fail("unexpected request"));
  await assert.rejects(
    client.payers("foreign?subject=1"),
    (error) => error.code === "invalid_setup_cursor",
  );
});

for (const subject of [undefined, "0", "017", "foreign", 17]) {
  test(`setup recovery requires a canonical verified subject: ${subject}`, async () => {
    const client = createProductSetupClient(async () =>
      reply({
        verified: true,
        subject,
        csrf_token: session.csrfToken,
        expires_at: session.expiresAt,
        operational_access_granted: false,
      }),
    );
    await assert.rejects(client.session(), (error) => error.code === "invalid_setup_response");
  });
}
