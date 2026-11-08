import test from "node:test";
import assert from "node:assert/strict";
import { createProductTeamClient, ProductTeamError } from "../src/modules/core/product-team-api.ts";
const company = {
  id: "11111111-1111-1111-1111-111111111111",
  linkId: "22222222-2222-2222-2222-222222222222",
};
const invitation = "33333333-3333-3333-3333-333333333333";
const command = {
  ...{ companyId: company.id, linkId: company.linkId },
  operation: "issue",
  email: "person@example.test",
  role: "member",
  key: "44444444-4444-4444-4444-444444444444",
};
const row = {
  invitation_id: invitation,
  recipient: command.email,
  role: "member",
  state: "pending",
  revision: 1,
  expires_at: "2026-11-12T12:00:00Z",
};
const list = {
  invitations: [row],
  next_cursor: null,
  actor_role: "owner",
  operational_access_granted: false,
};
const reply = (body, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
test("invitation requests use fixed same-origin scoped paths and expose no bearer or secret", async () => {
  const calls = [];
  const api = createProductTeamClient(async (url, options) => {
    calls.push({ url, options });
    return options.method === "GET"
      ? reply(list)
      : reply(
          {
            status: "Issued",
            invitation_id: invitation,
            state: "pending",
            revision: 1,
            operational_access_granted: false,
          },
          201,
        );
  });
  assert.equal((await api.invitations(company)).invitations[0].recipient, command.email);
  await api.invitationCommand(command, "csrf-in-memory");
  for (const { url, options } of calls) {
    assert.ok(url.startsWith("/api/core/product-identity/team/invitations"));
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.mode, "same-origin");
    assert.equal(options.redirect, "error");
    assert.equal(options.cache, "no-store");
    assert.equal(options.headers.Authorization, undefined);
    assert.ok(!url.includes(command.email));
  }
  assert.equal(calls[1].options.headers["X-Kuubu-Team-CSRF"], "csrf-in-memory");
  assert.deepEqual(JSON.parse(calls[1].options.body), {
    company_id: company.id,
    link_id: company.linkId,
    command_key: command.key,
    confirmed: true,
    email: command.email,
    role: "member",
  });
});
test("an ambiguous retry preserves the exact reviewed key and body", async () => {
  const bodies = [];
  const api = createProductTeamClient(async (_, options) => {
    bodies.push(options.body);
    if (bodies.length === 1) throw new TypeError("response lost");
    return reply({
      status: "Existing",
      invitation_id: invitation,
      state: "pending",
      revision: 1,
      operational_access_granted: false,
    });
  });
  await assert.rejects(api.invitationCommand(command, "csrf"));
  assert.equal(await api.invitationCommand(command, "csrf"), undefined);
  assert.equal(bodies[0], bodies[1]);
});
for (const patch of [
  { state: "sent" },
  { role: "owner" },
  { revision: 2 },
  { recipient: "private\n@example.test" },
  { invitation_id: "foreign" },
])
  test(`invalid invitation data fails closed: ${JSON.stringify(patch)}`, async () => {
    const api = createProductTeamClient(async () =>
      reply({ ...list, invitations: [{ ...row, ...patch }] }),
    );
    await assert.rejects(
      api.invitations(company),
      (error) => error instanceof ProductTeamError && error.status === 502,
    );
  });
test("duplicate records and invalid continuation tokens cannot become an invitation page", async () => {
  for (const patch of [{ invitations: [row, row] }, { next_cursor: invitation }]) {
    const api = createProductTeamClient(async () => reply({ ...list, ...patch }));
    await assert.rejects(api.invitations(company));
  }
});
test("revocation uses the reviewed revision and validates the exact returned invitation", async () => {
  const revoke = {
    companyId: company.id,
    linkId: company.linkId,
    key: command.key,
    operation: "revoke",
    invitationId: invitation,
    revision: 1,
  };
  let body;
  const api = createProductTeamClient(async (_, options) => {
    body = JSON.parse(options.body);
    return reply({
      status: "Revoked",
      invitation_id: invitation,
      state: "revoked",
      revision: 2,
      operational_access_granted: false,
    });
  });
  await api.invitationCommand(revoke, "csrf");
  assert.equal(body.expected_revision, 1);
  assert.equal(body.confirmed, true);
  const foreign = createProductTeamClient(async () =>
    reply({
      status: "Revoked",
      invitation_id: command.key,
      state: "revoked",
      revision: 2,
      operational_access_granted: false,
    }),
  );
  await assert.rejects(foreign.invitationCommand(revoke, "csrf"));
});
for (const availability of [undefined, false, true, "true"]) {
  test(`invitation capability must be an explicit boolean: ${availability}`, async () => {
    const api = createProductTeamClient(async () =>
      reply({
        verified: true,
        product_code: "kuubu",
        purpose: "company-team-v1",
        csrf_token: "A".repeat(100),
        expires_at: "2026-11-12T12:00:00Z",
        operational_access_granted: false,
        invitation_management_available: availability,
      }),
    );
    if (typeof availability === "string") await assert.rejects(api.session());
    else assert.equal((await api.session()).invitationManagementAvailable, availability === true);
  });
}
