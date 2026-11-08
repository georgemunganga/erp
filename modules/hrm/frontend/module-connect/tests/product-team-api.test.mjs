import test from "node:test";
import assert from "node:assert/strict";
import { createProductTeamClient, ProductTeamError } from "../src/modules/core/product-team-api.ts";
const company = {
  id: "11111111-1111-1111-1111-111111111111",
  linkId: "22222222-2222-2222-2222-222222222222",
  name: "Company A",
  role: "owner",
};
const member = "33333333-3333-3333-3333-333333333333";
const command = {
  companyId: company.id,
  linkId: company.linkId,
  memberId: member,
  key: "44444444-4444-4444-4444-444444444444",
  revision: 1,
  operation: "role-change",
  role: "administrator",
};
const csrf = "A".repeat(200);
const proof = {
  verified: true,
  product_code: "kuubu",
  purpose: "company-team-v1",
  expires_at: new Date(Date.now() + 300000).toISOString(),
  csrf_token: csrf,
  operational_access_granted: false,
};
const result = {
  status: "Changed",
  membership_id: member,
  role: "administrator",
  membership_active: true,
  revision: 2,
  operational_access_granted: false,
};
const reply = (body, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

test("team session remains separate from ERP access and bearer credentials", async () => {
  let request;
  const api = createProductTeamClient(async (url, options) => {
    request = { url, options };
    return reply(proof);
  });
  assert.deepEqual(await api.session(), { csrf, expiresAt: proof.expires_at });
  assert.equal(request.url, "/api/core/product-identity/team/session");
  for (const [key, value] of Object.entries({
    credentials: "same-origin",
    mode: "same-origin",
    cache: "no-store",
    redirect: "error",
  }))
    assert.equal(request.options[key], value);
  assert.equal(request.options.headers.Authorization, undefined);
});
for (const [field, value] of [
  ["purpose", "company-create-v1"],
  ["product_code", "other"],
  ["verified", false],
  ["operational_access_granted", true],
  ["csrf_token", "short"],
  ["expires_at", "invalid"],
]) {
  test(`reject invalid proof ${field}`, async () => {
    const api = createProductTeamClient(async () => reply({ ...proof, [field]: value }));
    await assert.rejects(
      api.session(),
      (error) => error instanceof ProductTeamError && error.status === 502,
    );
  });
}
test("directory metadata never substitutes for licensed member authority", async () => {
  const api = createProductTeamClient(async () =>
    reply({
      companies: [
        {
          company_id: company.id,
          link_id: company.linkId,
          company_name: company.name,
          role: "owner",
        },
      ],
      next_cursor: null,
      license_check_required: true,
      operational_access_granted: false,
    }),
  );
  assert.deepEqual((await api.companies()).companies, [company]);
  await assert.rejects(api.companies("foreign?tenant=other"), (error) => error.status === 400);
});
test("member role and availability are validated independently", async () => {
  const response = {
    members: [
      {
        membership_id: member,
        display_name: "Ben",
        role: "member",
        membership_active: false,
        identity_available: true,
        revision: 3,
      },
    ],
    actor_role: "administrator",
    next_cursor: null,
    operational_access_granted: false,
  };
  const api = createProductTeamClient(async () => reply(response));
  const page = await api.members(company);
  assert.equal(page.members[0].active, false);
  assert.equal(page.members[0].identityAvailable, true);
  response.actor_role = "member";
  await assert.rejects(api.members(company), (error) => error.status === 502);
});
test("confirmed mutation uses only selectors, target role, revision and stable operation key", async () => {
  const requests = [];
  const api = createProductTeamClient(async (url, options) => {
    requests.push({ url, options });
    return reply(result);
  });
  await api.execute(command, csrf);
  await api.execute(command, csrf);
  assert.equal(requests[0].options.body, requests[1].options.body);
  assert.equal(requests[0].options.headers["X-Kuubu-Team-CSRF"], csrf);
  assert.ok(!requests[0].url.includes(csrf));
  assert.deepEqual(JSON.parse(requests[0].options.body), {
    company_id: company.id,
    link_id: company.linkId,
    membership_id: member,
    command_key: command.key,
    expected_revision: 1,
    operation: "role-change",
    confirmed: true,
    role: "administrator",
  });
});
test("historical receipts are acknowledged rather than installed as current membership", async () => {
  const api = createProductTeamClient(async () => reply({ ...result, status: "Existing" }));
  assert.equal(await api.execute(command, csrf), undefined);
});
for (const patch of [
  { membership_id: company.id },
  { operational_access_granted: true },
  { role: "owner" },
  { revision: 99 },
  { membership_active: false },
]) {
  test(`reject ambiguous successful mutation ${Object.keys(patch)[0]}`, async () => {
    const api = createProductTeamClient(async () => reply({ ...result, ...patch }));
    await assert.rejects(api.execute(command, csrf), (error) => error.status === 502);
  });
}
test("revoke omits role and cannot become a role-change request", async () => {
  let body;
  const api = createProductTeamClient(async (_, options) => {
    body = JSON.parse(options.body);
    return reply({ ...result, role: "member", membership_active: false });
  });
  await api.execute({ ...command, operation: "revoke" }, csrf);
  assert.equal(Object.hasOwn(body, "role"), false);
});
test("last-owner conflict is bounded and raw service responses are never shown", async () => {
  const api = createProductTeamClient(async () =>
    reply({ status: "LastOwner", detail: "private server details" }, 409),
  );
  await assert.rejects(
    api.execute(command, csrf),
    (error) => error.code === "LastOwner" && !error.message.includes("private"),
  );
});
