import { test, expect, type Page } from "@playwright/test";
const prefix = "/api/core/product-identity/operational";
const csrf = "c".repeat(43);
const company = {
  company_id: "11111111-1111-1111-1111-111111111111",
  link_id: "22222222-2222-2222-2222-222222222222",
  name: "Copperbelt Trading",
  role: "owner",
};
const alice = {
  membership_id: "33333333-3333-3333-3333-333333333333",
  display_name: "Alice Banda",
  role: "member",
  membership_active: true,
  identity_available: true,
  revision: 1,
};
const bob = {
  ...alice,
  membership_id: "77777777-7777-7777-7777-777777777777",
  display_name: "Bob Phiri",
};
const branch = {
  location_id: "44444444-4444-4444-4444-444444444444",
  name: "Lusaka HQ",
  available: true,
};
const branch2 = {
  location_id: "88888888-8888-8888-8888-888888888888",
  name: "Kitwe branch",
  available: true,
};
type Grant = {
  version_id: string;
  revision: number;
  membership_revision: number;
  state: string;
  roles: string[];
  actions: string[];
  scope: string;
  locations: (typeof branch)[];
  requires_reconfirmation: boolean;
};
async function fixture(page: Page, mode = "ready") {
  const posts: Record<string, unknown>[] = [];
  const calls: string[] = [];
  let signedOut = false;
  let grantModule = "hrm";
  let release: (() => void) | undefined;
  let grant: Grant | null = ["existing", "stale", "inactive"].includes(mode)
    ? {
        version_id: "55555555-5555-5555-5555-555555555555",
        revision: 1,
        membership_revision: 1,
        state: "granted",
        roles: ["hr_ops"],
        actions: ["hrm-access"],
        scope: "company",
        locations: [],
        requires_reconfirmation: mode === "stale" || mode === "inactive",
      }
    : null;
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    calls.push(url.pathname);
    const send = (body: unknown, status = 200) => route.fulfill({ status, json: body });
    if (url.pathname === prefix + "/bootstrap")
      return send({ csrf_token: csrf, operational_access_granted: false });
    if (url.pathname === prefix + "/session")
      return signedOut
        ? send({}, 401)
        : send({
            verified: true,
            purpose: "erp-session-v1",
            product_code: "kuubu",
            csrf_token: csrf,
            operational_access_granted: false,
            next_step: "company_selection",
            proof_until: new Date(Date.now() + 30000).toISOString(),
            company_discovery_available: true,
          });
    if (url.pathname === prefix + "/companies")
      return send({ companies: [company], next_cursor: null, operational_access_granted: false });
    if (url.pathname === prefix + "/companies/" + company.company_id)
      return send({
        company,
        licensed_modules: ["hrm", "procurement"],
        operational_access_granted: false,
        next_step: "module_access_assignment",
      });
    if (url.pathname === prefix + "/logout") {
      signedOut = true;
      return route.fulfill({ status: 204 });
    }
    if (url.pathname.startsWith(prefix + "/module-access/")) {
      if (mode === "disabled") return send({}, 503);
      if (url.pathname.endsWith("/commands")) {
        const body = route.request().postDataJSON();
        posts.push(body);
        expect(route.request().headers()["x-kuubu-operational-csrf"]).toBe(csrf);
        expect(Object.keys(body)).toHaveLength(14);
        expect(body.confirmed).toBe(true);
        expect(body.company_id).toBe(company.company_id);
        expect(body.link_id).toBe(company.link_id);
        if (mode === "conflict") return send({}, 409);
        const replay = posts.length > 1 && posts[0].command_key === body.command_key;
        if (!replay) {
          grantModule = body.module;
          grant = {
            version_id: "55555555-5555-5555-5555-555555555555",
            revision: body.expected_revision + 1,
            membership_revision: body.membership_revision,
            state: body.operation === "revoke" ? "revoked" : "granted",
            roles: body.roles,
            actions: body.actions,
            scope: body.scope,
            locations: body.location_ids.map((id: string) =>
              id === branch.location_id ? branch : branch2,
            ),
            requires_reconfirmation: false,
          };
        }
        if (mode === "uncertain" && posts.length === 1) return send({}, 503);
        return send({
          status: replay ? "Existing" : "Changed",
          version_id: grant!.version_id,
          revision: grant!.revision,
          state: grant!.state,
          operational_access_granted: false,
        });
      }
      expect(url.searchParams.get("company_id")).toBe(company.company_id);
      expect(url.searchParams.get("link_id")).toBe(company.link_id);
      if (url.pathname.endsWith("/members"))
        return send({
          members: url.searchParams.has("after")
            ? [bob]
            : [alice, ...(mode === "paging" ? [] : [bob])],
          next_cursor:
            mode === "paging" && !url.searchParams.has("after") ? alice.membership_id : null,
          operational_access_granted: false,
        });
      if (url.pathname.endsWith("/locations"))
        return send({
          locations: url.searchParams.has("after") ? [branch2] : [branch],
          next_cursor:
            mode === "paging" && !url.searchParams.has("after") ? branch.location_id : null,
          operational_access_granted: false,
        });
      if (url.pathname.endsWith("/review")) {
        if (mode === "expired") return send({}, 401);
        const member = url.searchParams.get("membership_id") === alice.membership_id ? alice : bob;
        if (mode === "late" && member === alice)
          await new Promise<void>((resolve) => {
            release = resolve;
          });
        return send({
          member: {
            ...member,
            membership_active: mode !== "inactive",
            revision: mode === "stale" ? 2 : 1,
          },
          module: url.searchParams.get("module"),
          roles:
            url.searchParams.get("module") === "procurement"
              ? ["procurement_manager"]
              : ["hr_ops", "hr_admin"],
          actions:
            url.searchParams.get("module") === "procurement"
              ? ["procurement-access", "procurement-vendor-read"]
              : ["hrm-access", "hrm-admin"],
          grant: member === alice && grantModule === url.searchParams.get("module") ? grant : null,
          operational_access_granted: false,
        });
      }
    }
    throw new Error("Unexpected API request: " + url.pathname);
  });
  return { posts, calls, release: () => release?.() };
}
async function open(page: Page) {
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  const panel = page.getByRole("region", { name: "Module permissions" });
  await panel.getByRole("combobox", { name: "Team member" }).click();
  await page.getByRole("option", { name: "Alice Banda", exact: true }).click();
  await expect(panel.getByRole("heading", { name: "Alice Banda · Human resources" })).toBeVisible();
  return panel;
}
async function choose(page: Page) {
  const panel = await open(page);
  await panel.getByRole("checkbox", { name: "HR operations", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Use HR and payroll", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Lusaka HQ", exact: true }).check();
  return panel;
}
test("explicit review names the selected branches and records no automatic ERP admission", async ({
  page,
}) => {
  const f = await fixture(page);
  const panel = await choose(page);
  expect(f.posts).toHaveLength(0);
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toContainText("Alice Banda");
  await expect(dialog).toContainText("Lusaka HQ");
  expect(f.posts).toHaveLength(0);
  await dialog.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("Permission decision recorded");
  expect(f.posts).toHaveLength(1);
  expect(f.posts[0].location_ids).toEqual([branch.location_id]);
  expect(f.posts[0].all_company_locations_confirmed).toBe(false);
  expect(f.calls.every((path) => path.startsWith(prefix))).toBeTruthy();
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
});
test("company-wide access needs a separate unchecked confirmation after every review", async ({
  page,
}) => {
  const f = await fixture(page);
  const panel = await open(page);
  await panel.getByRole("checkbox", { name: "HR operations", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Use HR and payroll", exact: true }).check();
  await panel.getByRole("combobox", { name: "Branch access", exact: true }).click();
  await page.getByRole("option", { name: "All company branches", exact: true }).click();
  await expect(
    panel.getByRole("button", { name: "Review permissions", exact: true }),
  ).toBeDisabled();
  const confirmation = panel.getByRole("checkbox", {
    name: "Allow access to all current and future branches in this company",
  });
  await expect(confirmation).not.toBeChecked();
  await confirmation.check();
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await page.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("Permission decision recorded");
  await expect(confirmation).not.toBeChecked();
  expect(f.posts[0].scope).toBe("company");
  expect(f.posts[0].location_ids).toEqual([]);
});
test("uncertain completed save retries the same reviewed payload and key", async ({ page }) => {
  const f = await fixture(page, "uncertain");
  const panel = await choose(page);
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await page.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("couldn’t confirm whether the save finished");
  expect(f.posts).toHaveLength(1);
  await expect(panel.getByRole("combobox", { name: "Team member" })).toBeDisabled();
  await panel.getByRole("button", { name: "Retry reviewed change" }).click();
  await expect(panel).toContainText("Permission decision recorded");
  expect(f.posts).toHaveLength(2);
  expect(f.posts[0]).toEqual(f.posts[1]);
});
test("switching modules clears the HR draft and confirms only Procurement actions", async ({
  page,
}) => {
  const f = await fixture(page);
  const panel = await choose(page);
  await panel.getByRole("combobox", { name: "Module", exact: true }).click();
  await page.getByRole("option", { name: "Procurement", exact: true }).click();
  await expect(panel.getByRole("heading", { name: "Alice Banda · Procurement" })).toBeVisible();
  await expect(
    panel.getByRole("checkbox", { name: "Use HR and payroll", exact: true }),
  ).toHaveCount(0);
  await panel.getByRole("checkbox", { name: "Procurement Manager", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Use Procurement", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Vendor Read", exact: true }).check();
  await panel.getByRole("checkbox", { name: "Lusaka HQ", exact: true }).check();
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await page.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("Permission decision recorded");
  expect(f.posts[0].module).toBe("procurement");
  expect(f.posts[0].actions).toEqual(["procurement-access", "procurement-vendor-read"]);
});
test("revocation has its own review and clears the action and branch snapshot", async ({
  page,
}) => {
  const f = await fixture(page, "existing");
  const panel = await open(page);
  await panel.getByRole("button", { name: "Revoke module permissions" }).click();
  await expect(page.getByRole("alertdialog")).toContainText(
    "Remove this member’s module permissions",
  );
  await page.getByRole("button", { name: "Confirm revocation", exact: true }).click();
  await expect(panel).toContainText("Module permissions were revoked");
  expect(f.posts[0].operation).toBe("revoke");
  expect(f.posts[0].roles).toEqual([]);
  expect(f.posts[0].actions).toEqual([]);
  expect(f.posts[0].scope).toBe("none");
});
test("stale writes require a fresh review instead of automatic replay", async ({ page }) => {
  const f = await fixture(page, "conflict");
  const panel = await choose(page);
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await page.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("Permissions or company membership changed");
  await expect(
    panel.getByRole("button", { name: "Reload permissions", exact: true }),
  ).toBeVisible();
  expect(f.posts).toHaveLength(1);
});
test("stale membership cannot silently restore old role and scope choices", async ({ page }) => {
  await fixture(page, "stale");
  const panel = await open(page);
  await expect(panel).toContainText("Membership or branch access changed");
  await expect(
    panel.getByRole("checkbox", { name: "HR operations", exact: true }),
  ).not.toBeChecked();
  await expect(
    panel.getByRole("button", { name: "Review permissions", exact: true }),
  ).toBeDisabled();
});
test("inactive members can have old permissions revoked but cannot receive new grants", async ({
  page,
}) => {
  await fixture(page, "inactive");
  const panel = await open(page);
  await expect(panel).toContainText("not currently available");
  await expect(
    panel.getByRole("button", { name: "Review permissions", exact: true }),
  ).toBeDisabled();
  await expect(panel.getByRole("button", { name: "Revoke module permissions" })).toBeEnabled();
});
test("member and branch paging preserve the selected branch set", async ({ page }) => {
  const f = await fixture(page, "paging");
  const panel = await choose(page);
  await panel.getByRole("button", { name: "More branches", exact: true }).click();
  await panel.getByRole("checkbox", { name: "Kitwe branch", exact: true }).check();
  await expect(panel.getByRole("checkbox", { name: "Lusaka HQ", exact: true })).toBeChecked();
  await panel.getByRole("button", { name: "More team members" }).click();
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await expect(page.getByRole("alertdialog")).toContainText("Kitwe branch");
  await page.getByRole("button", { name: "Confirm permissions", exact: true }).click();
  await expect(panel).toContainText("Permission decision recorded");
  expect(f.posts[0].location_ids).toEqual([branch.location_id, branch2.location_id]);
});
test("a late member response cannot replace a new member selection", async ({ page }) => {
  const f = await fixture(page, "late");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  const panel = page.getByRole("region", { name: "Module permissions" });
  await panel.getByRole("combobox", { name: "Team member" }).click();
  await page.getByRole("option", { name: "Alice Banda", exact: true }).click();
  await expect(panel).toContainText("Reviewing current permissions");
  await panel.getByRole("combobox", { name: "Team member" }).click();
  await page.getByRole("option", { name: "Bob Phiri", exact: true }).click();
  await expect(panel.getByRole("heading", { name: "Bob Phiri · Human resources" })).toBeVisible();
  f.release();
  await expect(panel.getByRole("heading", { name: "Alice Banda · Human resources" })).toHaveCount(
    0,
  );
});
test("expired identity returns to Kuubu sign-in and clears the editor", async ({ page }) => {
  await fixture(page, "expired");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await page.getByRole("combobox", { name: "Team member" }).click();
  await page.getByRole("option", { name: "Alice Banda", exact: true }).click();
  await expect(page.getByRole("button", { name: "Continue with Kuubu" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Module permissions" })).toHaveCount(0);
});
test("disabled API offers recovery without a working grant form", async ({ page }) => {
  const f = await fixture(page, "disabled");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  const panel = page.getByRole("region", { name: "Module permissions" });
  await expect(panel).toContainText("temporarily unavailable");
  await expect(panel.getByRole("button", { name: "Review permissions", exact: true })).toHaveCount(
    0,
  );
  expect(f.posts).toHaveLength(0);
});
test("mobile editor fits the viewport and sign-out removes permission data", async ({
  page,
}, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await fixture(page);
  const panel = await choose(page);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: info.outputPath("permissions-mobile.png"), fullPage: true });
  await panel.getByRole("button", { name: "Review permissions", exact: true }).click();
  await expect(page.getByRole("alertdialog")).toContainText("Lusaka HQ");
  await page.screenshot({ path: info.outputPath("confirmation-mobile.png"), fullPage: true });
  await page.getByRole("button", { name: "Back", exact: true }).click();
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Continue with Kuubu" })).toBeVisible();
  await expect(panel).toHaveCount(0);
});
