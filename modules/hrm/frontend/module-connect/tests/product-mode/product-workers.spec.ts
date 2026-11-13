import { expect, test, type Page } from "@playwright/test";

const company = "1d12138d-b50d-4998-aa53-4a6fc8a77be8";
const location = "28b64065-2982-4dc7-a325-1557acb7a444";
const worker = "3ac6641c-4297-44e7-a48a-31d9998807dc";
const marker = "a".repeat(64);
const csrf = "c".repeat(43);
const prefix = "/api/core/product-identity/operational";
const link = "8ddd4d8d-4be4-4ccb-a2b1-0f554ba5855f";
const grant = "bd6baa54-dc6a-44ab-94b0-4b0778ca9e2b";
const companyRow = {
  company_id: company,
  link_id: link,
  name: "Copperbelt Trading",
  role: "member",
};

async function fixture(page: Page, mode: "archive" | "replace" | "signed-out") {
  let archived = false;
  let archives = 0;
  let reads = 0;
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const headers = request.headers();
    for (const name of [
      "authorization",
      "hrm-default-tenantid",
      "x-shell-entity",
      "x-shell-location",
    ]) {
      expect(headers[name]).toBeUndefined();
    }
    if (url.pathname === "/api/core/v1/workspace-context") {
      if (mode === "signed-out") return route.fulfill({ status: 401 });
      return route.fulfill({
        json: {
          schemaVersion: 1,
          tenantId: "fixture",
          contextKind: "product",
          selectedModule: "hrm",
          selectedCompanyId: company,
          organisationWide: false,
          sessionContext: marker,
          coreActions: [],
          workerDirectory: true,
          workerArchive: mode === "archive",
          companies: [
            {
              id: company,
              code: "TEST",
              registeredName: "Copperbelt Trading",
              currency: "ZMW",
              isDefault: true,
            },
          ],
          branches: [{ id: location, name: "Kitwe", legalEntityId: company }],
        },
      });
    }
    if (url.pathname === prefix + "/session")
      return route.fulfill({
        json: {
          verified: true,
          purpose: "erp-session-v1",
          product_code: "kuubu",
          csrf_token: csrf,
          operational_access_granted: false,
          next_step: "company_selection",
          proof_until: new Date(Date.now() + 30000).toISOString(),
          company_discovery_available: true,
        },
      });
    if (url.pathname === prefix + "/bootstrap")
      return route.fulfill({ json: { csrf_token: csrf, operational_access_granted: false } });
    if (url.pathname === prefix + "/companies")
      return route.fulfill({
        json: { companies: [companyRow], next_cursor: null, operational_access_granted: false },
      });
    if (url.pathname === prefix + "/companies/" + company)
      return route.fulfill({
        json: {
          company: companyRow,
          licensed_modules: ["hrm"],
          operational_access_granted: false,
          next_step: "module_access_assignment",
        },
      });
    if (url.pathname === prefix + "/erp-session/review")
      return route.fulfill({
        json: {
          company_id: company,
          module: "hrm",
          grant_version_id: grant,
          grant_revision: 1,
          scope: "locations",
          location_ids: [location],
          roles: ["hr_ops"],
          actions: ["hrm-access"],
          session_established: false,
          operational_access_granted: false,
        },
      });
    if (
      url.pathname === prefix + "/erp-session/select" ||
      url.pathname === prefix + "/erp-session/session"
    ) {
      const scope =
        request.method() === "GET" ? { scope: "locations", location_ids: [location] } : {};
      return route.fulfill({
        json: {
          ...scope,
          company_id: company,
          module: "hrm",
          session_established: true,
          expires_at: new Date(Date.now() + 300000).toISOString(),
          operational_access_granted: false,
          next_step: "erp_access_pending",
        },
      });
    }
    expect(headers["x-kuubu-session-context"]).toBe(marker);
    if (url.pathname === "/api/hrm/workers/") {
      reads++;
      if (mode === "replace" && reads > 1) return route.fulfill({ status: 409 });
      const items =
        archived && !url.searchParams.has("includeArchived")
          ? []
          : [
              {
                id: worker,
                fullName: "Ruth Example",
                employeeNo: "EMP-0001",
                locationId: location,
                locationName: "Kitwe",
                status: archived ? "archived" : "active",
              },
            ];
      return route.fulfill({ json: { items, totalCount: items.length } });
    }
    expect(url.pathname).toBe("/api/hrm/workers/" + worker + "/archive");
    expect(request.method()).toBe("POST");
    expect(headers["x-kuubu-operational-csrf"]).toBe(csrf);
    expect(request.postDataJSON()).toEqual({});
    archives++;
    archived = true;
    return route.fulfill({ status: 200 });
  });
  return { archives: () => archives, reads: () => reads };
}

test("the product employee route loads only server-selected branches and archives after confirmation", async ({
  page,
}) => {
  const calls = await fixture(page, "archive");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await page.getByRole("button", { name: "Review Human resources access" }).click();
  await page.getByRole("button", { name: "Select company session" }).click();
  await page.getByRole("link", { name: "Open employees" }).click();
  await expect(page).toHaveURL(/\/hrm\/employees\/?$/);
  await expect(page.getByText("Copperbelt Trading")).toBeVisible();
  await expect(page.getByText("Ruth Example", { exact: true })).toBeVisible();
  await page.getByRole("combobox", { name: "Branch" }).click();
  await expect(page.getByRole("option")).toHaveCount(2);
  await page.getByRole("option", { name: "Kitwe" }).click();
  await page.getByRole("button", { name: "Archive Ruth Example" }).click();
  expect(calls.archives()).toBe(0);
  await page.getByRole("button", { name: "Archive employee", exact: true }).click();
  await expect(page.getByText("Ruth Example archived.")).toBeVisible();
  await expect(page.getByText("Ruth Example", { exact: true })).toHaveCount(0);
  await page.getByLabel("Include archived employees").check();
  await expect(page.getByText("Ruth Example", { exact: true })).toBeVisible();
  expect(calls.archives()).toBe(1);
});

test("a replaced session clears previously displayed employees and does not expose archive to a reader", async ({
  page,
}) => {
  await fixture(page, "replace");
  await page.goto("/hrm/employees");
  await expect(page.getByText("Ruth Example", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Archive Ruth Example" })).toHaveCount(0);
  await page.getByPlaceholder("Search employees").fill("Ru");
  await expect(page.getByRole("alert")).toContainText("Your workspace access changed");
  await expect(page.getByText("Ruth Example", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Choose company" })).toHaveAttribute(
    "href",
    "/workspace-access",
  );
});

test("a direct employee link without a selected session never loads worker data", async ({
  page,
}) => {
  const calls = await fixture(page, "signed-out");
  await page.goto("/hrm/employees");
  await expect(page.getByRole("alert")).toContainText("Your workspace access changed");
  expect(calls.reads()).toBe(0);
});
