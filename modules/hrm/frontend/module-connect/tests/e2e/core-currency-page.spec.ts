import { expect as baseExpect, test, type Page, type Route } from "@playwright/test";
const expect = baseExpect.configure({ timeout: 30_000 });
test.setTimeout(120_000);

const actor = "6115aa71-334a-4e3a-97c0-24e1d7463c0f";
const first = "01a17921-a5a8-7ac2-8912-f5c2197c82ec";
const second = "01a17921-a5a8-7ac2-8912-f5c2197c82ed";
const versionId = "01a17921-a5a8-7ac2-8912-f5c2197c82ee";
const stamp = "2026-10-30T08:00:00Z";
const version = (overrides: Record<string, unknown> = {}) => ({
  id: versionId, currencyCode: "ZMW", revision: 1, minorUnits: 2, rounding: "ToEven",
  sourceReference: "Core reference review", publishedAtUtc: stamp, retiredAtUtc: null,
  retirementReason: null, digest: "a".repeat(64), current: true, ...overrides,
});
const json = (route: Route, body: unknown, status = 200) => route.fulfill({
  status, contentType: "application/json", body: JSON.stringify(body),
});

type FixtureOptions = {
  canConfigure?: boolean;
  denied?: boolean;
  initial?: Record<string, unknown>[];
  onPublish?: (route: Route, key: string, body: Record<string, unknown>) => Promise<void>;
  onRetire?: (route: Route, key: string, body: Record<string, unknown>) => Promise<void>;
  onRecovery?: (route: Route, operation: string, key: string) => Promise<void>;
};
async function installCoreFixture(page: Page, options: FixtureOptions = {}) {
  const calls: { method: string; path: string; key: string | null; companyId: string | null }[] = [];
  const items = [...(options.initial ?? [])];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();
    const key = request.headers()["idempotency-key"] ?? null;
    const companyId = url.searchParams.get("companyId");
    calls.push({ method, path, key, companyId });
    if (path === "/api/hrm/auth/capabilities") return json(route, { mode: "local", localUsersEnabled: true, identityConfigured: false });
    if (path === "/api/hrm/auth/me") return json(route, { authenticated: true, user: {
      id: actor, email: "core.reader@example.invalid", displayName: "Core reader", roles: ["currency_reader"],
      isActive: true, mustChangePassword: false,
    } });
    if (path === "/api/hrm/me") return json(route, { linked: false, worker: null, subject: actor });
    if (path === "/api/core/v1/workspace-context") return json(route, {
      schemaVersion: 1, tenantId: "core-private-tenant", principalId: actor, organisationWide: true,
      coreActions: ["core-currency-precision-read", ...(options.canConfigure === false ? [] : ["core-currency-precision-configure"])],
      companies: [
        { id: first, code: "ONE", registeredName: "Company One", currency: "ZMW", isDefault: true },
        { id: second, code: "TWO", registeredName: "Company Two", currency: "USD", isDefault: false },
      ], branches: [],
    });
    if (path === "/api/core/v1/currency-precisions" && method === "GET") {
      if (options.denied) return json(route, { code: "CoreCurrencyReadDenied", message: "CoreCurrencyReadDenied" }, 403);
      return json(route, { schemaVersion: 1, companyId, companyCurrency: companyId === second ? "USD" : "ZMW",
        canConfigure: options.canConfigure ?? true, items });
    }
    if (path.startsWith("/api/core/v1/currency-precisions/commands/") && method === "GET") {
      const match = path.match(/\/commands\/(Publish|Retire)\/([0-9a-f-]+)$/);
      if (!match || !options.onRecovery) return json(route, { code: "NotFound" }, 404);
      return options.onRecovery(route, match[1], match[2]);
    }
    if (path === `/api/core/v1/currency-precisions/${versionId}` && method === "GET")
      return json(route, items.find((item) => item.id === versionId) ?? { code: "NotFound" }, items.some((item) => item.id === versionId) ? 200 : 404);
    if (path === "/api/core/v1/currency-precisions" && method === "POST") {
      if (!key) return json(route, { code: "MissingKey" }, 400);
      const body = request.postDataJSON() as Record<string, unknown>;
      if (options.onPublish) return options.onPublish(route, key, body);
      items.splice(0, items.length, version({ sourceReference: body.sourceReference, minorUnits: body.minorUnits, rounding: body.rounding }));
      return json(route, { ...items[0], commandKey: key, operation: "Publish" }, 201);
    }
    if (path === `/api/core/v1/currency-precisions/${versionId}/retire` && method === "POST") {
      if (!key) return json(route, { code: "MissingKey" }, 400);
      const body = request.postDataJSON() as Record<string, unknown>;
      if (options.onRetire) return options.onRetire(route, key, body);
      items.splice(0, items.length, version({ current: false, retiredAtUtc: stamp, retirementReason: body.reason }));
      return json(route, { ...items[0], commandKey: key, operation: "Retire" });
    }
    if (path === "/api/hrm/branding") return json(route, { code: "NotConfigured" }, 404);
    return json(route, { code: "UnexpectedApiCall", path, method }, 404);
  });
  return { calls, items };
}

test("Core-only reader sees real references without an HRM admin role", async ({ page }) => {
  const fixture = await installCoreFixture(page, { canConfigure: false, initial: [version()] });
  await page.goto("/core/workspace/settings/currencies");
  await expect(page.getByRole("heading", { name: "Currencies" })).toBeVisible();
  await expect(page.getByText("Company default: ZMW")).toBeVisible();
  await expect(page.getByRole("table").getByText("ZMW")).toBeVisible();
  await expect(page.getByRole("button", { name: "Publish revision" })).toHaveCount(0);
  expect(fixture.calls.some((call) => call.path === "/api/core/v1/workspace-context")).toBe(true);
  expect(fixture.calls.some((call) => call.path === "/api/hrm/admin/legal-entities" || call.path === "/api/hrm/admin/locations")).toBe(false);
  expect(fixture.calls.some((call) => call.path === "/api/core/v1/currency-precisions" && call.companyId === first)).toBe(true);
});

test("currency-only role finds ERP Core from the launcher and switcher while other settings stay gated", async ({ page }) => {
  await installCoreFixture(page, { canConfigure: false, initial: [version()] });
  await page.goto("/");
  await expect(page.getByRole("link", { name: /ERP Core/ })).toBeVisible();
  await page.getByRole("link", { name: /ERP Core/ }).click();
  await expect(page).toHaveURL(/\/core\/workspace\/settings\/currencies$/);
  await expect(page.getByRole("heading", { name: "Currencies" })).toBeVisible();
  await page.getByRole("button", { name: "Switch ERP module" }).click();
  await expect(page.getByRole("menuitem", { name: "ERP Core" })).toBeVisible();
  await page.goto("/core/workspace/settings/organization");
  await expect(page.getByRole("heading", { name: "Core settings access required" })).toBeVisible();
});

test("Core page publishes then retires one revision with distinct original keys", async ({ page }) => {
  const fixture = await installCoreFixture(page);
  await page.goto("/core/workspace/settings/currencies");
  await expect(page.getByRole("heading", { name: "Publish a calculation revision" })).toBeVisible();
  await page.getByLabel("Reference source").fill("Approved Core reference");
  await page.getByLabel("Reason for publication").fill("Initial reviewed precision");
  await page.getByRole("button", { name: "Publish revision" }).click();
  await expect(page.getByText(/revision 1 was published/)).toBeVisible();
  expect(fixture.calls.filter((call) => call.method === "POST" && call.path === "/api/core/v1/currency-precisions")).toHaveLength(1);
  await page.getByRole("button", { name: "View" }).click();
  await expect(page.getByRole("region", { name: "Currency revision detail" }).getByText("Approved Core reference")).toBeVisible();
  await page.getByLabel("Reason for retirement").fill("Reference withdrawn");
  await page.getByRole("button", { name: "Retire this revision" }).click();
  await expect(page.getByText(/revision 1 was retired/)).toBeVisible();
  const posts = fixture.calls.filter((call) => call.method === "POST" && call.path.startsWith("/api/core/v1/currency-precisions"));
  expect(posts).toHaveLength(2);
  expect(posts[0].key).toMatch(/^[0-9a-f-]{36}$/i);
  expect(posts[1].key).toMatch(/^[0-9a-f-]{36}$/i);
  expect(posts[0].key).not.toBe(posts[1].key);
});

test("lost Publish response keeps the original key and recovers after reload without another POST", async ({ page }) => {
  let saved: Record<string, unknown> | null = null;
  let fixture: Awaited<ReturnType<typeof installCoreFixture>>;
  fixture = await installCoreFixture(page, { onPublish: async (route, key, body) => {
    saved = { ...version({ sourceReference: body.sourceReference }), commandKey: key, operation: "Publish" };
    fixture.items.push(saved);
    await route.abort("failed");
  }, onRecovery: async (route, operation, key) => {
    if (!saved || saved.commandKey !== key || operation !== "Publish") return json(route, { code: "NotFound" }, 404);
    return json(route, saved, 201);
  } });
  await page.goto("/core/workspace/settings/currencies");
  await page.getByLabel("Reference source").fill("Core reviewed source");
  await page.getByLabel("Reason for publication").fill("Reviewed precision");
  await page.getByRole("button", { name: "Publish revision" }).click();
  await expect(page.getByRole("heading", { name: "Check the saved result" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { name: "Check the saved result" })).toBeVisible();
  await page.getByRole("button", { name: "Check saved result" }).click();
  await expect(page.getByText(/saved publish result recovered/)).toBeVisible();
  expect(fixture.calls.filter((call) => call.method === "POST" && call.path === "/api/core/v1/currency-precisions")).toHaveLength(1);
  expect(fixture.calls.filter((call) => call.path.includes("/commands/Publish/"))).toHaveLength(1);
});

test("company switch reads only the selected Core workspace company", async ({ page }) => {
  const fixture = await installCoreFixture(page, { canConfigure: false });
  await page.goto("/core/workspace/settings/currencies");
  await expect(page.getByText("Company default: ZMW")).toBeVisible();
  await page.getByRole("button", { name: "Organization and branch" }).click();
  await page.getByRole("button", { name: "Company Two" }).click();
  await expect(page.getByText("Company default: USD")).toBeVisible();
  expect(fixture.calls.some((call) => call.path === "/api/core/v1/currency-precisions" && call.companyId === second)).toBe(true);
});

test("server currency-read denial stays a denied state and sends no write", async ({ page }) => {
  const fixture = await installCoreFixture(page, { denied: true });
  await page.goto("/core/workspace/settings/currencies");
  await expect(page.getByRole("heading", { name: "You do not have permission" })).toBeVisible();
  expect(fixture.calls.filter((call) => call.method === "POST" && call.path.startsWith("/api/core/v1/currency-precisions"))).toHaveLength(0);
});
