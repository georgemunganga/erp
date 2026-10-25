import { initialData } from "../src/data";
import { test as base, expect } from "@playwright/test";
import type { Page } from "@playwright/test";
import { adminUser, companyId, principalId, publicBranding, workspaceContext } from "./erp-fixtures/public-context";

// Mocked UI import acceptance ONLY. No real Core login, accounting API or durable ERP write.
const basePath = "/accounting/workspace";
type User = typeof adminUser;
type CoreFixture = { authenticated: boolean; user: User; contextStatus: number; context?: typeof workspaceContext };
type NetworkGuard = { unexpected: string[]; mutations: string[]; pageErrors: string[]; coreReads: string[]; fixture: CoreFixture };
const test = base.extend<{ guard: NetworkGuard }>({
  guard: [async ({ page, baseURL }, use) => {
    if (!baseURL) throw new Error("ERP integration baseURL is required");
    const origin = new URL(baseURL).origin;
    const guard: NetworkGuard = { unexpected: [], mutations: [], pageErrors: [], coreReads: [], fixture: { authenticated: true, user: { ...adminUser }, contextStatus: 200 } };
    page.on("pageerror", (error) => guard.pageErrors.push(error.message));
    await page.route("**/*", async (route) => {
      const request = route.request(); const url = new URL(request.url());
      // Block BEFORE dispatch. No credential/logout/reset/business mutation is mocked as successful.
      if (request.method() !== "GET" && request.method() !== "HEAD") {
        guard.mutations.push(`${request.method()} ${url.pathname}`); await route.abort("blockedbyclient"); return;
      }
      if (url.origin !== origin) { guard.unexpected.push(`external ${url.origin}${url.pathname}`); await route.abort("blockedbyclient"); return; }
      if (url.pathname.startsWith("/api/")) {
        guard.coreReads.push(url.pathname);
        if (url.pathname === "/api/hrm/auth/me") {
          await route.fulfill({ json: { authenticated: guard.fixture.authenticated, user: guard.fixture.authenticated ? guard.fixture.user : null } }); return;
        }
        if (url.pathname === "/api/hrm/branding") { await route.fulfill({ json: publicBranding }); return; }
        if (url.pathname === "/api/core/v1/workspace-context") {
          await route.fulfill({ status: guard.fixture.contextStatus, json: guard.fixture.contextStatus === 200 ? guard.fixture.context || workspaceContext : { code: "workspace-context-denied" } }); return;
        }
        if (guard.fixture.contextStatus === 404 && url.pathname === "/api/hrm/admin/legal-entities") { await route.fulfill({ json: [...workspaceContext.companies, { id: "other", registeredName: "Unassigned Company", currency: "ZMW", isDefault: false }] }); return; }
        if (guard.fixture.contextStatus === 404 && url.pathname === "/api/hrm/admin/locations") { await route.fulfill({ json: [...workspaceContext.branches, { id: "other-branch", legalEntityId: "other", name: "Unassigned Branch" }] }); return; }
        if (guard.fixture.contextStatus === 404 && url.pathname === "/api/hrm/shell/") { await route.fulfill({ json: { confined: true, assignedLocationIds: [workspaceContext.branches[0].id] } }); return; }
        guard.unexpected.push(`unmocked API ${url.pathname}`); await route.abort("blockedbyclient"); return;
      }
      // Accounting's dev server does not host Core sign-in. This inert landing page records
      // the redirect target only; it contains no form, credential handling or API calls.
      if (url.pathname === "/sign-in") {
        await route.fulfill({ contentType: "text/html", body: "<!doctype html><title>Core sign-in fixture</title><h1>Core sign-in fixture</h1>" }); return;
      }
      await route.continue();
    });
    await use(guard);
    expect(guard.pageErrors, "No uncaught client exception").toEqual([]);
    expect(guard.mutations, "Preview actions must never dispatch backend writes").toEqual([]);
    expect(guard.unexpected, "No remote/IdP/unreviewed API fallback is permitted").toEqual([]);
  }, { auto: true }],
});
async function openPreview(page: Page) {
  await page.goto(`${basePath}/`);
  await expect(page.getByRole("navigation", { name: "Main navigation", exact: true })).toBeVisible();
}

test("anonymous redirects to Core sign-in with local returnTo; simulated Core GET identity returns to preview", async ({ page, guard }) => {
  guard.fixture.authenticated = false;
  const requested = `${basePath}/workflows/create-customer?preview=1`;
  await page.goto(requested);
  await expect(page).toHaveURL((url) => url.pathname === "/sign-in");
  const redirected = new URL(page.url());
  expect(redirected.searchParams.get("returnTo")).toBe(requested);
  expect(guard.coreReads).toContain("/api/hrm/auth/me");
  guard.fixture.authenticated = true; // Simulated me response, never a login POST.
  await page.goto(redirected.searchParams.get("returnTo")!);
  await expect(page.locator("h1")).toHaveText("Create customer");
  expect(guard.fixture.user.workerId).toBeNull();
});

test("non-admin Core user cannot enter Accounting preview", async ({ page, guard }) => {
  guard.fixture.user = { ...adminUser, roles: ["procurement_buyer"] };
  await page.goto(`${basePath}/workflows/create-customer`);
  await expect(page.getByRole("heading", { name: "Accounting preview access required", exact: true })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Main navigation", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Next", exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => Object.keys(localStorage).filter((key) => key.startsWith("greenwebb.accounting.preview.v1.")))).toEqual([]);
});

test("Core context denial fails closed without legacy endpoint fallback or preview storage", async ({ page, guard }) => {
  guard.fixture.contextStatus = 403;
  await page.goto(`${basePath}/`);
  await expect(page.getByRole("heading", { name: "Accounting could not open", exact: true })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Main navigation", exact: true })).toHaveCount(0);
  expect(guard.coreReads).toContain("/api/core/v1/workspace-context");
  expect(guard.coreReads.some((path) => path.startsWith("/api/hrm/admin/") || path === "/api/hrm/shell/")).toBe(false);
  expect(await page.evaluate(() => Object.keys(localStorage).filter((key) => key.startsWith("greenwebb.accounting.preview.v1.")))).toEqual([]);
});

test("workerless admin receives Core company branding, font and colors with explicit preview disclosure", async ({ page, guard }) => {
  await openPreview(page);
  await expect(page.getByRole("button", { name: "Organization and branch", exact: true })).toContainText("Fixture Trading Limited");
  await expect(page.locator(".demo-disclosure")).toContainText(/browser.local|preview/i);
  await expect(page.locator(".demo-disclosure")).toContainText(/no.*(posting|backend|live)/i);
  const theme = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    return { primary: root.getPropertyValue("--primary").trim(), button: root.getPropertyValue("--button-primary").trim(), font: getComputedStyle(document.querySelector(".erp-accounting-shell")!).fontFamily };
  });
  expect(theme.primary.toLowerCase()).toBe(publicBranding.primaryColor);
  expect(theme.button.toLowerCase()).toBe(publicBranding.buttonColor);
  expect(theme.font).toContain(publicBranding.fontFamily);
  expect(await page.evaluate(async () => (await document.fonts.load('400 17px "Poppins"')).length)).toBeGreaterThan(0);
  expect(guard.coreReads).toEqual(expect.arrayContaining(["/api/hrm/auth/me", "/api/hrm/branding", "/api/core/v1/workspace-context"]));
  expect(guard.fixture.user.workerId).toBeNull();
});

test("ERP module navigation keeps Core last and links global settings to Core", async ({ page }) => {
  await openPreview(page);
  await page.getByRole("button", { name: "Switch ERP module", exact: true }).click();
  const modules = page.getByRole("navigation", { name: "ERP modules", exact: true });
  await expect(modules.getByRole("link")).toHaveText(["Accounting", "Procurement", "ERP Core"]);
  await expect(modules.getByRole("link", { name: "Accounting", exact: true })).toHaveAttribute("href", `${basePath}/`);
  await expect(modules.getByRole("link", { name: "ERP Core", exact: true })).toHaveAttribute("href", "/core");
  await expect(page.getByRole("link", { name: "ERP Core settings", exact: true })).toHaveAttribute("href", "/core");
});

test("desktop sidebar collapses and expands without losing imported navigation", async ({ page }) => {
  await openPreview(page);
  const sidebar = page.getByRole("complementary", { name: "Accounting navigation", exact: true });
  const full = (await sidebar.boundingBox())!.width;
  await page.getByRole("button", { name: "Collapse side menu", exact: true }).click();
  await expect(page.getByRole("button", { name: "Expand side menu", exact: true })).toBeVisible();
  await expect.poll(async () => (await sidebar.boundingBox())!.width).toBeLessThan(full);
  await page.getByRole("button", { name: "Expand side menu", exact: true }).click();
  await expect(page.getByRole("navigation", { name: "Main navigation", exact: true }).getByRole("link", { name: "Workflows", exact: true })).toBeVisible();
});

test("mobile drawer retains imported workflow navigation without horizontal overflow", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${basePath}/workflows/create-customer`);
  await expect(page.locator("h1")).toHaveText("Create customer");
  await page.getByRole("button", { name: "Open navigation", exact: true }).click();
  const navigation = page.getByRole("navigation", { name: "Main navigation", exact: true });
  await expect(navigation.getByRole("link", { name: "Workflows", exact: true })).toBeVisible();
  await navigation.getByRole("link", { name: "Workflows", exact: true }).click();
  await expect(page).toHaveURL(`${basePath}/workflows`);
  await expect(page.locator(".sidebar")).not.toHaveClass(/sidebar-open/);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2)).toBe(true);
});

test("sample customer workflow survives reload only in the principal/company preview namespace", async ({ page }) => {
  await page.goto(`${basePath}/workflows/create-customer`);
  await expect(page.locator("h1")).toHaveText("Create customer");
  await page.locator('[data-field-id="name"] input').fill("Fictional ERP Import Customer");
  await page.getByRole("combobox", { name: "Customer type", exact: true }).click();
  await page.getByRole("option", { name: "Business", exact: true }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.locator('[data-field-id="email"] input').fill("fictional-customer@example.test");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("button", { name: "Complete Create customer", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`${basePath}/workflow-results/result-`));
  await expect(page.locator("h1")).toHaveText("Customer saved locally");
  const receiptUrl = page.url();
  const namespace = `greenwebb.accounting.preview.v1.${encodeURIComponent(principalId)}.${encodeURIComponent(companyId)}`;
  await expect.poll(() => page.evaluate((key) => {
    const workspace = JSON.parse(localStorage.getItem(`${key}.workspace`) || "{}");
    const workflows = JSON.parse(localStorage.getItem(`${key}.workflows`) || "{}");
    return Boolean(workspace.customers?.some((record: { name: string }) => record.name === "Fictional ERP Import Customer") && workflows.receipts?.some((receipt: { flowId: string }) => receipt.flowId === "create-customer"));
  }, namespace)).toBe(true);
  await page.reload();
  await expect(page).toHaveURL(receiptUrl);
  await expect(page.locator("h1")).toHaveText("Customer saved locally");
  await page.goto(`${basePath}/customers`);
  await expect(page.locator(".records-table").getByText("Fictional ERP Import Customer", { exact: true })).toBeVisible();
  expect(await page.evaluate(() => [localStorage.getItem("books-demo-workspace-v1"), localStorage.getItem("books-workflows-browser-local-v2")])).toEqual([null, null]);
});


test("older Core404 uses only owned legacy choices and intersects assigned branches", async ({ page, guard }) => {
  guard.fixture.contextStatus = 404;
  await openPreview(page);
  await page.getByRole("button", { name: "Organization and branch", exact: true }).click();
  const context = page.getByRole("group", { name: "Organization context", exact: true });
  await expect(context).toContainText("Fixture Head Office");
  await expect(context).not.toContainText("Unassigned Company");
  await expect(context).not.toContainText("Unassigned Branch");
  expect(guard.coreReads).toEqual(expect.arrayContaining(["/api/hrm/admin/legal-entities", "/api/hrm/admin/locations", "/api/hrm/shell/"]));
});

test("switching permitted company remounts and restores the correct sample partition", async ({ page, guard }) => {
  const otherId = "44444444-4444-4444-8444-444444444444";
  guard.fixture.context = { ...workspaceContext, companies: [...workspaceContext.companies, { id: otherId, code: "TWO", registeredName: "Second Fixture Company", currency: "ZMW", isDefault: false }], branches: [...workspaceContext.branches, { id: "55555555-5555-4555-8555-555555555555", code: "TWO-HQ", name: "Second Head Office", legalEntityId: otherId, type: "Office" }] };
  await page.addInitScript(({ workspace, key }) => {
    const copy = structuredClone(workspace);
    copy.customers.unshift({ id: "scoped-one", code: "CUS-SCOPED", name: "Scoped Company One", party: "Scoped Company One", date: "2026-10-25", status: "Active", amount: 0 });
    localStorage.setItem(key, JSON.stringify(copy));
  }, { workspace: initialData, key: `greenwebb.accounting.preview.v1.${principalId}.${companyId}.workspace` });
  await page.goto(`${basePath}/customers`);
  await expect(page.locator("table")).toContainText("Scoped Company One");
  await page.getByRole("button", { name: "Organization and branch", exact: true }).click();
  await page.getByRole("group", { name: "Organization context", exact: true }).getByRole("button", { name: "Second Fixture Company", exact: true }).click();
  await expect(page.locator("table")).not.toContainText("Scoped Company One");
  await page.getByRole("button", { name: "Organization and branch", exact: true }).click();
  await page.getByRole("group", { name: "Organization context", exact: true }).getByRole("button", { name: "Fixture Trading Limited", exact: true }).click();
  await expect(page.locator("table")).toContainText("Scoped Company One");
});
