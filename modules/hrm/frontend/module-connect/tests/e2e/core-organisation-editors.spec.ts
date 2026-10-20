import { expect, test, type Page } from "@playwright/test";

// Presentation/HTTP-contract scenarios only; run against real-API-mode UI. Backend authorization has separate service/HTTP gates.
async function configure(page: Page, options: { uncertain?: boolean; deny?: boolean; pending?: Promise<void>; secondCompany?: boolean } = {}) {
  const writes: Array<{ path: string; body: Record<string, unknown> }> = [];
  const workforce: string[] = [];
  const companyReads: string[] = [];
  const unexpected: string[] = [];
  let company = { id: "company-owned", code: "CORE", registeredName: "Owned Company", tradingName: "Trading", currency: "ZMW", countryCode: "ZM", createdAt: new Date().toISOString(), isDefault: true, tpin: "retained" };
  let department = { id: "department-owned", code: "OPS", name: "Operations", legalEntityId: company.id, costCentreRef: "CC1", status: "active", effectiveFrom: new Date().toISOString().slice(0, 10), parentId: "retained-parent" };
  await page.route("**/api/hrm/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/hrm/workers") workforce.push(path);
    if (request.method() === "PATCH" || request.method() === "POST") {
      const body = request.postDataJSON() as Record<string, unknown>;
      writes.push({ path, body });
      if (options.uncertain) { await route.abort("failed"); return; }
      if (options.pending) await options.pending;
      if (options.deny) { await route.fulfill({ status: 403, json: { code: "forbidden", message: "Access denied." } }); return; }
      if (path === `/api/hrm/admin/legal-entities/${company.id}`) { company = { ...company, ...body }; await route.fulfill({ json: company }); return; }
      if (path === `/api/hrm/admin/org-units/${department.id}`) { department = { ...department, ...body }; await route.fulfill({ json: department }); return; }
      if (path === "/api/hrm/admin/org-units") { department = { ...department, ...body, id: "department-created" }; await route.fulfill({ status: 201, json: department }); return; }
      if (path === "/api/hrm/admin/locations") { await route.fulfill({ status: 201, json: { ...body, id: "location-created" } }); return; }
      unexpected.push(`${request.method()} ${path}`);
      await route.fulfill({ status: 404, json: { message: "Unexpected operation" } }); return;
    }
    if (path === "/api/hrm/admin/legal-entities") companyReads.push(path);
    const body = path.endsWith("/auth/capabilities") ? { mode: "local", localUsersEnabled: true, identityConfigured: false }
      : path.endsWith("/auth/me") ? { authenticated: true, user: { id: "core-admin", email: "core@example.test", displayName: "Core Admin", roles: ["erp_admin"], isActive: true } }
      : path.endsWith("/branding") ? {}
      : path === "/api/hrm/me" ? { linked: false, worker: null }
      : path === "/api/hrm/shell" ? { scopedToBranch: false, assignedLocationIds: [], confined: false }
      : path === "/api/hrm/admin/legal-entities" ? { items: [company, ...(options.secondCompany ? [{ ...company, id: "company-remote", registeredName: "Remote Company", code: "REMOTE" }] : [])], totalCount: options.secondCompany ? 2 : 1 }
      : path === "/api/hrm/admin/org-units" ? [department]
      : path === "/api/hrm/admin/locations" ? { items: [], totalCount: 0 } : null;
    if (body === null) { unexpected.push(path); await route.fulfill({ status: 403, json: { message: "Denied" } }); return; }
    await route.fulfill({ json: body });
  });
  await page.goto("/core/workspace/settings/organization");
  await expect(page.getByRole("heading", { name: "Organisation setup", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit Owned Company", exact: true })).toBeVisible();
  return { writes, workforce, unexpected, companyReads };
}

test("ERP-only admin edits company currency and department metadata without workforce or hidden field round-trip", async ({ page }) => {
  const observed = await configure(page);
  await page.getByRole("button", { name: "Edit Owned Company", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Code", { exact: true })).toBeDisabled();
  await dialog.getByLabel("Default currency").fill("USD");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(observed.writes[0]).toEqual({ path: "/api/hrm/admin/legal-entities/company-owned", body: { registeredName: "Owned Company", tradingName: "Trading", currency: "USD" } });
  await page.getByRole("button", { name: "Edit Operations", exact: true }).click();
  await page.getByRole("dialog").getByLabel("Department name").fill("Shared Operations");
  await page.getByRole("dialog").getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(observed.writes[1]).toEqual({ path: "/api/hrm/admin/org-units/department-owned", body: { name: "Shared Operations", costCentreRef: "CC1" } });
  expect(observed.workforce).toEqual([]);
  expect(observed.unexpected).toEqual([]);
});

test("unconfirmed create disables retries and reconciles by refresh without a second POST", async ({ page }) => {
  const observed = await configure(page, { uncertain: true });
  await page.getByRole("button", { name: "Add department", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Code", { exact: true }).fill("NEW");
  await dialog.getByLabel("Department name").fill("New Department");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Save could not be confirmed");
  await expect(dialog.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
  expect(observed.writes).toHaveLength(1);
  await dialog.getByRole("button", { name: "Close and refresh", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(observed.writes).toHaveLength(1);
  expect(observed.workforce).toEqual([]);
});

test("server permission denial preserves form input and does not announce a saved record", async ({ page }) => {
  const observed = await configure(page, { deny: true });
  await page.getByRole("button", { name: "Edit Owned Company", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Default currency").fill("EUR");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Access denied");
  await expect(dialog.getByLabel("Default currency")).toHaveValue("EUR");
  expect(observed.writes).toHaveLength(1);
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(dialog).toHaveCount(0);
});


test("Escape reconciles an uncertain save instead of bypassing refresh", async ({ page }) => {
  const observed = await configure(page, { uncertain: true });
  await page.getByRole("button", { name: "Edit Owned Company", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Default currency").fill("USD");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Save could not be confirmed");
  const reads = observed.companyReads.length;
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
  await expect.poll(() => observed.companyReads.length).toBeGreaterThan(reads);
  expect(observed.writes).toHaveLength(1);
});

test("existing shell company switch unmounts a pending editor and ignores its late success", async ({ page }) => {
  let release!: () => void;
  const pending = new Promise<void>((resolve) => { release = resolve; });
  const observed = await configure(page, { pending, secondCompany: true });
  await page.getByRole("button", { name: "Edit Owned Company", exact: true }).click();
  await page.getByRole("dialog").getByLabel("Default currency").fill("USD");
  await page.getByRole("dialog").getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => observed.writes.length).toBe(1);
  // Exercise the real shell context callbacks while a modal command is pending (e.g. an external shell event).
  // Programmatic click avoids pretending the modal's normally inert background is pointer-accessible.
  await page.locator('button[aria-label="Organization and branch"]').evaluate((button) => button.click());
  await page.getByRole("button", { name: "Remote Company", exact: true, includeHidden: true }).evaluate((button) => button.click());
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Edit Owned Company", exact: true })).toBeVisible();
  const reads = observed.companyReads.length;
  const completed = page.waitForResponse((response) => response.request().method() === "PATCH" && response.url().endsWith("/legal-entities/company-owned"));
  release();
  await completed;
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(page.getByText("Organization settings saved.", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(observed.companyReads).toHaveLength(reads);
  expect(observed.writes).toHaveLength(1);
});


test("late location creation cannot close a new shell context location form", async ({ page }) => {
  let release!: () => void;
  const pending = new Promise<void>((resolve) => { release = resolve; });
  const observed = await configure(page, { pending, secondCompany: true });
  await page.getByRole("button", { name: "Add work location", exact: true }).click();
  await page.getByRole("dialog").getByLabel("Location name", { exact: true }).fill("Old context location");
  await page.getByRole("dialog").getByLabel("Code", { exact: true }).fill("OLD");
  await page.getByRole("dialog").getByRole("button", { name: "Create location", exact: true }).click();
  await expect.poll(() => observed.writes.length).toBe(1);
  await page.locator('button[aria-label="Organization and branch"]').evaluate((button) => button.click());
  await page.getByRole("button", { name: "Remote Company", exact: true, includeHidden: true }).evaluate((button) => button.click());
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.getByRole("button", { name: "Add work location", exact: true }).click();
  await page.getByRole("dialog").getByLabel("Location name", { exact: true }).fill("Keep new context input");
  const completed = page.waitForResponse((response) => response.request().method() === "POST" && response.url().endsWith("/admin/locations"));
  release();
  await completed;
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(page.getByRole("dialog").getByLabel("Location name", { exact: true })).toHaveValue("Keep new context input");
  await expect(page.getByText("Location created. It is now available for placement.", { exact: true })).toHaveCount(0);
  expect(observed.writes).toHaveLength(1);
});
