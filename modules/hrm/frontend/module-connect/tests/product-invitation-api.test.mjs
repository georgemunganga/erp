import test from "node:test";
import assert from "node:assert/strict";
import {
  createInvitationClient,
  invitationStart,
  InvitationError,
} from "../src/modules/core/product-invitation-api.ts";
const nonce = "n".repeat(43),
  token = "t".repeat(43);
const invitation = "11111111-1111-1111-1111-111111111111",
  company = "22222222-2222-2222-2222-222222222222",
  member = "33333333-3333-3333-3333-333333333333";
const attempt = {
  key: "44444444-4444-4444-4444-444444444444",
  revision: 1,
  invitationId: invitation,
};
const session = {
  verified: true,
  purpose: "company-invitation-v1",
  csrf_token: nonce,
  expires_at: new Date(Date.now() + 240000).toISOString(),
  operational_access_granted: false,
};
const review = {
  invitation_id: invitation,
  company_id: company,
  company_name: "Acacia Trading",
  role: "member",
  revision: 1,
  expires_at: new Date(Date.now() + 86400000).toISOString(),
  operational_access_granted: false,
};
const receipt = {
  status: "Accepted",
  invitation_id: invitation,
  membership_id: member,
  revision: 2,
  operational_access_granted: false,
};
const failure = (error) => error instanceof InvitationError && error.status === 502;
test("fixed same-origin transport sends only the selected invitation operations and reviewed command", async () => {
  const calls = [];
  const api = createInvitationClient(async (url, options) => {
    calls.push({ url, options });
    if (url.endsWith("/session")) return Response.json(session);
    if (url.endsWith("/prepare"))
      return Response.json(
        { continue_url: invitationStart, operational_access_granted: false },
        { status: 201 },
      );
    if (url.endsWith("/review")) return Response.json(review);
    if (url.endsWith("/accept")) return Response.json(receipt);
    return new Response(null, { status: 204 });
  });
  assert.equal((await api.session()).verified, true);
  await api.prepare(token, nonce);
  assert.equal((await api.review()).companyName, "Acacia Trading");
  await api.accept(attempt, nonce);
  await api.accept(attempt, nonce);
  await api.cancel(nonce);
  for (const { url, options } of calls) {
    assert.ok(url.startsWith("/api/core/product-identity/invitation/"));
    assert.ok(!url.includes(token));
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.mode, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.headers.Authorization, undefined);
    assert.ok(options.signal instanceof AbortSignal);
  }
  assert.deepEqual(JSON.parse(calls[1].options.body), { token });
  assert.deepEqual(JSON.parse(calls[3].options.body), {
    command_key: attempt.key,
    expected_revision: 1,
    confirmed: true,
  });
  assert.equal(calls[3].options.body, calls[4].options.body);
  assert.equal(calls[3].options.headers["X-Kuubu-Invitation-CSRF"], nonce);
});
test("session metadata must match the distinct current bounded purpose", async () => {
  for (const patch of [
    { verified: "true" },
    { purpose: "company-team-v1" },
    { csrf_token: "short" },
    { expires_at: "not-a-date" },
    { expires_at: new Date(Date.now() - 1000).toISOString() },
    { expires_at: new Date(Date.now() + 3600000).toISOString() },
    { operational_access_granted: true },
    { access_token: "must-not-be-a-browser-token" },
  ]) {
    await assert.rejects(
      createInvitationClient(async () => Response.json({ ...session, ...patch })).session(),
      failure,
    );
  }
  assert.equal(
    (
      await createInvitationClient(async () =>
        Response.json({ ...session, verified: false }),
      ).session()
    ).verified,
    false,
  );
});
test("review accepts only the targeted bounded company and immutable invitation role", async () => {
  for (const patch of [
    { invitation_id: "foreign" },
    { company_id: "" },
    { company_name: "" },
    { company_name: "x".repeat(201) },
    { company_name: "a\nb" },
    { role: "owner" },
    { role: ["member"] },
    { revision: 2 },
    { operational_access_granted: true },
    { token },
  ]) {
    await assert.rejects(
      createInvitationClient(async () => Response.json({ ...review, ...patch })).review(),
      failure,
    );
  }
});
test("prepare never follows a supplied destination or an incorrect success status", async () => {
  for (const url of [
    "https://foreign.invalid/",
    "//app.kuubuerp.com/",
    "/team",
    invitationStart + "?token=" + token,
  ]) {
    await assert.rejects(
      createInvitationClient(async () =>
        Response.json({ continue_url: url, operational_access_granted: false }, { status: 201 }),
      ).prepare(token, nonce),
      failure,
    );
  }
  await assert.rejects(
    createInvitationClient(async () =>
      Response.json({ continue_url: invitationStart, operational_access_granted: false }),
    ).prepare(token, nonce),
    failure,
  );
});
test("accept receipts cannot substitute another invitation or assert operational access", async () => {
  for (const patch of [
    { status: "Issued" },
    { status: ["Accepted"] },
    { invitation_id: company },
    { membership_id: "" },
    { revision: 1 },
    { operational_access_granted: true },
    { role: "owner" },
  ]) {
    await assert.rejects(
      createInvitationClient(async () => Response.json({ ...receipt, ...patch })).accept(
        attempt,
        nonce,
      ),
      failure,
    );
  }
  await createInvitationClient(async () =>
    Response.json({ ...receipt, status: "Existing" }),
  ).accept(attempt, nonce);
});
test("malformed commands and secrets fail before any request", async () => {
  let calls = 0;
  const api = createInvitationClient(async () => {
    calls++;
    throw new Error();
  });
  for (const bad of ["", null, {}, "short"]) {
    await assert.rejects(api.prepare(bad, nonce));
    await assert.rejects(api.prepare(token, bad));
  }
  for (const patch of [
    { key: "" },
    { key: "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA" },
    { revision: 2 },
    { invitationId: "" },
  ])
    await assert.rejects(api.accept({ ...attempt, ...patch }, nonce));
  assert.equal(calls, 0);
});
test("transport failures preserve uncertainty and never expose error bodies", async () => {
  for (const status of [401, 403, 409, 429, 503])
    await assert.rejects(
      createInvitationClient(async () => Response.json({ secret: token }, { status })).session(),
      (error) => error.status === status && !error.message.includes(token),
    );
  await assert.rejects(
    createInvitationClient(async () => {
      throw new Error(token);
    }).accept(attempt, nonce),
    (error) => error.status === 503 && !error.message.includes(token),
  );
  await assert.rejects(
    createInvitationClient(
      async () => new Response("bad-json", { headers: { "Content-Type": "text/html" } }),
    ).session(),
    failure,
  );
  await assert.rejects(
    createInvitationClient(
      async () =>
        new Response("x".repeat(8193), { headers: { "Content-Type": "application/json" } }),
    ).session(),
    failure,
  );
});
