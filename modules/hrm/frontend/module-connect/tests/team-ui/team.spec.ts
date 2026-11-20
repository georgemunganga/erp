import { expect, test, type Page } from "@playwright/test";
const pageErrors = new WeakMap<Page, string[]>();
test.beforeEach(async ({ page }) => {
  pageErrors.set(page, []);
  page.on("pageerror", (error) => pageErrors.get(page)?.push(error.message));
  await page.route("**/*", (route) =>
    new URL(route.request().url()).origin === "http://127.0.0.1:3188"
      ? route.continue()
      : route.abort("blockedbyclient"),
  );
});
test.afterEach(({ page }) => {
  expect(pageErrors.get(page)).toEqual([]);
});
const company = "11111111-1111-1111-1111-111111111111";
const link = "22222222-2222-2222-2222-222222222222";
const target = "33333333-3333-3333-3333-333333333333";
const owner = "44444444-4444-4444-4444-444444444444";
const admin = "55555555-5555-5555-5555-555555555555";
test("enabled invitations require review and confirmation, then refresh authoritative status", async ({
  page,
}, testInfo) => {
  const f = await fixture(page, "owner", true);
  await page.goto("/team");
  await page.getByRole("button", { name: "Invite teammate", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("Acacia Trading");
  await dialog.getByLabel("Recipient email").fill("new.person@example.test");
  await expect(
    dialog.getByRole("button", { name: "Create invitation", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("combobox", { name: "Company", exact: true, includeHidden: true }),
  ).toBeDisabled();
  await dialog.getByRole("checkbox").check();
  await page.screenshot({ path: testInfo.outputPath("invitation-review.png"), fullPage: true });
  await dialog.getByRole("button", { name: "Create invitation", exact: true }).click();
  await expect(page.getByText("Awaiting acceptance", { exact: false })).toBeVisible();
  expect(f.invitationWrites).toHaveLength(1);
  expect(f.invitationWrites[0].role).toBe("member");
  await page
    .getByRole("button", { name: "Revoke invitation for new.person@example.test", exact: true })
    .click();
  await page.getByRole("dialog").getByRole("checkbox").check();
  await page.getByRole("button", { name: "Confirm revocation", exact: true }).click();
  await expect(page.getByText("Member · Revoked", { exact: true })).toBeVisible();
  expect(f.invitationWrites).toHaveLength(2);
  expect(f.otherRequests).toEqual([]);
});
test("uncertain invitation response keeps the reviewed body and key for retry", async ({
  page,
}) => {
  const f = await fixture(page, "invitation-ambiguous", true);
  await page.goto("/team");
  await page.getByRole("button", { name: "Invite teammate", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Recipient email").fill("retry@example.test");
  await dialog.getByRole("checkbox").check();
  await dialog.getByRole("button", { name: "Create invitation", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("couldn’t confirm");
  await expect(dialog.getByLabel("Recipient email")).toBeDisabled();
  await dialog
    .getByRole("button", { name: "Retry reviewed invitation request", exact: true })
    .click();
  await expect(
    page.getByRole("list", { name: "Company invitations", exact: true }).getByRole("listitem"),
  ).toHaveCount(1);
  expect(f.invitationWrites).toHaveLength(2);
  expect(f.invitationWrites[0]).toEqual(f.invitationWrites[1]);
});
test("administrator invitations cannot select privileged roles or revoke administrator invitations", async ({
  page,
}) => {
  await fixture(page, "admin", true);
  await page.goto("/team");
  await expect(
    page.getByRole("button", {
      name: "Revoke invitation for privileged@example.test",
      exact: true,
    }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Invite teammate", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByRole("combobox", { name: "Company role", exact: true })
    .click();
  await expect(page.getByRole("option", { name: "Administrator", exact: true })).toHaveCount(0);
  await expect(page.getByRole("option", { name: "Member", exact: true })).toBeVisible();
});
test("unavailable invitation capability offers no disconnected controls", async ({ page }) => {
  await fixture(page);
  await page.goto("/team");
  await expect(page.getByRole("heading", { name: "Acacia Trading", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Invite teammate", exact: true })).toHaveCount(0);
});
test("mobile invitation controls and recipient addresses stay inside the viewport", async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await fixture(page, "owner", true);
  await page.goto("/team");
  await page.getByRole("button", { name: "Invite teammate", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Recipient email").fill("mobile.person@example.test");
  await dialog.getByRole("checkbox").check();
  await dialog.getByRole("button", { name: "Create invitation", exact: true }).click();
  const button = page.getByRole("button", {
    name: "Revoke invitation for mobile.person@example.test",
    exact: true,
  });
  await button.scrollIntoViewIfNeeded();
  await expect(button).toBeInViewport();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("invitation-mobile.png"), fullPage: true });
});
async function fixture(page: Page, mode = "owner", invitations = false) {
  const writes: Record<string, unknown>[] = [];
  const otherRequests: string[] = [];
  let memberReads = 0;
  let invitationReads = 0;
  const invitationWrites: Record<string, unknown>[] = [];
  const invitationRows: Record<string, unknown>[] =
    mode === "admin" || mode.startsWith("delivery-")
      ? [
          {
            invitation_id: "66666666-6666-6666-6666-666666666666",
            recipient: "privileged@example.test",
            role: mode === "admin" ? "administrator" : "member",
            state: "pending",
            revision: 1,
            expires_at: new Date(Date.now() + 86400000).toISOString(),
          },
        ]
      : [];
  const invitationKeys = new Set<string>();
  let role = "member",
    active = true,
    revision = 1;
  await page.route("**/api/**", async (route) => {
    const request = route.request(),
      url = new URL(request.url());
    const path = url.pathname.replace("/api/core/product-identity/team", "");
    const send = (body: unknown, status = 200) => route.fulfill({ status, json: body });
    if (!url.pathname.startsWith("/api/core/product-identity/team/")) {
      otherRequests.push(url.pathname);
      await send({}, 401);
      return;
    }
    if (path === "/session") {
      if (mode === "signed-out") {
        await send({}, 401);
        return;
      }
      if (mode === "unavailable") {
        await send({}, 503);
        return;
      }
      await send({
        verified: true,
        product_code: "kuubu",
        purpose: "company-team-v1",
        csrf_token: "T".repeat(200),
        expires_at: new Date(Date.now() + 300000).toISOString(),
        operational_access_granted: false,
        invitation_management_available: invitations,
      });
      return;
    }
    if (path === "/companies") {
      await send({
        companies: [
          {
            company_id: company,
            link_id: link,
            company_name: "Acacia Trading",
            role: mode === "admin" ? "administrator" : "owner",
          },
        ],
        next_cursor: null,
        license_check_required: true,
        operational_access_granted: false,
      });
      return;
    }
    if (path === "/members") {
      memberReads++;
      if (mode === "denied") {
        await send({}, 403);
        return;
      }
      const members = [
        {
          membership_id: owner,
          display_name: "Alice Owner",
          role: "owner",
          membership_active: true,
          identity_available: true,
          revision: 1,
        },
        {
          membership_id: target,
          display_name: "Ben Member",
          role,
          membership_active: active,
          identity_available: true,
          revision,
        },
        {
          membership_id: admin,
          display_name: "Cara Administrator",
          role: "administrator",
          membership_active: true,
          identity_available: true,
          revision: 1,
        },
      ];
      if (mode === "paging" && url.searchParams.has("after")) members.splice(0, 2);
      else if (mode === "paging") members.splice(2, 1);
      await send({
        members,
        next_cursor: mode === "paging" && !url.searchParams.has("after") ? target : null,
        actor_role: mode === "admin" ? "administrator" : "owner",
        operational_access_granted: false,
      });
      return;
    }
    if (path === "/invitations") {
      invitationReads++;
      if (mode === "delivery-refresh")
        invitationRows[0].delivery =
          invitationReads === 1
            ? { state: "queued", attempts: 0, retry_at: null, accepted_at: null }
            : {
                state: "accepted",
                attempts: 1,
                retry_at: null,
                accepted_at: new Date().toISOString(),
              };
      if (mode === "delivery-retrying")
        invitationRows[0].delivery = {
          state: "retrying",
          attempts: 2,
          retry_at: new Date(Date.now() + 30000).toISOString(),
          accepted_at: null,
        };
      await send({
        invitations: invitationRows,
        next_cursor: null,
        actor_role: mode === "admin" ? "administrator" : "owner",
        operational_access_granted: false,
      });
      return;
    }
    if (path === "/invitations/issue" || path === "/invitations/revoke") {
      const body = request.postDataJSON() as Record<string, unknown>;
      invitationWrites.push(body);
      expect(body.confirmed).toBe(true);
      expect(request.headers()["x-kuubu-team-csrf"]).toBe("T".repeat(200));
      const previous = invitationKeys.has(String(body.command_key));
      invitationKeys.add(String(body.command_key));
      if (path.endsWith("/issue") && !previous)
        invitationRows.push({
          invitation_id: "77777777-7777-7777-7777-777777777777",
          recipient: body.email,
          role: body.role,
          state: "pending",
          revision: 1,
          expires_at: new Date(Date.now() + 86400000).toISOString(),
        });
      if (path.endsWith("/revoke")) {
        const row = invitationRows.find((x) => x.invitation_id === body.invitation_id);
        if (row) {
          row.state = "revoked";
          row.revision = 2;
        }
      }
      if (mode === "invitation-ambiguous" && invitationWrites.length === 1) {
        await route.abort("failed");
        return;
      }
      await send(
        {
          status: previous ? "Existing" : path.endsWith("/issue") ? "Issued" : "Revoked",
          invitation_id: body.invitation_id ?? "77777777-7777-7777-7777-777777777777",
          state: path.endsWith("/issue") ? "pending" : "revoked",
          revision: path.endsWith("/issue") ? 1 : 2,
          operational_access_granted: false,
        },
        path.endsWith("/issue") && !previous ? 201 : 200,
      );
      return;
    }
    if (path === "/commands") {
      const body = request.postDataJSON() as Record<string, unknown>;
      writes.push(body);
      expect(request.headers()["x-kuubu-team-csrf"]).toBe("T".repeat(200));
      expect(body.confirmed).toBe(true);
      if (mode === "last-owner") {
        await send({ status: "LastOwner" }, 409);
        return;
      }
      if (mode === "ambiguous" && writes.length === 1) {
        role = "administrator";
        active = false;
        revision = 3;
        await route.abort("failed");
        return;
      }
      if (mode === "ambiguous") {
        await send({
          status: "Existing",
          membership_id: target,
          role: "administrator",
          membership_active: true,
          revision: 2,
          operational_access_granted: false,
        });
        return;
      }
      role = body.operation === "role-change" ? String(body.role) : role;
      active = body.operation !== "revoke";
      revision++;
      await send({
        status: "Changed",
        membership_id: body.membership_id,
        role,
        membership_active: active,
        revision,
        operational_access_granted: false,
      });
      return;
    }
    if (path === "/cancel") {
      await route.fulfill({ status: 204 });
      return;
    }
    await send({}, 404);
  });
  return { writes, invitationWrites, otherRequests, reads: () => memberReads };
}

test("refresh shows mail acceptance separately from joining and makes no invitation command", async ({
  page,
}, testInfo) => {
  const f = await fixture(page, "delivery-refresh", true);
  await page.goto("/team");
  const list = page.getByRole("list", { name: "Company invitations", exact: true });
  await expect(list).toContainText("Email queued");
  await expect(list).toContainText("Awaiting acceptance");
  await expect(list).not.toContainText("Email submitted");
  await page.getByRole("button", { name: "Refresh invitations", exact: true }).click();
  await expect(list).toContainText("Email submitted");
  await expect(list).toContainText("Awaiting acceptance");
  await expect(list).toContainText("inbox delivery is unconfirmed");
  expect(f.invitationWrites).toHaveLength(0);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: testInfo.outputPath("invitation-delivery-mobile.png"),
    fullPage: true,
  });
});
test("retry timing and unavailable legacy delivery are explicit", async ({ page }) => {
  await fixture(page, "delivery-retrying", true);
  await page.goto("/team");
  const list = page.getByRole("list", { name: "Company invitations", exact: true });
  await expect(list).toContainText("Email retry scheduled");
  await expect(list).toContainText("2 attempts");
  await expect(list).toContainText("Next attempt");
  await expect(list).not.toContainText("Email submitted");
});
test("legacy delivery never appears submitted", async ({ page }) => {
  await fixture(page, "delivery-legacy", true);
  await page.goto("/team");
  const list = page.getByRole("list", { name: "Company invitations", exact: true });
  await expect(list).toContainText("Email status unavailable");
  await expect(list).not.toContainText("Email submitted");
});
async function roleReview(page: Page) {
  await page.getByRole("button", { name: "Change role for Ben Member", exact: true }).click();
  await page.getByLabel("New company role").click();
  await page.getByRole("option", { name: "Administrator", exact: true }).click();
}
test("branded team sign-in is separate from ERP authentication", async ({ page }) => {
  const f = await fixture(page, "signed-out");
  await page.goto("/team");
  await expect(page.getByRole("heading", { name: "Your company team" })).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Continue with Kuubu", exact: true }),
  ).toHaveAttribute("href", "/api/core/product-identity/team/start");
  expect(f.writes).toHaveLength(0);
  expect(f.otherRequests).toEqual([]);
});
test("owner reviews company, person and role before a confirmed write and fresh read", async ({
  page,
}) => {
  const f = await fixture(page);
  await page.goto("/team");
  await roleReview(page);
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("Acacia Trading · Ben Member");
  await expect(dialog.getByRole("button", { name: "Confirm change" })).toBeDisabled();
  expect(f.writes).toHaveLength(0);
  await dialog.getByRole("checkbox").check();
  await dialog
    .getByRole("button", { name: "Confirm change" })
    .evaluate((button: HTMLButtonElement) => {
      button.click();
      button.click();
    });
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole("row").filter({ hasText: "Ben Member" })).toContainText(
    "Administrator",
  );
  expect(f.writes).toHaveLength(1);
  expect(f.reads()).toBe(2);
  expect(f.otherRequests).toEqual([]);
  expect(Object.keys(f.writes[0]).sort()).toEqual(
    [
      "company_id",
      "link_id",
      "membership_id",
      "command_key",
      "expected_revision",
      "operation",
      "confirmed",
      "role",
    ].sort(),
  );
});
test("ambiguous retry retains the key and uses fresh members instead of historical receipt state", async ({
  page,
}) => {
  const f = await fixture(page, "ambiguous");
  await page.goto("/team");
  await roleReview(page);
  await page.getByRole("dialog").getByRole("checkbox").check();
  await page.getByRole("button", { name: "Confirm change" }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText("couldn’t confirm");
  await expect(page.getByLabel("New company role")).toBeDisabled();
  await page.getByRole("button", { name: "Retry reviewed request" }).click();
  await expect(page.getByRole("dialog")).not.toBeVisible();
  await expect(page.getByRole("row").filter({ hasText: "Ben Member" })).toContainText("Revoked");
  expect(f.writes).toHaveLength(2);
  expect(f.writes[0]).toEqual(f.writes[1]);
  expect(f.reads()).toBe(2);
});
test("administrators can only review revocation of ordinary members", async ({ page }) => {
  const f = await fixture(page, "admin");
  await page.goto("/team");
  await expect(
    page.getByRole("button", { name: "Revoke access for Ben Member", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: /Change role for/ })).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Revoke access for Alice Owner", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Revoke access for Cara Administrator", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Revoke access for Ben Member", exact: true }).click();
  await page.getByRole("dialog").getByRole("checkbox").check();
  await page.getByRole("button", { name: "Confirm change" }).click();
  await expect(page.getByRole("row").filter({ hasText: "Ben Member" })).toContainText("Revoked");
  expect(f.writes[0].operation).toBe("revoke");
  expect(Object.hasOwn(f.writes[0], "role")).toBe(false);
});
test("last owner denial remains a denial in the review", async ({ page }) => {
  const f = await fixture(page, "last-owner");
  await page.goto("/team");
  await page.getByRole("button", { name: "Revoke access for Alice Owner", exact: true }).click();
  await page.getByRole("dialog").getByRole("checkbox").check();
  await page.getByRole("button", { name: "Confirm change" }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText("another active owner");
  expect(f.writes).toHaveLength(1);
  expect(f.reads()).toBe(1);
});
test("denied membership cannot retain team actions", async ({ page }) => {
  const f = await fixture(page, "denied");
  await page.goto("/team");
  await expect(page.getByRole("alert")).toContainText("access or subscription has changed");
  await expect(page.getByRole("button", { name: /Change role for|Revoke access for/ })).toHaveCount(
    0,
  );
  expect(f.writes).toHaveLength(0);
});
test("member pagination appends the next scoped page", async ({ page }) => {
  const f = await fixture(page, "paging");
  await page.goto("/team");
  await expect(page.getByRole("row").filter({ hasText: "Cara Administrator" })).toHaveCount(0);
  await page.getByRole("button", { name: "Load more members" }).click();
  await expect(page.getByRole("row").filter({ hasText: "Cara Administrator" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Load more members" })).toHaveCount(0);
  expect(f.reads()).toBe(2);
});
test("unavailable boundary offers no broken sign-in or write controls", async ({ page }) => {
  const f = await fixture(page, "unavailable");
  await page.goto("/team");
  await expect(page.getByRole("heading", { name: "Team management is unavailable" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Continue with Kuubu", exact: true })).toHaveCount(0);
  expect(f.writes).toHaveLength(0);
});
test("mobile team page exposes member actions without horizontal scrolling", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await fixture(page);
  await page.goto("/team");
  await expect(page.getByRole("heading", { name: "Acacia Trading", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page
    .getByRole("list", { name: "Company team members" })
    .getByRole("listitem")
    .filter({ hasText: "Ben Member" })
    .scrollIntoViewIfNeeded();
  const button = page.getByRole("button", { name: "Change role for Ben Member", exact: true });
  await expect(button).toBeInViewport();
  await button.click();
  await expect(page.getByRole("dialog")).toBeVisible();
});
