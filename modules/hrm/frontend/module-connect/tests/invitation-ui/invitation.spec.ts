import { test, expect, type Page } from "@playwright/test";
const origin = "http://127.0.0.1:3192";
const prefix = "/api/core/product-identity/invitation";
const secret = "t".repeat(43),
  nonce = "n".repeat(43);
const invitation = "11111111-1111-1111-1111-111111111111";
const company = "22222222-2222-2222-2222-222222222222";
const member = "33333333-3333-3333-3333-333333333333";
async function fixture(page: Page, mode = "review") {
  const calls: { path: string; body?: Record<string, unknown> }[] = [];
  const unexpected: string[] = [];
  const messages: string[] = [];
  let verified = !["entry", "missing", "unavailable"].includes(mode),
    writes = 0;
  page.on("console", (message) => messages.push(message.text()));
  page.on("pageerror", () => unexpected.push("page-error"));
  await page.route("**/*", (route) =>
    new URL(route.request().url()).origin === origin ? route.continue() : route.abort(),
  );
  await page.route("**/api/**", async (route) => {
    const request = route.request(),
      path = new URL(request.url()).pathname;
    if (!path.startsWith(prefix + "/")) {
      unexpected.push(path);
      return route.fulfill({ status: 401, json: {} });
    }
    const body = request.method() === "POST" ? request.postDataJSON() : undefined;
    calls.push({ path, body });
    if (path.endsWith("/session"))
      return route.fulfill({
        status: mode === "unavailable" ? 503 : 200,
        json: {
          verified,
          purpose: "company-invitation-v1",
          csrf_token: nonce,
          expires_at: new Date(Date.now() + 240000).toISOString(),
          operational_access_granted: false,
        },
      });
    if (path.endsWith("/review"))
      return route.fulfill({
        status: mode === "denied" ? 403 : 200,
        json: {
          invitation_id: invitation,
          company_id: company,
          company_name: "Acacia Trading",
          role: "member",
          revision: 1,
          expires_at: new Date(Date.now() + 86400000).toISOString(),
          operational_access_granted: false,
        },
      });
    if (path.endsWith("/prepare"))
      return route.fulfill({
        status: 201,
        json: { continue_url: prefix + "/start", operational_access_granted: false },
      });
    if (path.endsWith("/start"))
      return route.fulfill({
        status: 200,
        contentType: "text/html",
        body: "<!doctype html><title>Scoped Kuubu sign-in</title><p>Scoped Kuubu sign-in</p>",
      });
    if (path.endsWith("/cancel")) {
      verified = false;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/accept")) {
      writes++;
      if (mode === "ambiguous" && writes === 1) return route.abort("failed");
      return route.fulfill({
        status: 200,
        json: {
          status: writes === 1 ? "Accepted" : "Existing",
          invitation_id: invitation,
          membership_id: member,
          revision: 2,
          operational_access_granted: false,
        },
      });
    }
    unexpected.push(path);
    return route.fulfill({ status: 404 });
  });
  return { calls, unexpected, messages };
}
async function privateState(page: Page, f: Awaited<ReturnType<typeof fixture>>) {
  expect(f.unexpected).toEqual([]);
  expect(f.messages.every((message) => !message.includes(secret))).toBe(true);
  const storage = await page.evaluate(() => ({
    local: Object.entries(localStorage),
    session: Object.entries(sessionStorage),
  }));
  expect(storage.local).toEqual([]);
  for (const [key, value] of storage.session) {
    expect(key).toBe("tsr-scroll-restoration-v1_3");
    expect(value).not.toContain(secret);
    expect(value).not.toContain(nonce);
    const positions = JSON.parse(value);
    for (const elements of Object.values(positions) as Record<
      string,
      { scrollX: number; scrollY: number }
    >[]) {
      for (const position of Object.values(elements)) {
        expect(Number.isFinite(position.scrollX)).toBe(true);
        expect(Number.isFinite(position.scrollY)).toBe(true);
      }
    }
  }
}
test("fresh link is removed before hydration and preparation requires explicit sign-in", async ({
  page,
}) => {
  const f = await fixture(page, "entry");
  await page.goto("/accept-invitation#token=" + secret);
  await expect(
    page.getByRole("button", { name: "Continue with Kuubu", exact: true }),
  ).toBeVisible();
  await expect(page).toHaveURL(origin + "/accept-invitation");
  expect(await page.evaluate(() => typeof window.__kuubuTakeInvitationFragment)).toBe("undefined");
  expect(f.calls.filter((x) => x.body)).toHaveLength(0);
  expect(await page.locator("html").innerText()).not.toContain(secret);
  await page.getByRole("button", { name: "Continue with Kuubu", exact: true }).click();
  await expect(page).toHaveURL(origin + prefix + "/start");
  expect(f.calls.find((x) => x.path.endsWith("/prepare"))?.body).toEqual({ token: secret });
  await privateState(page, f);
});
test("authenticated recipient reviews company and role before checking explicit confirmation", async ({
  page,
}, info) => {
  const f = await fixture(page);
  await page.goto("/accept-invitation");
  await expect(page.getByRole("heading", { name: "Acacia Trading", exact: true })).toBeVisible();
  const accept = page.getByRole("button", { name: "Accept company invitation", exact: true });
  await expect(accept).toBeDisabled();
  await expect(page.getByRole("checkbox")).not.toBeChecked();
  expect(f.calls.filter((x) => x.path.endsWith("/accept"))).toHaveLength(0);
  await page.getByRole("checkbox").check();
  await expect(accept).toBeEnabled();
  await page.screenshot({ path: info.outputPath("review.png"), fullPage: true });
  await accept.click();
  await expect(
    page.getByRole("heading", { name: "Your invitation was accepted.", exact: true }),
  ).toBeVisible();
  const writes = f.calls.filter((x) => x.path.endsWith("/accept"));
  expect(writes).toHaveLength(1);
  expect(writes[0].body).toEqual({
    command_key: expect.any(String),
    expected_revision: 1,
    confirmed: true,
  });
  await privateState(page, f);
});
test("uncertain acceptance retries the same reviewed command instead of issuing a new key", async ({
  page,
}) => {
  const f = await fixture(page, "ambiguous");
  await page.goto("/accept-invitation");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Accept company invitation", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("couldn’t confirm");
  await expect(page.getByRole("checkbox")).toBeDisabled();
  await page
    .getByRole("button", { name: "Retry the same acceptance request", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Your invitation was accepted.", exact: true }),
  ).toBeVisible();
  const writes = f.calls.filter((x) => x.path.endsWith("/accept"));
  expect(writes).toHaveLength(2);
  expect(writes[0].body).toEqual(writes[1].body);
  await privateState(page, f);
});
test("a new email link never silently reviews the previous invitation grant", async ({ page }) => {
  const f = await fixture(page, "existing-link");
  await page.goto("/accept-invitation#token=" + secret);
  await expect(
    page.getByRole("button", { name: "Continue with Kuubu", exact: true }),
  ).toBeVisible();
  expect(f.calls.some((x) => x.path.endsWith("/review"))).toBe(false);
  await page.getByRole("button", { name: "Continue with Kuubu", exact: true }).click();
  await expect(page).toHaveURL(origin + prefix + "/start");
  expect(f.calls.filter((x) => x.body).map((x) => x.path)).toEqual([
    prefix + "/cancel",
    prefix + "/prepare",
  ]);
  await privateState(page, f);
});
test("unavailable and missing-link states offer no disconnected acceptance or sign-in actions", async ({
  page,
}) => {
  for (const mode of ["missing", "unavailable"]) {
    const f = await fixture(page, mode);
    await page.goto("/accept-invitation");
    if (mode === "unavailable") await expect(page.getByRole("alert")).toBeVisible();
    else
      await expect(
        page.getByText("Open your invitation link to continue.", { exact: true }),
      ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Accept company invitation", exact: true }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: "Continue with Kuubu", exact: true }),
    ).toHaveCount(0);
    expect(f.calls.filter((x) => x.body)).toHaveLength(0);
    await privateState(page, f);
    await page.unrouteAll({ behavior: "wait" });
  }
});
test("denied recipient reveals no company or acceptance action", async ({ page }) => {
  const f = await fixture(page, "denied");
  await page.goto("/accept-invitation");
  await expect(page.getByRole("alert")).toContainText("unavailable");
  await expect(page.getByText("Acacia Trading", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("checkbox")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Accept company invitation", exact: true }),
  ).toHaveCount(0);
  await privateState(page, f);
});
test("ending invitation sign-in clears the workflow without a company acceptance write", async ({
  page,
}) => {
  const f = await fixture(page);
  await page.goto("/accept-invitation");
  await page.getByRole("button", { name: "End invitation sign-in", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("has ended");
  expect(f.calls.some((x) => x.path.endsWith("/accept"))).toBe(false);
  expect(f.calls.filter((x) => x.path.endsWith("/cancel"))).toHaveLength(1);
  await privateState(page, f);
});
test("mobile confirmation and action buttons stay visible without horizontal overflow", async ({
  page,
}, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const f = await fixture(page);
  await page.goto("/accept-invitation");
  const checkbox = page.getByRole("checkbox");
  await checkbox.check();
  const accept = page.getByRole("button", { name: "Accept company invitation", exact: true });
  await accept.scrollIntoViewIfNeeded();
  await expect(accept).toBeInViewport();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const box = await checkbox.boundingBox();
  expect(box?.width).toBeGreaterThanOrEqual(20);
  await page.screenshot({ path: info.outputPath("mobile.png"), fullPage: true });
  await privateState(page, f);
});
