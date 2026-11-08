import { test, expect, type Page } from "@playwright/test";

test.describe.configure({ mode: "serial" });
const base = "http://127.0.0.1:3187";
async function scenario(page: Page, value = "ready") {
  await page.request.post(`${base}/_fixture/reset`, { data: { scenario: value } });
  await page.goto(`${base}/setup`);
}
async function review(page: Page) {
  await page.getByRole("combobox", { name: "Billing account" }).click();
  await page.getByRole("option", { name: "Synthetic billing account", exact: true }).click();
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await page.getByLabel("Company name", { exact: true }).fill("Mosi Traders");
  await page.getByRole("button", { name: "Review company" }).click();
}
async function confirm(page: Page, retry = false) {
  await page.getByRole("checkbox").check();
  await page
    .getByRole("button", { name: retry ? "Retry reviewed request" : "Create company", exact: true })
    .click();
}
test("public branded setup renders without operational auth/branding calls and requires review confirmation", async ({
  page,
}, testInfo) => {
  const calls: string[] = [];
  page.on("request", (req) => {
    if (req.url().includes("/api/")) calls.push(req.url());
  });
  await scenario(page);
  await expect(page.getByRole("link", { name: "Kuubu home" })).toBeVisible();
  await review(page);
  await page.screenshot({ path: testInfo.outputPath("desktop-review.png"), fullPage: true });
  await expect(page.getByRole("button", { name: "Create company", exact: true })).toBeDisabled();
  expect((await (await page.request.get(`${base}/_fixture/state`)).json()).calls).toHaveLength(0);
  await confirm(page);
  await expect(page.getByRole("heading", { name: "Your company is saved." })).toBeVisible();
  const state = await (await page.request.get(`${base}/_fixture/state`)).json();
  expect(state.calls).toHaveLength(1);
  expect(state.receipts).toBe(1);
  expect(calls.every((url) => url.includes("/api/core/product-identity/setup/"))).toBe(true);
  expect(await page.evaluate(() => sessionStorage.getItem("kuubu.company-setup.v1"))).not.toContain(
    "csrf",
  );
  const response = await page.request.get(`${base}/setup`);
  expect(response.headers()["cache-control"]).toBe("no-store");
  expect(response.headers()["referrer-policy"]).toBe("no-referrer");
});
test("lost response and reload retain the exact company request and require a fresh explicit retry", async ({
  page,
}) => {
  await scenario(page, "ambiguous");
  await review(page);
  await confirm(page);
  await expect(page.getByRole("alert")).toContainText("couldn’t confirm");
  await expect(page.getByRole("button", { name: "Edit details" })).toHaveCount(0);
  await page.reload();
  await expect(page.getByRole("heading", { name: "Check your previous request." })).toBeVisible();
  await expect(page.getByRole("checkbox")).not.toBeChecked();
  await expect(page.getByRole("button", { name: "Retry reviewed request" })).toBeDisabled();
  await confirm(page, true);
  await expect(
    page.getByText("Your earlier request was confirmed. No additional company was created."),
  ).toBeVisible();
  const state = await (await page.request.get(`${base}/_fixture/state`)).json();
  expect(state.calls).toHaveLength(2);
  expect(state.receipts).toBe(1);
  expect(state.calls[0]).toEqual(state.calls[1]);
});
test("fresh consent can recover the same request after expiry", async ({ page }) => {
  await scenario(page, "ambiguous");
  await review(page);
  await confirm(page);
  await expect(page.getByRole("alert")).toBeVisible();
  await page.request.post(`${base}/_fixture/state`, { data: { scenario: "expired" } });
  await page.reload();
  await expect(
    page.getByText("Your previous company request is retained.", { exact: false }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Continue with Kuubu", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Check your previous request." })).toBeVisible();
  await confirm(page, true);
  await expect(page.getByRole("heading", { name: "Your company is saved." })).toBeVisible();
  const state = await (await page.request.get(`${base}/_fixture/state`)).json();
  expect(state.receipts).toBe(1);
  expect(state.calls[0]).toEqual(state.calls[1]);
});
test("another identity cannot resume or see the retained company's details", async ({ page }) => {
  await scenario(page, "ambiguous");
  await review(page);
  await confirm(page);
  await expect(page.getByRole("alert")).toBeVisible();
  await page.request.post(`${base}/_fixture/state`, { data: { subject: "18" } });
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Resume with the original account." }),
  ).toBeVisible();
  await expect(page.getByText("Mosi Traders", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Retry reviewed request" })).toHaveCount(0);
  expect((await (await page.request.get(`${base}/_fixture/state`)).json()).calls).toHaveLength(1);
});
test("a failed browser storage write prevents sending the company request", async ({ page }) => {
  await scenario(page);
  await review(page);
  await page.evaluate(() => {
    Storage.prototype.setItem = () => {
      throw new Error("blocked storage");
    };
  });
  await confirm(page);
  await expect(page.getByRole("alert")).toContainText("Nothing was sent");
  expect((await (await page.request.get(`${base}/_fixture/state`)).json()).calls).toHaveLength(0);
});
test("empty eligible-account state cannot create a company", async ({ page }) => {
  await scenario(page, "empty");
  await expect(
    page.getByText("No eligible billing accounts were found.", { exact: false }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue", exact: true })).toBeDisabled();
});
test("account pagination makes later billing accounts selectable", async ({ page }) => {
  await scenario(page, "paged");
  await page.getByRole("button", { name: "Load more accounts" }).click();
  await expect(page.getByRole("button", { name: "Load more accounts" })).toHaveCount(0);
  await page.getByRole("combobox", { name: "Billing account" }).click();
  await page.getByRole("option", { name: "Synthetic account 22", exact: true }).click();
  await expect(page.getByRole("button", { name: "Continue", exact: true })).toBeEnabled();
});
test("the portaled billing-account menu keeps Kuubu product branding", async ({ page }) => {
  await scenario(page);
  await page.getByRole("combobox", { name: "Billing account" }).click();
  const option = page.getByRole("option", { name: "Synthetic billing account", exact: true });
  await expect(option).toBeVisible();
  expect(await option.evaluate((element) => getComputedStyle(element).color)).toBe(
    "rgb(0, 75, 59)",
  );
});
test("capacity denial retains the same reviewed request and communicates the allowance", async ({
  page,
}) => {
  await scenario(page, "capacity");
  await review(page);
  await confirm(page);
  await expect(page.getByRole("alert")).toContainText("no company places available");
  await expect(page.getByText("2 companies in total", { exact: true })).toBeVisible();
  expect((await (await page.request.get(`${base}/_fixture/state`)).json()).receipts).toBe(0);
});
test("mobile layout stays inside the viewport with readable controls", async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await scenario(page);
  await review(page);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await expect(page.getByRole("button", { name: "Create company", exact: true })).toBeVisible();
  const box = await page.getByRole("button", { name: "Create company", exact: true }).boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(44);
  await page.screenshot({ path: testInfo.outputPath("mobile-review.png"), fullPage: true });
});

test("cancelling before submission revokes setup without creating a company", async ({ page }) => {
  await page.route("https://www.kuubuerp.com/**", (route) =>
    route.fulfill({ contentType: "text/html", body: "<h1>Kuubu</h1>" }),
  );
  await scenario(page);
  await page.getByRole("button", { name: "Cancel setup", exact: true }).click();
  await expect(page).toHaveURL("https://www.kuubuerp.com/");
  const state = await (await page.request.get(`${base}/_fixture/state`)).json();
  expect(state.cancelCalls).toBe(1);
  expect(state.calls).toHaveLength(0);
});
test("finishing a server-confirmed outcome clears its retained request", async ({ page }) => {
  await page.route("https://www.kuubuerp.com/**", (route) =>
    route.fulfill({ contentType: "text/html", body: "<h1>Kuubu</h1>" }),
  );
  await scenario(page);
  await review(page);
  await confirm(page);
  await expect(page.getByRole("heading", { name: "Your company is saved." })).toBeVisible();
  await page.getByRole("button", { name: "Return to Kuubu", exact: true }).click();
  await expect(page).toHaveURL("https://www.kuubuerp.com/");
  await page.goto(`${base}/setup`);
  expect(await page.evaluate(() => sessionStorage.getItem("kuubu.company-setup.v1"))).toBeNull();
  const state = await (await page.request.get(`${base}/_fixture/state`)).json();
  expect(state.cancelCalls).toBe(1);
  expect(state.receipts).toBe(1);
});
test("time-limited setup authorization expires while reviewing", async ({ page }) => {
  await page.clock.install();
  await scenario(page);
  await review(page);
  await page.clock.fastForward(301000);
  await expect(page.getByRole("link", { name: "Continue with Kuubu", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Create company", exact: true })).toHaveCount(0);
});
