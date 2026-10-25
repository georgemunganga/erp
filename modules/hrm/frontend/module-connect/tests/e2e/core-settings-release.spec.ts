import { expect, test, type Page } from "@playwright/test";

const admin = { id: "00000000-0000-0000-0000-000000000001", email: "core.admin@example.test", displayName: "Core Admin",
  roles: ["erp_admin"], isActive: true, mustChangePassword: false };

async function mockCore(page: Page, user: typeof admin | null) {
  const unexpected: string[] = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() !== "GET") {
      unexpected.push(`${request.method()} ${path}`);
      await route.fulfill({ status: 405, json: { message: "Read-only route test" } });
      return;
    }
    const json: unknown = path === "/api/hrm/auth/capabilities"
      ? { mode: "local", localUsersEnabled: true, identityConfigured: false }
      : path === "/api/hrm/auth/me" ? { authenticated: Boolean(user), user }
      : path === "/api/hrm/branding" || path === "/api/hrm/admin/branding"
        ? { companyName: "Example Company", displayName: "ERP workspace" }
      : path === "/api/hrm/shell" ? { confined: false, assignedLocationIds: [], scopedToBranch: false }
      : path === "/api/hrm/admin/locations" ? { items: [] }
      : path === "/api/hrm/workers" ? { items: [], totalCount: 0 }
      : path === "/api/hrm/admin/branch-access" ? { items: [], locations: [] }
      : path === "/api/hrm/auth/users" ? { items: [] }
      : path === "/api/hrm/me" ? { linked: false, worker: null }
      : path === "/api/core/v1/role-permissions"
        ? { schemaVersion: 1, procurement: [{ key: "procurement-access", label: "Procurement access", group: "General" }] }
      : path === "/api/hrm/admin/roles" || path === "/api/hrm/admin/legal-entities" || path === "/api/hrm/admin/org-units"
        ? []
      : null;
    if (json === null) unexpected.push(`GET ${path}`);
    await route.fulfill({ status: json === null ? 404 : 200, json: json ?? { message: "Unexpected route" } });
  });
  return unexpected;
}

test("workerless Core administrator reaches each connected settings editor", async ({ page }) => {
  const unexpected = await mockCore(page, admin);
  for (const [path, heading] of [
    ["/core/workspace/settings", "Core settings"],
    ["/core/workspace/settings/branding", "Company branding"],
    ["/core/workspace/settings/users", "HRM user access"],
    ["/core/workspace/settings/organization", "Organisation setup"],
    ["/core/workspace/settings/branch-access", "Branch access"],
    ["/core/workspace/settings/roles", "Roles and permissions"],
  ] as const) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: heading, exact: true }).first()).toBeVisible();
    await expect(page.getByText("HRM access not assigned")).toHaveCount(0);
  }
  expect(unexpected).toEqual([]);
});

test("legacy Core links reach the same settings pages", async ({ page }) => {
  const unexpected = await mockCore(page, admin);
  for (const [oldPath, destination] of [
    ["/core", "/core/workspace/settings"],
    ["/core/branding", "/core/workspace/settings/branding"],
    ["/core/roles", "/core/workspace/settings/roles"],
    ["/core/users", "/core/workspace/settings/users"],
    ["/core/organization", "/core/workspace/settings/organization"],
    ["/core/branch-access", "/core/workspace/settings/branch-access"],
  ] as const) {
    await page.goto(oldPath);
    await expect(page).toHaveURL(new RegExp(`${destination.replaceAll("/", "\\/")}/?$`));
  }
  expect(unexpected).toEqual([]);
});

test("nonadministrator cannot open Core settings or role controls", async ({ page }) => {
  const unexpected = await mockCore(page, { ...admin, roles: ["custom_procurement_role"] });
  await page.goto("/core/workspace/settings/roles");
  await expect(page.getByRole("heading", { name: "Core settings access required" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Create role" })).toHaveCount(0);
  expect(unexpected).toEqual([]);
});

test("anonymous settings request returns to sign-in", async ({ page }) => {
  const unexpected = await mockCore(page, null);
  await page.goto("/core/workspace/settings/roles");
  await expect(page).toHaveURL(/\/sign-in\?/);
  await expect(page.getByRole("heading", { name: "Roles and permissions" })).toHaveCount(0);
  expect(unexpected).toEqual([]);
});
