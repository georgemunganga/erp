import { expect, test, type Page } from "@playwright/test";
import { permissionPatch, readProcurementRoleCatalog } from "../../src/platform/role-permission-catalog";

const procurement = [
  { key: "procurement-access", group: "Access", label: "Procurement access" },
  { key: "procurement-payment-record", group: "Payments", label: "Record manual payment" },
  { key: "procurement-po-edit", group: "Purchase orders", label: "Edit draft purchase order" },
  { key: "procurement-sourcing-publish", group: "Sourcing", label: "Publish sourcing round" },
];

type Role = { id: string; roleKey: string; roleName: string; category: string; active: boolean; permissions: string[];
  effectiveProcurementPermissions?: string[] };

async function mockRoles(page: Page, initial: Role[], catalog: unknown = { schemaVersion: 1, procurement }) {
  let roles = initial;
  const writes: { method: string; path: string; body: Record<string, unknown> }[] = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    if (path === "/api/core/v1/role-permissions") {
      await route.fulfill({ status: catalog === null ? 503 : 200, json: catalog ?? {} });
      return;
    }
    if (path === "/api/hrm/admin/roles" && method === "GET") {
      await route.fulfill({ json: roles });
      return;
    }
    if (path === "/api/hrm/admin/roles" && method === "POST") {
      const body = request.postDataJSON() as Record<string, unknown>;
      writes.push({ method, path, body });
      const next: Role = { id: "created-role", roleKey: String(body.roleKey), roleName: String(body.roleName),
        category: String(body.category), active: Boolean(body.active), permissions: body.permissions as string[] };
      roles = [...roles, next];
      await route.fulfill({ status: 201, json: next });
      return;
    }
    if (path.startsWith("/api/hrm/admin/roles/") && method === "PATCH") {
      const body = request.postDataJSON() as Record<string, unknown>;
      writes.push({ method, path, body });
      roles = roles.map((role) => path.endsWith(`/${role.roleKey}`) ? { ...role, ...body } as Role : role);
      await route.fulfill({ json: roles.find((role) => path.endsWith(`/${role.roleKey}`)) });
      return;
    }
    if (path.endsWith("/auth/capabilities")) {
      await route.fulfill({ json: { mode: "local", localUsersEnabled: true, identityConfigured: false } });
      return;
    }
    if (path.endsWith("/auth/me")) {
      await route.fulfill({ json: { authenticated: true, user: { id: "role-admin", displayName: "Role Admin", roles: ["hr_admin"], isActive: true } } });
      return;
    }
    await route.fulfill({ json: [] });
  });
  return writes;
}

test("catalog rejects missing, duplicate and malformed actions; metadata edits retain grants", () => {
  expect(() => readProcurementRoleCatalog({ schemaVersion: 1, procurement: [] })).toThrow();
  expect(() => readProcurementRoleCatalog({ schemaVersion: 1, procurement: [...procurement, procurement[0]] })).toThrow();
  expect(() => readProcurementRoleCatalog({ schemaVersion: 1, procurement: [{ key: "hr_admin", label: "Admin", group: "Other" }] })).toThrow();
  const original = { roleName: "Buyer", category: "procurement", active: true, permissions: ["procurement-payment-record"] };
  expect(permissionPatch(original, { ...original, roleName: "Senior buyer" })).toEqual({ roleName: "Senior buyer" });
  expect(permissionPatch(original, { ...original, active: false })).toEqual({ active: false });
  expect(permissionPatch(original, { ...original, category: "hrm", permissions: [] })).toEqual({ category: "hrm", permissions: [] });
});

test("new Procurement role receives only explicitly chosen advanced actions", async ({ page }) => {
  const writes = await mockRoles(page, []);
  await page.goto("/core/workspace/settings/roles");
  await expect(page.getByRole("heading", { name: "Roles and permissions" })).toBeVisible();
  await page.getByLabel("Role key").fill("buyer_reviewer");
  await page.getByLabel("Role name").fill("Buyer reviewer");
  await page.getByLabel("Category", { exact: true }).first().selectOption("procurement");
  await expect(page.getByRole("button", { name: "Create role" })).toBeDisabled();
  await page.getByRole("switch", { name: "Record manual payment" }).click();
  await page.getByRole("switch", { name: "Edit draft purchase order" }).click();
  await page.getByRole("switch", { name: "Publish sourcing round" }).click();
  await page.getByRole("button", { name: "Create role" }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body.permissions).toEqual([
    "procurement-payment-record", "procurement-po-edit", "procurement-sourcing-publish",
  ]);
  expect(writes[0].body.permissions).not.toContain("procurement-access");
});

test("editing name preserves advanced grants without resending permissions", async ({ page }) => {
  const role: Role = { id: "r1", roleKey: "buyer", roleName: "Buyer", category: "procurement", active: true,
    permissions: ["procurement-payment-record", "procurement-po-edit"] };
  const writes = await mockRoles(page, [role]);
  await page.goto("/core/workspace/settings/roles");
  await page.getByRole("textbox", { name: "Name for buyer" }).fill("Senior buyer");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ roleName: "Senior buyer" });
});

test("explicit permission edit retains other advanced grants", async ({ page }) => {
  const role: Role = { id: "r1", roleKey: "buyer", roleName: "Buyer", category: "procurement", active: true,
    permissions: ["procurement-payment-record"] };
  const writes = await mockRoles(page, [role]);
  await page.goto("/core/workspace/settings/roles");
  await page.getByRole("switch", { name: "Publish sourcing round" }).click();
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ permissions: ["procurement-payment-record", "procurement-sourcing-publish"] });
});

test("category change clears prior grants and requires an explicit choice", async ({ page }) => {
  const role: Role = { id: "r1", roleKey: "assistant", roleName: "Assistant", category: "hrm", active: true,
    permissions: ["employee"] };
  const writes = await mockRoles(page, [role]);
  await page.goto("/core/workspace/settings/roles");
  await page.getByRole("combobox", { name: "Category for assistant" }).selectOption("procurement");
  await expect(page.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
  await page.getByRole("switch", { name: "Record manual payment" }).click();
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ category: "procurement", permissions: ["procurement-payment-record"] });
});

test("catalog failure closes grant editing while metadata-only update stays safe", async ({ page }) => {
  const role: Role = { id: "r1", roleKey: "buyer", roleName: "Buyer", category: "procurement", active: true,
    permissions: ["procurement-payment-record"] };
  const writes = await mockRoles(page, [role], null);
  await page.goto("/core/workspace/settings/roles");
  await expect(page.getByText("Procurement permission choices are unavailable.", { exact: false })).toBeVisible();
  await expect(page.getByRole("switch", { name: "Record manual payment" })).toHaveCount(0);
  await page.getByRole("textbox", { name: "Name for buyer" }).fill("Buyer two");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ roleName: "Buyer two" });
});

test("empty effective role permissions never bootstrap a grant during readback or rename", async ({ page }) => {
  const role: Role = { id: "r2", roleKey: "custom_buyer", roleName: "Custom buyer", category: "procurement", active: true, permissions: [] };
  const unknown: Role = { id: "r3", roleKey: "unknown_legacy", roleName: "Unknown legacy", category: "procurement", active: true, permissions: [] };
  const writes = await mockRoles(page, [role, unknown]);
  await page.goto("/core/workspace/settings/roles");
  await expect(page.getByRole("switch", { name: "Procurement access" })).toHaveCount(2);
  for (const grant of await page.getByRole("switch", { name: "Procurement access" }).all()) await expect(grant).not.toBeChecked();
  await page.getByRole("textbox", { name: "Name for custom_buyer" }).fill("Renamed buyer");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ roleName: "Renamed buyer" });
});

test("system administrator shows effective Procurement grants as read only", async ({ page }) => {
  const role: Role = { id: "r4", roleKey: "erp_admin", roleName: "ERP administrator", category: "system", active: true,
    permissions: ["erp_admin"], effectiveProcurementPermissions: ["procurement-access", "procurement-payment-record"] };
  const writes = await mockRoles(page, [role]);
  await page.goto("/core/workspace/settings/roles");
  await page.getByText("Effective Procurement actions: 2 (read only)").click();
  await expect(page.getByText("procurement-payment-record", { exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "Name for erp_admin" }).fill("ERP administrator two");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].body).toEqual({ roleName: "ERP administrator two" });
});
