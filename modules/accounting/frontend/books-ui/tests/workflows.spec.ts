import { adminUser, publicBranding, workspaceContext, principalId, companyId } from "./erp-fixtures/public-context";
import { listResources } from "../src/workflows/engine";
import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import type { Page, Locator } from "@playwright/test";
import { moduleSpecs } from "../src/data";
import { createFlowFor } from "../src/workflows/registry";
import type { FlowDefinition, FlowValues, FlowRow, FlowStore } from "../src/workflows/contracts";
import type { WorkspaceData } from "../src/types";
interface Case { flow: FlowDefinition; workspace: WorkspaceData; store: FlowStore; values: FlowValues; }
const cases: Case[] = JSON.parse(readFileSync("tests/fixtures/workflow-cases.json", "utf8"));
const BASE_PATH = "/accounting/workspace";
const PREVIEW = `greenwebb.accounting.preview.v1.${principalId}.${companyId}`;
const ORIGIN = new URL(process.env.ACCOUNTING_TEST_BASE_URL || "http://127.0.0.1:3402").origin;
async function gotoAccounting(page: Page, path: string) { return page.goto(`${BASE_PATH}${path}`); }
test.beforeEach(async ({ page }) => {
  await page.route("**/api/**", async route => {
    if (route.request().method() !== "GET") throw new Error("Accounting preview attempted a backend mutation");
    const path = new URL(route.request().url()).pathname;
    const value = path === "/api/hrm/auth/me" ? { authenticated: true, user: adminUser }
      : path === "/api/hrm/branding" ? publicBranding
      : path === "/api/core/v1/workspace-context" ? workspaceContext : null;
    if (!value) throw new Error(`Unexpected Accounting API request: ${path}`);
    await route.fulfill({ json: value });
  });
});
async function chooseOption(control: Locator, value: string, fixture: Case) {
  const combo = control.getByRole("combobox");
  if (!await combo.count()) { await control.locator("select").selectOption(value); return; }
  const collections = [...Object.keys(fixture.workspace), ...fixture.store.artifacts.map(x => x.collection)];
  const record = collections.flatMap(key => listResources(fixture.workspace, fixture.store, key)).find(x => x.id === value);
  const label = record ? `${record.name}${record.code ? ` · ${record.code}` : ""}${record.party ? ` · ${record.party}` : ""}${record.amount !== 0 ? ` · ${record.amount.toFixed(2)}` : ""}` : value;
  await combo.click();
  await control.page().getByRole("option", { name: label, exact: true }).click();
}
async function seed(page: Page, fixture: Case) {
  await page.addInitScript(({ workspace, store, prefix }) => {
    localStorage.clear();
    localStorage.setItem(`${prefix}.workspace`, JSON.stringify(workspace));
    localStorage.setItem(`${prefix}.workflows`, JSON.stringify(store));
  }, { workspace: fixture.workspace, store: fixture.store, prefix: PREVIEW });
}
async function fillStep(page: Page, fixture: Case, index: number) {
  for (const field of fixture.flow.steps[index].fields) {
    const block = page.locator(`[data-field-id="${field.id}"]`); const value = fixture.values[field.id];
    if (field.type === "record" || field.type === "select") { const option = value ? String(value) : field.type === "select" ? field.options?.[0] : undefined; if (option) await chooseOption(block, option, fixture); if (field.required) await expect(block.getByRole("combobox")).not.toHaveText(/Choose an option|Choose a record/); }
    else if (field.id === "amount" && await block.locator("input").count() === 0) { await expect(block).toContainText("Calculated from line items"); }
    else if (["text", "email", "date", "number"].includes(field.type)) await block.locator("input").fill(value === undefined ? "" : String(value));
    else if (field.type === "textarea" || field.type === "csv") await block.locator("textarea").fill(String(value || ""));
    else if (field.type === "checkbox") await block.getByRole("switch").setChecked(value === true);
    else if (field.type === "multi-record") { for (const id of value as string[] || []) await block.locator(`[data-resource-id="${id}"] input`).check(); }
    else if (field.type === "attachment") await block.locator("input[type=file]").setInputFiles({ name: "fictional-receipt.txt", mimeType: "text/plain", buffer: Buffer.from("Fictional receipt") });
    else if (field.type === "allocations") {
      for (const row of value as FlowRow[] || []) { const option = block.locator(`[data-resource-id="${row.recordId}"]`); await option.locator("input").check(); }
      const inputs = block.locator("table input[type=number]"); const rows = value as FlowRow[] || [];
      for (let i = 0; i < rows.length; i++) await inputs.nth(i).fill(String(rows[i].amount));
    } else if (field.type === "lines" || field.type === "journal-lines") {
      const rows = value as FlowRow[] || []; const bodyRows = block.locator("tbody tr"); const initialCount = await bodyRows.count(); const hasExistingRows = initialCount > 0 && (await bodyRows.first().locator("select").count() > 0 || await bodyRows.first().locator("input").count() > 0);
      for (let i = 0; i < rows.length; i++) {
        if (!hasExistingRows || i >= initialCount) await block.getByRole("button", { name: field.type === "lines" ? "Add line" : "Add journal line", exact: true }).click();
        const row = block.locator("tbody tr").nth(i);
        if (field.type === "lines") { await row.locator("input").nth(0).fill(String(rows[i].description)); await row.locator("input").nth(1).fill(String(rows[i].quantity)); await row.locator("input").nth(2).fill(String(rows[i].rate)); }
        else { await chooseOption(row, String(rows[i].account), fixture); await row.locator("input").nth(0).fill(String(rows[i].debit)); await row.locator("input").nth(1).fill(String(rows[i].credit)); }
      }
    }
  }
}
for (const fixture of cases) test(`finish ${fixture.flow.id}`, async ({ page }) => {
  const errors: string[] = []; const outbound: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => { if (/^https?:/.test(request.url()) && new URL(request.url()).origin !== ORIGIN) outbound.push(request.url()); });
  await seed(page, fixture); const recordId = typeof fixture.values.record === "string" ? fixture.values.record : ""; await gotoAccounting(page,`/workflows/${fixture.flow.id}${recordId ? `?record=${encodeURIComponent(recordId)}` : ""}`);
  await expect(page.locator(".workflow-main-header h2")).toHaveText(fixture.flow.steps[0].title);
  for (let index = 0; index < fixture.flow.steps.length; index++) {
    await fillStep(page, fixture, index); await page.getByRole("button", { name: "Next", exact: true }).click();
    await expect(page.locator(".workflow-main-header h2")).toHaveText(index === fixture.flow.steps.length - 1 ? "Review and complete" : fixture.flow.steps[index + 1].title);
  }
  await page.getByRole("button", { name: `Complete ${fixture.flow.title}`, exact: true }).click();
  await expect(page).toHaveURL(/\/workflow-results\/result-/);
  await expect(page.locator("h1")).toHaveText(fixture.flow.completionTitle);
  const receipts = await page.evaluate(() => JSON.parse(localStorage.getItem(Object.keys(localStorage).find(k => k.endsWith(".workflows") && k.startsWith("greenwebb.accounting.preview.v1.")) || "absent") || "{}").receipts);
  expect(receipts[0].flowId).toBe(fixture.flow.id); expect(receipts[0].links.length).toBeGreaterThan(0);
  const changed = await page.evaluate(({ workspaceBefore, storeBefore }) => { const workspace = localStorage.getItem(Object.keys(localStorage).find(k => k.endsWith(".workspace") && k.startsWith("greenwebb.accounting.preview.v1.")) || "absent") || "{}"; const store = JSON.parse(localStorage.getItem(Object.keys(localStorage).find(k => k.endsWith(".workflows") && k.startsWith("greenwebb.accounting.preview.v1.")) || "absent") || "{}"); return workspace !== JSON.stringify(workspaceBefore) || JSON.stringify(store.artifacts || []) !== JSON.stringify(storeBefore.artifacts || []); }, { workspaceBefore: fixture.workspace, storeBefore: fixture.store });
  expect(changed, "A completed browser workflow must update a fictional local record or artifact").toBe(true);
  expect(errors).toEqual([]); expect(outbound).toEqual([]);
});
test("required field and cancel leave entity data unchanged", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "create-customer")!; await seed(page, fixture); await gotoAccounting(page,"/workflows/create-customer");
  const before = await page.evaluate(() => localStorage.getItem(Object.keys(localStorage).find(k => k.endsWith(".workspace") && k.startsWith("greenwebb.accounting.preview.v1.")) || "absent"));
  await page.getByRole("button", { name: "Next", exact: true }).click(); await expect(page.getByRole("alert")).toContainText("required");
  await page.locator('[data-field-id="name"] input').fill("Canceled customer"); await page.getByRole("button", { name: "Cancel", exact: true }).click();
  expect(await page.evaluate(() => localStorage.getItem(Object.keys(localStorage).find(k => k.endsWith(".workspace") && k.startsWith("greenwebb.accounting.preview.v1.")) || "absent"))).toBe(before);
});
test("mobile workflow is visible without page overflow", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "create-invoice")!; await page.setViewportSize({ width: 390, height: 844 }); await seed(page, fixture); await gotoAccounting(page,"/workflows/create-invoice");
  await expect(page.locator("h1")).toBeVisible(); await expect(page.getByRole("button", { name: "Next", exact: true })).toBeVisible();
  const widths = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, viewport: innerWidth })); expect(widths.doc).toBeLessThanOrEqual(widths.viewport + 2);
  await page.getByRole("button", { name: "Open navigation" }).click(); await expect(page.getByRole("link", { name: "Workflows", exact: true })).toBeVisible();
});
test("existing bill detail starts its contextual action with the selected bill", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "approve-bill")!; const recordId = String(fixture.values.record);
  await seed(page, fixture); await gotoAccounting(page,`/records/bills/${recordId}`); await page.getByRole("link", { name: "Approve bill", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/workflows/approve-bill\\?record=${encodeURIComponent(recordId)}`));
  await expect(page.locator('[data-field-id="record"]').getByRole("combobox")).toContainText(fixture.workspace.bills.find(x => x.id === recordId)!.name);
});
test("every module New route opens its matching creation workflow", async ({ page }) => {
  await seed(page, cases[0]);
  for (const spec of moduleSpecs.filter(item => item.createPath)) {
    const flow = createFlowFor(spec.key); expect(flow, `${spec.key} must have a creation workflow`).toBeTruthy();
    await gotoAccounting(page,spec.createPath!); await expect(page).toHaveURL(`${BASE_PATH}/workflows/${flow!.id}`); await expect(page.locator("h1")).toHaveText(flow!.title);
  }
});
test("top-level routes render without white screens", async ({ page }) => {
  await seed(page, cases[0]); const errors: string[] = []; page.on("pageerror", (error) => errors.push(error.message));
  for (const path of ["/", "/invoices", "/banking", "/chart-of-accounts", "/transaction-locking", "/documents", "/settings", "/settings/templates", "/settings/automation", "/reports", "/reports/profit-and-loss", "/records/invoices/invoice-1048"]) {
    await gotoAccounting(page,path); await expect(page.locator("h1")).toBeVisible(); await expect(page.locator("#root")).not.toBeEmpty();
  }
  expect(errors).toEqual([]);
});
