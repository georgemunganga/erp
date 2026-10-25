import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import type { Page } from "@playwright/test";
import { moduleSpecs } from "../src/data";
import { createFlowFor } from "../src/workflows/registry";
import type { FlowDefinition, FlowValues, FlowRow, FlowStore } from "../src/workflows/contracts";
import type { WorkspaceData } from "../src/types";
interface Case { flow: FlowDefinition; workspace: WorkspaceData; store: FlowStore; values: FlowValues; }
const cases: Case[] = JSON.parse(readFileSync("tests/fixtures/workflow-cases.json", "utf8"));
async function seed(page: Page, fixture: Case) {
  await page.addInitScript(({ workspace, store }) => {
    localStorage.clear();
    localStorage.setItem("books-demo-workspace-v1", JSON.stringify(workspace));
    localStorage.setItem("books-workflows-browser-local-v2", JSON.stringify(store));
  }, { workspace: fixture.workspace, store: fixture.store });
}
async function fillStep(page: Page, fixture: Case, index: number) {
  for (const field of fixture.flow.steps[index].fields) {
    const block = page.locator(`[data-field-id="${field.id}"]`); const value = fixture.values[field.id];
    if (field.type === "record" || field.type === "select") { const select = block.locator("select"); const option = value ? String(value) : await select.locator('option:not([value=""])').first().getAttribute("value"); if (option) await select.selectOption(option); if (field.required) await expect(select).not.toHaveValue(""); }
    else if (field.id === "amount" && await block.locator("input").count() === 0) { await expect(block).toContainText("Calculated from line items"); }
    else if (["text", "email", "date", "number"].includes(field.type)) await block.locator("input").fill(value === undefined ? "" : String(value));
    else if (field.type === "textarea" || field.type === "csv") await block.locator("textarea").fill(String(value || ""));
    else if (field.type === "checkbox") await block.locator("input[type=checkbox]").setChecked(value === true);
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
        else { await row.locator("select").selectOption(String(rows[i].account)); await row.locator("input").nth(0).fill(String(rows[i].debit)); await row.locator("input").nth(1).fill(String(rows[i].credit)); }
      }
    }
  }
}
for (const fixture of cases) test(`finish ${fixture.flow.id}`, async ({ page }) => {
  const errors: string[] = []; const outbound: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => { if (/^https?:/.test(request.url()) && !request.url().startsWith("http://127.0.0.1:3000")) outbound.push(request.url()); });
  await seed(page, fixture); const recordId = typeof fixture.values.record === "string" ? fixture.values.record : ""; await page.goto(`/workflows/${fixture.flow.id}${recordId ? `?record=${encodeURIComponent(recordId)}` : ""}`);
  await expect(page.locator(".workflow-main-header h2")).toHaveText(fixture.flow.steps[0].title);
  for (let index = 0; index < fixture.flow.steps.length; index++) {
    await fillStep(page, fixture, index); await page.getByRole("button", { name: "Next", exact: true }).click();
    await expect(page.locator(".workflow-main-header h2")).toHaveText(index === fixture.flow.steps.length - 1 ? "Review and complete" : fixture.flow.steps[index + 1].title);
  }
  await page.getByRole("button", { name: `Complete ${fixture.flow.title}`, exact: true }).click();
  await expect(page).toHaveURL(/\/workflow-results\/result-/);
  await expect(page.locator("h1")).toHaveText(fixture.flow.completionTitle);
  const receipts = await page.evaluate(() => JSON.parse(localStorage.getItem("books-workflows-browser-local-v2") || "{}").receipts);
  expect(receipts[0].flowId).toBe(fixture.flow.id); expect(receipts[0].links.length).toBeGreaterThan(0);
  const changed = await page.evaluate(({ workspaceBefore, storeBefore }) => { const workspace = localStorage.getItem("books-demo-workspace-v1") || "{}"; const store = JSON.parse(localStorage.getItem("books-workflows-browser-local-v2") || "{}"); return workspace !== JSON.stringify(workspaceBefore) || JSON.stringify(store.artifacts || []) !== JSON.stringify(storeBefore.artifacts || []); }, { workspaceBefore: fixture.workspace, storeBefore: fixture.store });
  expect(changed, "A completed browser workflow must update a fictional local record or artifact").toBe(true);
  expect(errors).toEqual([]); expect(outbound).toEqual([]);
});
test("required field and cancel leave entity data unchanged", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "create-customer")!; await seed(page, fixture); await page.goto("/workflows/create-customer");
  const before = await page.evaluate(() => localStorage.getItem("books-demo-workspace-v1"));
  await page.getByRole("button", { name: "Next", exact: true }).click(); await expect(page.getByRole("alert")).toContainText("required");
  await page.locator('[data-field-id="name"] input').fill("Canceled customer"); await page.getByRole("button", { name: "Cancel", exact: true }).click();
  expect(await page.evaluate(() => localStorage.getItem("books-demo-workspace-v1"))).toBe(before);
});
test("mobile workflow is visible without page overflow", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "create-invoice")!; await page.setViewportSize({ width: 390, height: 844 }); await seed(page, fixture); await page.goto("/workflows/create-invoice");
  await expect(page.locator("h1")).toBeVisible(); await expect(page.getByRole("button", { name: "Next", exact: true })).toBeVisible();
  const widths = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, viewport: innerWidth })); expect(widths.doc).toBeLessThanOrEqual(widths.viewport + 2);
  await page.getByRole("button", { name: "Open navigation" }).click(); await expect(page.getByRole("link", { name: "Workflows", exact: true })).toBeVisible();
});
test("existing bill detail starts its contextual action with the selected bill", async ({ page }) => {
  const fixture = cases.find(item => item.flow.id === "approve-bill")!; const recordId = String(fixture.values.record);
  await seed(page, fixture); await page.goto(`/records/bills/${recordId}`); await page.getByRole("link", { name: "Approve bill", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/workflows/approve-bill\\?record=${encodeURIComponent(recordId)}`));
  await expect(page.locator('[data-field-id="record"] select')).toHaveValue(recordId);
});
test("every module New route opens its matching creation workflow", async ({ page }) => {
  await seed(page, cases[0]);
  for (const spec of moduleSpecs.filter(item => item.createPath)) {
    const flow = createFlowFor(spec.key); expect(flow, `${spec.key} must have a creation workflow`).toBeTruthy();
    await page.goto(spec.createPath!); await expect(page).toHaveURL(`/workflows/${flow!.id}`); await expect(page.locator("h1")).toHaveText(flow!.title);
  }
});
test("top-level routes render without white screens", async ({ page }) => {
  await seed(page, cases[0]); const errors: string[] = []; page.on("pageerror", (error) => errors.push(error.message));
  for (const path of ["/", "/invoices", "/banking", "/chart-of-accounts", "/transaction-locking", "/documents", "/settings", "/settings/templates", "/settings/automation", "/reports", "/reports/profit-and-loss", "/records/invoices/invoice-1048"]) {
    await page.goto(path); await expect(page.locator("h1")).toBeVisible(); await expect(page.locator("#root")).not.toBeEmpty();
  }
  expect(errors).toEqual([]);
});
