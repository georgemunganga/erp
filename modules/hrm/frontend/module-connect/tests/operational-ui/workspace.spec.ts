import { test, expect, type Page } from "@playwright/test";
const prefix = "/api/core/product-identity/operational";
const csrf = "c".repeat(43);
const a = {
  company_id: "11111111-1111-1111-1111-111111111111",
  link_id: "22222222-2222-2222-2222-222222222222",
  name: "Copperbelt Trading",
  role: "member",
};
const b = {
  company_id: "33333333-3333-3333-3333-333333333333",
  link_id: "44444444-4444-4444-4444-444444444444",
  name: "Lusaka Services",
  role: "owner",
};
async function fixture(page: Page, mode = "ready") {
  const calls: string[] = [];
  let release: (() => void) | undefined;
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    calls.push(url.pathname);
    const send = (body: unknown, status = 200) => route.fulfill({ status, json: body });
    if (url.pathname === prefix + "/bootstrap")
      return send({ csrf_token: csrf, operational_access_granted: false });
    if (url.pathname === prefix + "/session") {
      if (mode === "signed-out") return send({}, 401);
      if (mode === "outage") return send({}, 503);
      return send({
        verified: true,
        purpose: "erp-session-v1",
        product_code: "kuubu",
        csrf_token: csrf,
        operational_access_granted: false,
        next_step: "company_selection",
        proof_until: new Date(Date.now() + 30000).toISOString(),
        company_discovery_available: mode !== "disabled",
      });
    }
    if (url.pathname === prefix + "/companies")
      return send({
        companies:
          mode === "empty"
            ? []
            : url.searchParams.has("after")
              ? [b]
              : [a, ...(mode === "paging" ? [] : [b])],
        next_cursor: mode === "paging" && !url.searchParams.has("after") ? b.company_id : null,
        operational_access_granted: false,
      });
    if (url.pathname.startsWith(prefix + "/companies/")) {
      if (mode === "denied") return send({}, 403);
      if (mode === "expired") return send({}, 401);
      const selected = url.pathname.endsWith(a.company_id) ? a : b;
      expect(url.searchParams.get("link_id")).toBe(selected.link_id);
      if (mode === "stale" && selected === a)
        await new Promise<void>((resolve) => {
          release = resolve;
        });
      return send({
        company: selected,
        licensed_modules: ["hrm", "procurement"],
        operational_access_granted: false,
        next_step: "module_access_assignment",
      });
    }
    if (url.pathname === prefix + "/logout") {
      expect(route.request().headers()["x-kuubu-operational-csrf"]).toBe(csrf);
      mode = "signed-out";
      return route.fulfill({ status: 204 });
    }
    if (url.pathname === prefix + "/start") {
      expect(route.request().headers()["x-kuubu-operational-csrf"]).toBe(csrf);
      expect(route.request().postData()).toBe("{}");
      return send({}, 503); // No external sign-in or callback is navigated by this UI fixture.
    }
    if (url.pathname.startsWith(prefix + "/module-access/")) return send({}, 503);
    throw new Error("Unexpected ERP/bootstrap request from identity-only entry");
  });
  return { calls, release: () => release?.() };
}
test("signed-out entry starts only after an explicit protected Continue", async ({ page }) => {
  const f = await fixture(page, "signed-out");
  await page.goto("/workspace-access");
  await expect(page.getByRole("heading", { name: "Continue to Kuubu", exact: true })).toBeVisible();
  expect(f.calls).not.toContain(prefix + "/start");
  await page.getByRole("button", { name: "Continue with Kuubu" }).click();
  await expect(page.getByRole("alert")).toContainText("couldn’t confirm");
  expect(f.calls.filter((x) => x === prefix + "/start")).toHaveLength(1);
});
test("ordinary member reviews licensed modules without gaining ERP access", async ({ page }) => {
  const f = await fixture(page);
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  const review = page.getByRole("region", { name: "Company access review" });
  await expect(review.getByRole("heading", { name: a.name })).toBeVisible();
  await expect(review).toContainText("Human resources");
  await expect(review).toContainText("Module access must be assigned");
  await expect(review.getByRole("link", { name: "Manage company team" })).toHaveCount(0);
  expect(f.calls.every((x) => x.startsWith(prefix))).toBeTruthy();
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
});
test("company owner can reach the separate team workflow", async ({ page }) => {
  await fixture(page);
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Lusaka Services" }).click();
  await expect(page.getByRole("link", { name: "Manage company team" })).toHaveAttribute(
    "href",
    "/team",
  );
});
test("empty company list offers an explicit setup action", async ({ page }) => {
  await fixture(page, "empty");
  await page.goto("/workspace-access");
  await expect(page.getByRole("heading", { name: "No companies yet" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Create a company" })).toHaveAttribute(
    "href",
    "/setup",
  );
});
test("paging keeps explicit selection and denied review cannot show modules", async ({ page }) => {
  await fixture(page, "paging");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "More companies" }).click();
  await expect(page.getByRole("button", { name: "Review Lusaka Services" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Company access review" })).toContainText(
    "Select a company",
  );
});
test("changed access and expired identity provide recovery without a successful module view", async ({
  page,
}) => {
  await fixture(page, "denied");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await expect(page.getByRole("alert")).toContainText("has changed");
  await expect(page.getByRole("region", { name: "Company access review" })).not.toContainText(
    "Human resources",
  );
});
test("expired identity returns to own sign-in", async ({ page }) => {
  await fixture(page, "expired");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await expect(page.getByRole("button", { name: "Continue with Kuubu" })).toBeVisible();
});
test("outage and disabled company capability remain distinct", async ({ page }) => {
  await fixture(page, "outage");
  await page.goto("/workspace-access");
  await expect(
    page.getByRole("heading", { name: "Kuubu access is temporarily unavailable" }),
  ).toBeVisible();
});
test("late company responses cannot replace a new selection", async ({ page }) => {
  const f = await fixture(page, "stale");
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await expect(page.getByRole("status")).toContainText("Checking Copperbelt");
  await page.getByRole("button", { name: "Review Lusaka Services" }).click();
  await expect(
    page
      .getByRole("region", { name: "Company access review" })
      .getByRole("heading", { name: b.name }),
  ).toBeVisible();
  f.release();
  await expect(
    page
      .getByRole("region", { name: "Company access review" })
      .getByRole("heading", { name: a.name }),
  ).toHaveCount(0);
});
test("mobile selection fits the viewport and sign-out clears company review", async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await fixture(page);
  await page.goto("/workspace-access");
  await page.getByRole("button", { name: "Review Copperbelt Trading" }).click();
  await expect(page.getByText("Human resources", { exact: true })).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: testInfo.outputPath("mobile.png"), fullPage: true });
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Continue with Kuubu" })).toBeVisible();
  await expect(page.getByRole("heading", { name: a.name, exact: true })).toHaveCount(0);
});
