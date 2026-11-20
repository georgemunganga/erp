import { test, expect, type Page } from "@playwright/test";
const prefix = "/api/core/product-identity/operational";
const a = {
  company_id: "11111111-1111-1111-1111-111111111111",
  link_id: "22222222-2222-2222-2222-222222222222",
  name: "Copperbelt Trading",
  role: "member",
};
const b = {
  company_id: "55555555-5555-5555-5555-555555555555",
  link_id: "66666666-6666-6666-6666-666666666666",
  name: "Lusaka Services",
  role: "member",
};
const version = "33333333-3333-3333-3333-333333333333";
const location = "44444444-4444-4444-4444-444444444444";
async function fixture(page: Page, mode = "finite") {
  const commands: string[] = [];
  const calls: string[] = [];
  let current = a.company_id;
  let logouts = 0;
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    calls.push(url.pathname);
    const send = (value: unknown, status = 200) => route.fulfill({ json: value, status });
    if (url.pathname === prefix + "/bootstrap")
      return send({ csrf_token: "c".repeat(43), operational_access_granted: false });
    if (url.pathname === prefix + "/session")
      return send({
        verified: true,
        purpose: "erp-session-v1",
        product_code: "kuubu",
        csrf_token: "c".repeat(43),
        operational_access_granted: false,
        next_step: "company_selection",
        proof_until: new Date(Date.now() + 30000).toISOString(),
        company_discovery_available: true,
      });
    if (url.pathname === prefix + "/companies")
      return send({ companies: [a, b], next_cursor: null, operational_access_granted: false });
    if (url.pathname.startsWith(prefix + "/companies/")) {
      const company = url.pathname.endsWith(a.company_id) ? a : b;
      return send({
        company,
        licensed_modules: ["hrm"],
        operational_access_granted: false,
        next_step: "module_access_assignment",
      });
    }
    if (url.pathname === prefix + "/erp-session/review") {
      if (mode === "denied") return send({}, 403);
      return send({
        company_id: url.searchParams.get("company_id"),
        module: "hrm",
        grant_version_id: version,
        grant_revision: 1,
        scope: mode === "company" ? "company" : "locations",
        location_ids: mode === "company" ? [] : [location],
        roles: ["hr_ops"],
        actions: ["hrm-access"],
        session_established: false,
        operational_access_granted: false,
      });
    }
    if (url.pathname === prefix + "/erp-session/select") {
      commands.push(route.request().postData()!);
      expect(route.request().headers()["x-kuubu-operational-csrf"]).toBe("c".repeat(43));
      expect(route.request().headers()).not.toHaveProperty("authorization");
      expect(route.request().headers()).not.toHaveProperty("hrm-default-tenantid");
      if (mode === "lost" && commands.length === 1) return route.abort("failed");
      if (mode === "revoked") return send({}, 403);
      current = JSON.parse(commands.at(-1)!).company_id;
      return send({
        session_established: true,
        company_id: current,
        module: "hrm",
        expires_at: new Date(Date.now() + 3600000).toISOString(),
        operational_access_granted: false,
        next_step: "erp_access_pending",
      });
    }
    if (url.pathname === prefix + "/erp-session/session")
      return send({
        session_established: true,
        company_id: mode === "mismatch" ? b.company_id : current,
        module: "hrm",
        scope: mode === "company" ? "company" : "locations",
        location_ids: mode === "company" ? [] : [location],
        expires_at: new Date(Date.now() + 3600000).toISOString(),
        operational_access_granted: false,
        next_step: "erp_access_pending",
      });
    if (url.pathname === prefix + "/erp-session/logout") {
      logouts++;
      if (mode === "logout-lost" && logouts === 1) return send({}, 503);
      return route.fulfill({ status: 204 });
    }
    throw new Error("Unexpected request " + url.pathname);
  });
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  return { commands, calls };
}
test("ordinary member explicitly reviews and selects finite scope without owner privileges", async ({
  page,
}) => {
  const f = await fixture(page);
  expect(f.calls.some((path) => path.includes("/erp-session/"))).toBe(false);
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  const session = page.getByRole("region", { name: "Your ERP session" });
  await expect(session).toContainText("1 assigned location");
  expect(f.commands).toHaveLength(0);
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(session.getByRole("heading", { name: "Company session selected" })).toBeVisible();
  expect(f.commands).toHaveLength(1);
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
  await page.getByRole("button", { name: "End company session" }).click();
  await expect(session.getByRole("heading", { name: "Company session selected" })).toHaveCount(0);
});
test("company-wide access requires its own confirmation before selecting", async ({ page }) => {
  const f = await fixture(page, "company");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await expect(page.getByRole("button", { name: "Select company session" })).toBeDisabled();
  await page.getByRole("checkbox", { name: "I confirm access to all company locations." }).check();
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(page.getByRole("heading", { name: "Company session selected" })).toBeVisible();
  expect(JSON.parse(f.commands[0]).all_company_locations_confirmed).toBe(true);
});
test("lost result requires explicit exact retry and never creates a fresh command", async ({
  page,
}) => {
  const f = await fixture(page, "lost");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(page.getByRole("button", { name: "Retry same selection" })).toBeVisible();
  expect(f.commands).toHaveLength(1);
  await page.getByRole("button", { name: "Retry same selection" }).click();
  await expect(page.getByRole("heading", { name: "Company session selected" })).toBeVisible();
  expect(f.commands).toHaveLength(2);
  expect(f.commands[0]).toBe(f.commands[1]);
});
test("revoked authority clears review rather than presenting a selected session", async ({
  page,
}) => {
  await fixture(page, "revoked");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(page.getByRole("alert")).toContainText("permissions or subscription");
  await expect(page.getByRole("button", { name: "Select company session" })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Company session selected" })).toHaveCount(0);
});
test("foreign current session cannot be shown as successful selection", async ({ page }) => {
  await fixture(page, "mismatch");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(page.getByRole("alert")).toContainText("access changed");
  await expect(page.getByRole("heading", { name: "Company session selected" })).toHaveCount(0);
});
test("switching company removes the previous review and requires another explicit self-review", async ({
  page,
}) => {
  const f = await fixture(page);
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await expect(page.getByRole("button", { name: "Select company session" })).toBeVisible();
  await page.getByRole("button", { name: "Review Lusaka Services" }).click();
  await expect(page.getByRole("button", { name: "Select company session" })).toHaveCount(0);
  expect(f.commands).toHaveLength(0);
});
test("module denial cannot become membership-derived operational access", async ({ page }) => {
  const f = await fixture(page, "denied");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await expect(page.getByRole("alert")).toContainText("do not currently have access");
  await expect(page.getByRole("button", { name: "Select company session" })).toHaveCount(0);
  expect(f.commands).toHaveLength(0);
});
test("unconfirmed logout clears the selected display and offers explicit logout recovery", async ({
  page,
}) => {
  await fixture(page, "logout-lost");
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await page.getByRole("button", { name: "Select company session" }).click();
  await expect(page.getByRole("heading", { name: "Company session selected" })).toBeVisible();
  await page.getByRole("button", { name: "End company session", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "couldn’t confirm that the company session ended",
  );
  await expect(page.getByRole("heading", { name: "Company session selected" })).toHaveCount(0);
  await page.getByRole("button", { name: "Retry ending company session" }).click();
  await expect(page.getByRole("button", { name: "Retry ending company session" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Review Human resources access" })).toBeEnabled();
});
