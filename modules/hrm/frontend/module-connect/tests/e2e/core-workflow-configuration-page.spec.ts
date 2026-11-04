import { expect as baseExpect, test, type Page, type Route } from "@playwright/test";

const expect = baseExpect.configure({ timeout: 30_000 });
test.setTimeout(120_000);

// Browser UI coverage with intercepted APIs, not authenticated production UAT.
const root = "/core/workspace/settings/workflows";
const item = (recordType: string, recordLabel: string, trigger = "submitted") => ({
  id: `procurement.${recordType}.${trigger}`,
  moduleId: "procurement",
  moduleLabel: "Procurement",
  recordType,
  recordLabel,
  trigger,
  triggerLabel: trigger === "recorded" ? "After recording" : "When submitted",
  description: `Configure ${recordLabel.toLowerCase()} review.`,
  availabilityNote: "Current permissions and approval limits still apply.",
  settings: [
    { label: "Open approval settings", path: "/procurement/workspace/settings/buying-rules" },
  ],
});
const rows = [
  item("purchase-request", "Purchase request"),
  item("payment-request", "Payment request"),
  item("payment-request", "Payment request", "recorded"),
];
const respond = (route: Route, body: unknown, status = 200) =>
  route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
async function fixture(
  page: Page,
  options: { role?: string; items?: unknown[]; denied?: boolean; unavailable?: boolean } = {},
) {
  const writes: string[] = [];
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  let reads = 0;
  let unavailable = options.unavailable ?? false;
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() !== "GET") writes.push(path);
    if (path === "/api/hrm/auth/capabilities")
      return respond(route, { mode: "local", localUsersEnabled: true, identityConfigured: false });
    if (path === "/api/hrm/auth/me")
      return respond(route, {
        authenticated: true,
        user: {
          id: "6115aa71-334a-4e3a-97c0-24e1d7463c0f",
          email: "fixture@example.invalid",
          displayName: "Workflow administrator",
          roles: [options.role ?? "erp_admin"],
          isActive: true,
          mustChangePassword: false,
        },
      });
    if (path === "/api/hrm/me")
      return respond(route, {
        linked: false,
        worker: null,
        subject: "6115aa71-334a-4e3a-97c0-24e1d7463c0f",
      });
    if (path === "/api/core/v1/workspace-context")
      return respond(route, {
        schemaVersion: 1,
        tenantId: "fixture",
        principalId: "6115aa71-334a-4e3a-97c0-24e1d7463c0f",
        organisationWide: true,
        coreActions: [],
        companies: [
          {
            id: "01a17921-a5a8-7ac2-8912-f5c2197c82ec",
            code: "ONE",
            registeredName: "Fixture company",
            currency: "ZMW",
            isDefault: true,
          },
        ],
        branches: [],
      });
    if (path === "/api/core/v1/workflow-configuration") {
      reads++;
      if (options.denied) return respond(route, { message: "Forbidden" }, 403);
      if (unavailable) return respond(route, { message: "Unavailable" }, 503);
      return respond(route, { schemaVersion: 1, items: options.items ?? rows });
    }
    return respond(route, { code: "NotConfigured" }, 404);
  });
  return {
    writes,
    errors,
    reads: () => reads,
    recover: () => {
      unavailable = false;
    },
  };
}

async function select(page: Page, label: string, option: string) {
  await page.getByLabel(label, { exact: true }).click();
  await page.getByRole("option", { name: option, exact: true }).click();
}

test("Core user without employment selects request settings and follows a module-owned link", async ({
  page,
}) => {
  const api = await fixture(page);
  await page.goto(root);
  await expect(
    page.getByRole("heading", { name: "Approval workflows", exact: true }),
  ).toBeVisible();
  await select(page, "Module", "Procurement");
  await select(page, "Record type", "Purchase request");
  await expect(page.getByRole("link", { name: "Open approval settings" })).toHaveAttribute(
    "href",
    "/procurement/workspace/settings/buying-rules",
  );
  await expect(page.getByRole("region", { name: "Selected workflow settings" })).toContainText(
    "Current permissions and approval limits still apply.",
  );
  expect(api.writes).toEqual([]);
  expect(api.errors).toEqual([]);
});

test("payment approval and confirmation remain separate choices and switching record clears selection", async ({
  page,
}) => {
  await fixture(page);
  await page.goto(root);
  await select(page, "Module", "Procurement");
  await select(page, "Record type", "Payment request");
  await expect(page.getByRole("link", { name: "Open approval settings" })).toHaveCount(0);
  await select(page, "When", "After recording");
  await expect(page.getByRole("region", { name: "Selected workflow settings" })).toContainText(
    "after recording",
  );
  await select(page, "Record type", "Purchase request");
  await expect(page.getByRole("region", { name: "Selected workflow settings" })).toContainText(
    "when submitted",
  );
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByRole("link", { name: "Open approval settings" })).toHaveCount(0);
});

test("no installed providers shows an honest empty state", async ({ page }) => {
  await fixture(page, { items: [] });
  await page.goto(root);
  await expect(
    page.getByRole("heading", { name: "No workflow settings registered" }),
  ).toBeVisible();
});

test("permission failure offers no links", async ({ page }) => {
  await fixture(page, { denied: true });
  await page.goto(root);
  await expect(
    page.getByRole("heading", { name: "You do not have permission", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Module", { exact: true })).toHaveCount(0);
});

test("transient read failure can retry without any write", async ({ page }) => {
  const api = await fixture(page, { unavailable: true });
  await page.goto(root);
  await expect(
    page.getByText("Workflow settings could not be loaded. Please try again."),
  ).toBeVisible();
  api.recover();
  await page.getByRole("button", { name: "Try again", exact: true }).click();
  await expect(page.getByLabel("Module", { exact: true })).toBeVisible();
  expect(api.reads()).toBeGreaterThanOrEqual(2);
  expect(api.writes).toEqual([]);
  expect(api.errors).toEqual([]);
});

test("ordinary user cannot open Core administrative workflow settings", async ({ page }) => {
  const api = await fixture(page, { role: "employee" });
  await page.goto(root);
  await expect(page.getByRole("heading", { name: "Core settings access required" })).toBeVisible();
  expect(api.reads()).toBe(0);
});
