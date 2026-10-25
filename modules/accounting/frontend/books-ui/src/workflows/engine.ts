import { moduleByKey } from "../data";
import type { DataKey, DemoRecord, DemoStatus, WorkspaceData } from "../types";
import type { FlowDefinition, FlowReceipt, FlowRow, FlowStore, FlowValues, LocalArtifact } from "./contracts";
import { rowValues, validateValues } from "./validation";
import { evaluateCriteria } from "./criteria";

export interface ResourceOption { id: string; name: string; code?: string; party?: string; amount: number; status: string; values?: FlowValues; }
const prefixes: Record<DataKey, string> = { items: "ITM", customers: "CUS", quotes: "QT", invoices: "INV", recurringInvoices: "RI", paymentsReceived: "RCPT", creditNotes: "CN", vendors: "VEN", expenses: "EXP", bills: "BILL", purchaseOrders: "PO", recurringBills: "RB", paymentsMade: "PAY", vendorCredits: "VC", batchPayments: "BATCH", projects: "PRJ", timesheets: "TIME", journals: "JRN", budgets: "BUD" };
const text = (values: FlowValues, ...keys: string[]) => { for (const key of keys) if (values[key] !== undefined && values[key] !== "") return String(values[key]); return ""; };
const number = (values: FlowValues, ...keys: string[]) => { for (const key of keys) if (values[key] !== undefined && values[key] !== "") return Number(values[key]) || 0; return 0; };
const round = (amount: number) => Math.round((amount + Number.EPSILON) * 100) / 100;
export function outstanding(record: DemoRecord) { return Math.max(0, round(record.amount - (record.paidAmount ?? (record.status === "Paid" ? record.amount : 0)) - (record.creditedAmount ?? 0))); }
export function listResources(workspace: WorkspaceData, store: FlowStore, collection: string): ResourceOption[] {
  if (collection in workspace) return workspace[collection as DataKey].map((row) => ({ id: row.id, name: row.name, code: row.code, party: row.party, amount: ["invoices", "bills", "creditNotes", "vendorCredits"].includes(collection) ? outstanding(row) : row.amount, status: row.status, values: { ...(row.details as FlowValues), name: row.name, date: row.date, dueDate: row.dueDate || "", amount: row.amount, email: row.email || "", phone: row.phone || "", category: row.category || "", description: row.description || "", party: (row.details?.party as string) || row.party } }));
  return store.artifacts.filter((item) => item.collection === collection).map((item) => ({ id: item.id, name: item.name, code: String(item.values.code ?? ""), party: String(item.values.party ?? ""), amount: Number(item.values.amount ?? item.values.balance ?? item.values.openingBalance ?? 0), status: item.status, values: item.values }));
}
export function calculatedTotal(values: FlowValues) {
  const lines = rowValues(values.lines ?? values.items);
  if (!lines.length) return Math.max(0, number(values, "amount", "budget", "rate", "salesRate", "paymentAmount", "hours"));
  const subtotal = lines.reduce((sum, row) => sum + Math.max(0, Number(row.quantity || 0)) * Math.max(0, Number(row.rate || 0)), 0);
  const discount = Math.min(subtotal, Math.max(0, number(values, "discount")));
  const taxRate = Math.max(0, number(values, "taxRate"));
  const rowTax = lines.reduce((sum, row) => sum + Math.max(0, Number(row.quantity || 0)) * Math.max(0, Number(row.rate || 0)) * Math.max(0, Number(row.taxRate || 0)) / 100, 0);
  const globalTax = (subtotal - discount) * taxRate / 100;
  return round(subtotal - discount + (values.taxInclusive ? 0 : rowTax + globalTax) + number(values, "shipping") + number(values, "adjustment"));
}
function nextCode(key: DataKey, records: DemoRecord[]) {
  let max = 100;
  for (const record of records) { const match = record.code.match(/(\d+)$/); if (match) max = Math.max(max, Number(match[1])); }
  return `${prefixes[key]}-${String(max + 1).padStart(4, "0")}`;
}
function findEntity(data: WorkspaceData, key: DataKey | undefined, id: string) {
  const record = key ? data[key].find((item) => item.id === id) : undefined;
  if (!record) throw new Error("Choose an existing record before continuing.");
  return record;
}
function findArtifact(store: FlowStore, id: string) { const record = store.artifacts.find((item) => item.id === id); if (!record) throw new Error("Choose an existing workspace resource before continuing."); return record; }
function resolveParty(data: WorkspaceData, store: FlowStore, value: string) {
  for (const records of Object.values(data)) { const match = records.find((item) => item.id === value); if (match) return match.name; }
  return store.artifacts.find((item) => item.id === value)?.name ?? value;
}
function allocationRows(values: FlowValues): FlowRow[] { const arrays = Object.entries(values).filter(([key]) => /allocation|bill|invoice|transaction/i.test(key)).map(([, value]) => rowValues(value)); return arrays.find((rows) => rows.length && rows[0].recordId !== undefined) ?? rowValues(values.allocations); }
function assertTransition(flow: FlowDefinition, record: DemoRecord) {
  const sources: Record<string, DemoStatus[]> = {
    "send-quote": ["Draft"], "accept-quote": ["Sent"], "decline-quote": ["Draft", "Sent"], "send-invoice": ["Draft"],
    "submit-purchase-order": ["Draft"], "approve-purchase-order": ["Open", "Sent"], "approve-bill": ["Open", "Pending"],
    "publish-journal": ["Draft"], "pause-recurring-invoice": ["Active"], "pause-recurring-bill": ["Active"],
    "resume-recurring-invoice": ["Paused"], "resume-recurring-bill": ["Paused"], "close-project": ["Active"],
  };
  const allowed = sources[flow.id];
  if (allowed && !allowed.includes(record.status)) throw new Error(`${record.code} must be ${allowed.join(" or ")} before ${flow.title.toLowerCase()}.`);
}
function attachHistory(record: DemoRecord, at: string, action: string) { record.history = [...(record.history ?? [{ at: `${record.date}T09:00:00.000Z`, action: "Created" }]), { at, action }]; }
function parseCsv(input: string): string[][] {
  const rows: string[][] = []; let row: string[] = [], cell = "", quoted = false;
  for (let index = 0; index < input.length; index++) { const char = input[index]; if (char === '"') { if (quoted && input[index + 1] === '"') { cell += '"'; index++; } else quoted = !quoted; } else if (char === "," && !quoted) { row.push(cell.trim()); cell = ""; } else if ((char === "\n" || char === "\r") && !quoted) { if (char === "\r" && input[index + 1] === "\n") index++; row.push(cell.trim()); if (row.some(Boolean)) rows.push(row); row = []; cell = ""; } else cell += char; }
  if (quoted) throw new Error("CSV contains an unclosed quoted field.");
  row.push(cell.trim()); if (row.some(Boolean)) rows.push(row);
  return rows;
}

export function executeWorkflow(flow: FlowDefinition, values: FlowValues, currentData: WorkspaceData, currentStore: FlowStore): { workspace: WorkspaceData; store: FlowStore; receipt: FlowReceipt } {
  const errors = validateValues(flow, values);
  if (Object.keys(errors).length) throw new Error(Object.values(errors)[0]);
  const workspace = structuredClone(currentData);
  const store = structuredClone(currentStore);
  const at = new Date().toISOString();
  const links: FlowReceipt["links"] = [];
  const effect = flow.effect;
  const selectedId = text(values, "record", "sourceRecord", "profile", "invoice", "bill", "creditNote", "document", "rule", "template");
  const source = effect.entity && selectedId ? workspace[effect.entity].find((item) => item.id === selectedId) : undefined;
  if (["create", "journal", "edit"].includes(effect.kind) && effect.entity) {
    const group = ["invoices", "quotes", "creditNotes", "paymentsReceived", "recurringInvoices"].includes(effect.entity) ? "Sales" : ["bills", "expenses", "purchaseOrders", "vendorCredits", "paymentsMade", "recurringBills", "batchPayments"].includes(effect.entity) ? "Purchases" : "Accountant";
    const locked = store.artifacts.find((item) => item.collection === "settings" && item.values.lockActive === true && item.values.module === group);
    if (locked && text(values, "date") && text(values, "date") <= String(locked.values.date)) throw new Error(`The local ${group} period is locked through ${locked.values.date}. Unlock it before changing this date.`);
  }
  const entityLink = (key: DataKey, record: DemoRecord) => links.push({ label: `${record.code} · ${record.name}`, path: `/records/${key}/${record.id}` });
  const resourceLink = (record: LocalArtifact) => links.push({ label: record.name, path: `/workflow-resources/${record.id}` });
  const create = (key: DataKey, input: FlowValues, status?: DemoStatus, sourceId?: string): DemoRecord => {
    const defaults: Partial<Record<DataKey, DemoStatus>> = { customers: "Active", vendors: "Active", items: "Active", projects: "Active", recurringInvoices: "Active", recurringBills: "Active", expenses: "Recorded", timesheets: "Recorded", paymentsReceived: "Paid", paymentsMade: "Paid", creditNotes: "Open", vendorCredits: "Open", bills: "Open", purchaseOrders: "Draft" };
    const lines = rowValues(input.lines ?? input.items); const calculated = calculatedTotal(input);
    const total = lines.length ? calculated : Math.max(0, number(input, "amount", "budget", "rate", "salesRate", "paymentAmount", "hours") || calculated);
    const record: DemoRecord = { id: `${key}-${crypto.randomUUID()}`, code: nextCode(key, workspace[key]), name: text(input, "name", "subject", "title", "displayName") || `${moduleByKey[key].entityLabel} ${nextCode(key, workspace[key])}`, party: resolveParty(workspace, store, text(input, "party", "customer", "vendor", "project", "reference")), date: text(input, "date", "startDate", "paymentDate") || at.slice(0, 10), dueDate: text(input, "dueDate", "endDate", "nextDate") || undefined, amount: total, status: status ?? defaults[key] ?? "Draft", category: text(input, "category", "frequency", "paymentMode", "billingMethod") || undefined, description: text(input, "description", "notes", "remarks") || undefined, email: text(input, "email") || undefined, phone: text(input, "phone") || undefined, sourceId, details: structuredClone(input), history: [{ at, action: `Created · ${flow.title}` }] };
    if (key === "items") record.stock = number(input, "quantity", "openingStock");
    if (key === "timesheets") { record.amount = number(input, "quantity", "hours"); record.party = resolveParty(workspace, store, text(input, "project")) || record.party; }
    workspace[key].unshift(record); entityLink(key, record); return record;
  };
  const artifact = (collection: string, input: FlowValues, status = "Active", existingId?: string) => {
    const existing = existingId ? store.artifacts.find((item) => item.id === existingId && item.collection === collection) : undefined;
    if (existing) { existing.name = text(input, "name", "title") || existing.name; existing.values = { ...existing.values, ...structuredClone(input) }; existing.status = status; existing.history.push({ at, action: flow.title }); resourceLink(existing); return existing; }
    const record: LocalArtifact = { id: `${collection}-${crypto.randomUUID()}`, collection, name: text(input, "name", "title", "subject", "filename", "category") || flow.title, status, values: structuredClone(input), history: [{ at, action: flow.title }] };
    store.artifacts.unshift(record); resourceLink(record); return record;
  };
  const applyPayment = (key: DataKey, record: DemoRecord, amount: number, credit = false) => {
    if (["Void", "Draft", "Declined"].includes(record.status)) throw new Error(`${record.code} must be open before allocating a payment or credit.`);
    if (amount <= 0 || !Number.isFinite(amount) || amount - outstanding(record) > 0.005) throw new Error(`Allocation for ${record.code} must be positive and no more than its outstanding balance.`);
    if (credit) record.creditedAmount = round((record.creditedAmount ?? 0) + amount); else record.paidAmount = round((record.paidAmount ?? (record.status === "Paid" ? record.amount : 0)) + amount);
    record.status = outstanding(record) < 0.005 ? "Paid" : "Partially Paid";
    attachHistory(record, at, `${credit ? "Credit applied" : "Payment recorded"} · ${amount.toFixed(2)}`);
    entityLink(key, record);
  };

  switch (effect.kind) {
    case "create": {
      if (!effect.entity) throw new Error("This create workflow has no record collection.");
      if (["paymentsReceived", "paymentsMade", "batchPayments"].includes(effect.entity)) {
        const allocationKey: DataKey = effect.entity === "paymentsReceived" ? "invoices" : "bills";
        const rows = allocationRows(values); if (!rows.length) throw new Error("Allocate the payment to at least one open document.");
        const total = rows.reduce((sum, row) => sum + Number(row.amount), 0);
        const parties = new Set(rows.map((row) => findEntity(workspace, allocationKey, String(row.recordId)).party));
        if (effect.entity !== "batchPayments" && parties.size > 1) throw new Error("Select documents for one customer or vendor per payment.");
        for (const row of rows) applyPayment(allocationKey, findEntity(workspace, allocationKey, String(row.recordId)), Number(row.amount));
        create(effect.entity, { ...values, amount: total, party: [...parties].join(", ") }, "Paid");
      } else create(effect.entity, values, effect.status);
      break;
    }
    case "edit": {
      const record = findEntity(workspace, effect.entity, selectedId);
      if (["Paid", "Void", "Converted", "Published"].includes(record.status)) throw new Error("This completed record cannot be edited. Use its correction workflow instead.");
      const updated = { ...record.details, ...values } as FlowValues;
      record.name = text(values, "name", "subject") || record.name;
      if (values.party || values.customer || values.vendor) record.party = resolveParty(workspace, store, text(values, "party", "customer", "vendor"));
      if (values.date) record.date = String(values.date);
      if (values.dueDate) record.dueDate = String(values.dueDate);
      if (values.amount !== undefined || values.lines !== undefined) { const amount = calculatedTotal(updated); if (amount < (record.paidAmount ?? 0) + (record.creditedAmount ?? 0)) throw new Error("Total cannot be lower than existing payment/credit allocations."); record.amount = amount; }
      record.email = text(values, "email") || record.email; record.phone = text(values, "phone") || record.phone;
      record.category = text(values, "category", "frequency") || record.category;
      record.description = text(values, "description", "notes") || record.description;
      record.details = updated; attachHistory(record, at, "Edited"); entityLink(effect.entity!, record); break;
    }
    case "status": {
      const record = findEntity(workspace, effect.entity, selectedId);
      assertTransition(flow, record);
      const status = effect.status ?? "Approved";
      if (effect.entity === "journals" && /publish/i.test(effect.action ?? flow.id)) {
        const journalLines = rowValues(record.details?.lines); const debit = journalLines.reduce((sum, row) => sum + Number(row.debit || 0), 0); const credit = journalLines.reduce((sum, row) => sum + Number(row.credit || 0), 0);
        if (debit <= 0 || Math.abs(debit - credit) > 0.005 || journalLines.length < 2) throw new Error("Only a balanced journal can be published.");
        for (const line of journalLines) { const account = findArtifact(store, String(line.account)); account.values.balance = round(Number(account.values.balance ?? account.values.openingBalance ?? 0) + Number(line.debit || 0) - Number(line.credit || 0)); account.history.push({ at, action: `Journal ${record.code} published` }); }
      }
      if (status === "Void" && (record.paidAmount || record.creditedAmount || record.status === "Paid")) throw new Error("Remove or reverse existing allocations before voiding this record.");
      if (record.status === status) throw new Error("This record already has that status.");
      record.status = status; record.details = { ...record.details, ...values }; attachHistory(record, at, flow.title); entityLink(effect.entity!, record);
      if (/send|email|portal/i.test(flow.id)) artifact("messages", { ...values, name: `${flow.title} · ${record.code}`, record: record.id, delivery: "Browser simulation; not delivered externally" }, "Prepared");
      break;
    }
    case "convert": {
      const record = findEntity(workspace, effect.entity, selectedId);
      if (!effect.target) throw new Error("Choose a destination record type.");
      if (["Converted", "Void", "Declined"].includes(record.status)) throw new Error("This record has already been converted or is unavailable for conversion.");
      if (flow.id === "convert-quote-to-invoice" && record.status !== "Accepted") throw new Error("Accept the quote before converting it to an invoice.");
      if (flow.id === "convert-purchase-order-to-bill" && record.status !== "Approved") throw new Error("Approve the purchase order before converting it to a bill.");
      if (flow.id === "convert-expense-to-billable" && (record.status === "Billed" || record.details?.billable !== true)) throw new Error("Mark the expense as billable and keep it unbilled before conversion.");
      const sourceParty = record.details?.party ? resolveParty(workspace, store, String(record.details.party)) : record.party;
      const converted = create(effect.target, { ...(record.details as FlowValues), name: record.name, party: sourceParty, date: at.slice(0, 10), amount: record.amount, ...values }, effect.status, record.id);
      record.status = flow.id === "convert-expense-to-billable" ? "Billed" : "Converted"; attachHistory(record, at, `Converted to ${converted.code}`); entityLink(effect.entity!, record); break;
    }
    case "payment": {
      if (!effect.entity) throw new Error("Select the payment document type.");
      let rows = allocationRows(values);
      if (!rows.length && source) rows = [{ recordId: source.id, amount: number(values, "amount", "paymentAmount") }];
      if (!rows.length) throw new Error("Select a document and a positive allocation.");
      const total = round(rows.reduce((sum, row) => sum + Number(row.amount), 0));
      const stated = number(values, "amount", "paymentAmount");
      if (stated > 0 && Math.abs(stated - total) > 0.005) throw new Error("The payment amount must equal the sum of its allocations.");
      const allocatedIds = [...new Set(rows.map((row) => String(row.recordId)))].sort();
      const selectedRecord = text(values, "record", "bill", "invoice");
      if (selectedRecord && (allocatedIds.length !== 1 || allocatedIds[0] !== selectedRecord)) throw new Error("Payment allocation must belong to the selected invoice or bill.");
      const parties = new Set(rows.map((row) => findEntity(workspace, effect.entity, String(row.recordId)).party));
      if (parties.size > 1 && effect.target !== "batchPayments") throw new Error("Allocate one payment only to documents for the same customer or vendor.");
      const selectedRecords = Array.isArray(values.records) ? values.records.map(String) : [];
      if (effect.target === "batchPayments" && selectedRecords.length) {
        const normalized = [...new Set(selectedRecords)].sort();
        if (normalized.length !== allocatedIds.length || normalized.some((id, index) => id !== allocatedIds[index])) throw new Error("Every selected document must have exactly one payment allocation; remove unallocated selections.");
      }
      for (const row of rows) applyPayment(effect.entity, findEntity(workspace, effect.entity, String(row.recordId)), Number(row.amount));
      create(effect.target ?? (effect.entity === "invoices" ? "paymentsReceived" : "paymentsMade"), { ...values, amount: total, party: [...parties].join(", "), name: text(values, "name") || `Payment · ${total.toFixed(2)}` }, "Paid", selectedId || undefined);
      if (effect.target === "batchPayments") for (const party of parties) { const ownRows = rows.filter((row) => findEntity(workspace, effect.entity, String(row.recordId)).party === party); create("paymentsMade", { ...values, name: `Batch payment · ${party}`, party, amount: ownRows.reduce((sum, row) => sum + Number(row.amount), 0), allocations: ownRows }, "Paid"); }
      break;
    }
    case "credit": {
      const credit = findEntity(workspace, effect.entity, selectedId);
      const rows = allocationRows(values); const refund = /refund/i.test(flow.id + (effect.action ?? ""));
      if (refund) {
        const amount = number(values, "amount", "refundAmount");
        if (amount <= 0 || amount - outstanding(credit) > 0.005) throw new Error("Refund must be positive and no larger than unused credit.");
        credit.paidAmount = round((credit.paidAmount ?? 0) + amount); credit.status = outstanding(credit) === 0 ? "Refunded" : "Open";
        artifact("refunds", { ...values, name: `Refund · ${credit.code}`, amount, source: credit.id }, "Recorded");
      } else {
        if (!rows.length || !effect.target) throw new Error("Select an open invoice or bill and allocate a credit amount.");
        const total = rows.reduce((sum, row) => sum + Number(row.amount), 0);
        if (total - outstanding(credit) > 0.005) throw new Error("Credit allocation exceeds the unused credit balance.");
        for (const row of rows) { const destination = findEntity(workspace, effect.target, String(row.recordId)); if (destination.party !== credit.party) throw new Error("The credit and selected document must belong to the same customer/vendor."); applyPayment(effect.target, destination, Number(row.amount), true); }
        credit.paidAmount = round((credit.paidAmount ?? 0) + total); credit.status = outstanding(credit) === 0 ? "Applied" : "Open";
      }
      attachHistory(credit, at, flow.title); entityLink(effect.entity!, credit); break;
    }
    case "recurring": {
      const profile = findEntity(workspace, effect.entity, selectedId);
      if (profile.status !== "Active") throw new Error("Resume the recurring profile before generating its next document.");
      const target = effect.target ?? (effect.entity === "recurringInvoices" ? "invoices" : "bills");
      const generated = create(target, { ...(profile.details as FlowValues), name: profile.name, party: profile.party, amount: profile.amount, date: text(values, "date", "nextDate") || profile.dueDate || at.slice(0, 10), ...values }, target === "bills" ? "Open" : "Draft", profile.id);
      const next = new Date(`${generated.date}T12:00:00Z`); const frequency = text(profile.details as FlowValues ?? {}, "frequency") || profile.category || "Monthly";
      if (/week/i.test(frequency)) next.setUTCDate(next.getUTCDate() + 7); else if (/year/i.test(frequency)) next.setUTCFullYear(next.getUTCFullYear() + 1); else next.setUTCMonth(next.getUTCMonth() + (/quarter/i.test(frequency) ? 3 : 1));
      profile.dueDate = next.toISOString().slice(0, 10); attachHistory(profile, at, `Generated ${generated.code}; next date ${profile.dueDate}`); entityLink(effect.entity!, profile); break;
    }
    case "journal": {
      const lines = rowValues(values.lines ?? values.journalLines ?? values.journalEntries);
      const debit = lines.reduce((sum, row) => sum + Number(row.debit || 0), 0); const credit = lines.reduce((sum, row) => sum + Number(row.credit || 0), 0);
      if (lines.length < 2 || debit <= 0 || Math.abs(debit - credit) > 0.005 || lines.some((row) => !row.account || (Number(row.debit) > 0 && Number(row.credit) > 0))) throw new Error("Journal must balance with at least two accounts and separate debit/credit lines.");
      const reversed = effect.action === "reverse";
      if (reversed) { const original = findEntity(workspace, "journals", selectedId); if (!["Published", "Recorded"].includes(original.status)) throw new Error("Publish the original journal before reversing it."); original.status = "Reversed"; attachHistory(original, at, "Reversal created"); entityLink("journals", original); }
      const opening = effect.action === "opening-balance";
      const posted = reversed || opening;
      const journal = create("journals", { ...values, amount: debit, lines }, posted ? "Published" : effect.status ?? "Draft", reversed ? selectedId : undefined);
      if (posted) for (const line of lines) { const account = findArtifact(store, String(line.account)); account.values.balance = round(Number(account.values.balance ?? account.values.openingBalance ?? 0) + Number(line.debit || 0) - Number(line.credit || 0)); account.history.push({ at, action: `Journal ${journal.code} posted locally` }); }
      break;
    }
    case "time-invoice": {
      const ids = Array.isArray(values.records) ? values.records.map(String) : selectedId ? [selectedId] : [];
      const entries = workspace.timesheets.filter((item) => ids.includes(item.id));
      if (!entries.length || entries.some((item) => item.status === "Billed")) throw new Error("Select at least one unbilled time entry.");
      if (new Set(entries.map((item) => item.party)).size > 1) throw new Error("Choose time entries from a single project.");
      if (entries.some((item) => item.details?.billable === false || item.category === "Non-billable")) throw new Error("Choose billable time entries only.");
      const rate = number(values, "rate", "hourlyRate");
      const lines = entries.map((item) => ({ description: item.name, quantity: item.amount, rate: rate || Number(item.details?.amount || 100) }));
      const invoice = create("invoices", { ...values, name: text(values, "name") || `Project billing · ${entries[0].party}`, lines, party: text(values, "party", "customer") || workspace.projects.find((item) => item.name === entries[0].party)?.party || entries[0].party });
      entries.forEach((item) => { item.status = "Billed"; item.sourceId = invoice.id; attachHistory(item, at, `Billed in ${invoice.code}`); entityLink("timesheets", item); }); break;
    }
    case "inventory": {
      const item = findEntity(workspace, "items", selectedId); const change = number(values, "quantity", "adjustment", "quantityChange", "quantityDelta");
      if (!change) throw new Error("Enter a non-zero stock adjustment.");
      if ((item.stock ?? 0) + change < 0) throw new Error("Stock adjustment cannot produce a negative quantity.");
      item.stock = (item.stock ?? 0) + change; attachHistory(item, at, `Stock adjusted by ${change}; available ${item.stock}`); artifact("stockAdjustments", { ...values, name: `${item.name} · adjustment`, quantity: change, closingStock: item.stock }, "Recorded"); entityLink("items", item); break;
    }
    case "artifact": {
      const collection = effect.collection ?? "settings";
      const action = effect.action ?? "";
      if (["lock-transactions", "unlock-transactions"].includes(flow.id)) {
        const module = text(values, "module") || "Accountant";
        const old = store.artifacts.find((item) => item.collection === "settings" && item.values.module === module && item.values.lockActive !== undefined);
        artifact("settings", { ...values, name: `${module} period lock`, lockActive: flow.id === "lock-transactions" }, flow.id === "lock-transactions" ? "Locked" : "Unlocked", old?.id); break;
      }
      const oldSetting = collection === "settings" && !/user|role|term|tax|currency/i.test(action) ? store.artifacts.find((item) => item.collection === collection && item.values.action === action) : undefined;
      const existingName = selectedId ? store.artifacts.find((item) => item.id === selectedId)?.name : undefined;
      const input: FlowValues = { ...values, action, name: text(values, "name", "roleName", "connectionName", "accountName", "organizationName", "title") || existingName || flow.title };
      if (collection === "bankTransactions") { input.amount = /withdraw|debit|expense/i.test(text(values, "direction")) ? -Math.abs(number(values, "amount")) : number(values, "amount"); }
      const saved = artifact(collection, input, /disable|inactive|pause/i.test(action) || values.active === false || values.enabled === false ? "Inactive" : /draft/i.test(action) ? "Draft" : "Active", selectedId || oldSetting?.id);
      if (collection === "templates" && (/default/i.test(action + flow.id) || values.isDefault)) { for (const item of store.artifacts.filter((item) => item.collection === "templates" && (item.values.templateType ?? item.values.group) === (saved.values.templateType ?? saved.values.group))) item.values.isDefault = item.id === saved.id; }
      if (collection === "documents") saved.status = "Inbox";
      if (collection === "bankTransactions") saved.status = values.category && values.category !== "Uncategorized" ? "Categorized" : "Uncategorized";
      if (collection === "integrations") saved.status = "Simulated";
      if (/timer/i.test(flow.id)) { saved.values.timerState = "Running"; saved.values.startedAt = at; saved.status = "Running"; }
      break;
    }
    case "artifact-status": {
      const record = findArtifact(store, selectedId);
      if (flow.id === "set-default-template") { const type = record.values.templateType ?? record.values.group; for (const template of store.artifacts.filter((item) => item.collection === "templates" && (item.values.templateType ?? item.values.group) === type)) template.values.isDefault = template.id === record.id; record.values.isDefault = true; record.status = "Default"; }
      else if (flow.id === "stop-timer") { if (record.values.timerState !== "Running") throw new Error("Choose a running timer."); const hours = number(values, "quantity"); if (hours <= 0) throw new Error("Elapsed hours must be positive."); record.status = "Stopped"; record.values.timerState = "Stopped"; create("timesheets", { ...record.values, ...values, quantity: hours, name: text(values, "description") || "Timed work", project: String(record.values.project || "") }, "Recorded", record.id); }
      else record.status = /disable/i.test(effect.action ?? "") ? "Inactive" : effect.action || text(values, "status") || "Completed";
      record.values = { ...record.values, ...values }; record.history.push({ at, action: flow.title }); resourceLink(record); break;
    }
    case "bank-match": {
      if (/undo/i.test(effect.action ?? flow.id)) {
        const transaction = findArtifact(store, selectedId); if (transaction.status !== "Matched" || transaction.values.reconciled) throw new Error("Choose a matched, unreconciled transaction to undo.");
        const targetKey = String(transaction.values.matchedCollection) as DataKey;
        const matchedRows = rowValues(transaction.values.matchedAllocations);
        for (const row of matchedRows) { const target = findEntity(workspace, targetKey, String(row.recordId)); target.paidAmount = Math.max(0, round((target.paidAmount ?? (target.status === "Paid" ? target.amount : 0)) - Number(row.amount))); target.status = outstanding(target) === 0 ? (target.paidAmount ? "Paid" : "Open") : "Partially Paid"; attachHistory(target, at, "Bank match reversed"); entityLink(targetKey, target); }
        const paymentKey = targetKey === "invoices" ? "paymentsReceived" : "paymentsMade"; const payment = workspace[paymentKey].find((item) => item.id === transaction.values.paymentId); if (payment) { payment.status = "Void"; attachHistory(payment, at, "Bank match reversed"); }
        transaction.status = "Uncategorized"; delete transaction.values.targetRecord; transaction.history.push({ at, action: "Match undone" }); resourceLink(transaction); break;
      }
      const transaction = findArtifact(store, selectedId); const key = effect.entity ?? "invoices";
      if (transaction.status === "Matched") throw new Error("This bank transaction is already matched.");
      const amount = Math.abs(Number(transaction.values.amount) || 0);
      const direction = text(transaction.values as FlowValues, "direction").toLowerCase();
      const deposit = Number(transaction.values.amount) > 0 || /deposit|credit|inflow/i.test(direction);
      if ((key === "invoices") !== deposit) throw new Error(`Use ${deposit ? "Match deposit to invoice" : "Match withdrawal to bill"} for this bank transaction.`);
      const rows = allocationRows(values); const targetId = text(values, "targetRecord", "invoice", "bill");
      if (!rows.length || !rows.some((row) => String(row.recordId) === targetId)) throw new Error("Add the selected document to the allocation table and enter its allocation amount.");
      if (Math.abs(round(rows.reduce((sum, row) => sum + Number(row.amount), 0)) - amount) > 0.005) throw new Error("The document allocations must equal the bank transaction amount.");
      const targets = rows.map((row) => findEntity(workspace, key, String(row.recordId))); const parties = new Set(targets.map((item) => item.party));
      if (parties.size > 1) throw new Error("One bank transaction may only match documents for one customer or vendor.");
      for (let index = 0; index < rows.length; index++) applyPayment(key, targets[index], Number(rows[index].amount));
      transaction.status = "Matched"; transaction.values.targetRecord = targetId; transaction.values.matchedCollection = key; transaction.values.matchedAmount = amount; transaction.values.matchedAllocations = rows; transaction.history.push({ at, action: `Matched ${rows.length} document${rows.length === 1 ? "" : "s"}` }); const payment = create(key === "invoices" ? "paymentsReceived" : "paymentsMade", { name: `Bank match · ${targets[0].party}`, party: targets[0].party, amount, date: String(transaction.values.date || at.slice(0, 10)), allocations: rows }, "Paid", targetId); transaction.values.paymentId = payment.id; resourceLink(transaction); break;
    }
    case "bank-categorize": {
      const transaction = findArtifact(store, selectedId); if (transaction.status === "Matched") throw new Error("A matched transaction cannot be categorized again.");
      if (!/undo|uncategor/i.test(flow.id) && !text(values, "category", "account")) throw new Error("Choose an account/category.");
      if (transaction.values.reconciled) throw new Error("Undo the reconciliation before changing this transaction.");
      transaction.status = /undo|uncategor/i.test(flow.id) ? "Uncategorized" : "Categorized"; transaction.values = { ...transaction.values, ...values }; transaction.history.push({ at, action: flow.title }); resourceLink(transaction); break;
    }
    case "bank-reconcile": {
      if (/undo/i.test(effect.action ?? flow.id)) { const reconciliation = findArtifact(store, selectedId); if (reconciliation.status !== "Reconciled") throw new Error("This reconciliation is no longer active."); const ids = Array.isArray(reconciliation.values.transactionIds) ? reconciliation.values.transactionIds.map(String) : []; store.artifacts.filter((item) => ids.includes(item.id)).forEach((item) => { item.values.reconciled = false; item.history.push({ at, action: "Reconciliation undone" }); }); reconciliation.status = "Reversed"; reconciliation.history.push({ at, action: "Reconciliation undone" }); resourceLink(reconciliation); break; }
      const account = findArtifact(store, text(values, "account", "bankAccount"));
      const rows = allocationRows(values); const ids = rows.map((row) => String(row.recordId));
      const transactions = store.artifacts.filter((item) => item.collection === "bankTransactions" && ids.includes(item.id));
      if (!transactions.length || transactions.some((item) => !["Matched", "Categorized"].includes(item.status) || item.values.reconciled)) throw new Error("Choose matched or categorized transactions that are not already reconciled.");
      if (transactions.some((item) => item.values.account && item.values.account !== account.id)) throw new Error("Only transactions for the selected bank account can be reconciled.");
      const closing = round(Number(account.values.openingBalance || 0) + transactions.reduce((sum, item) => sum + Number(item.values.amount || 0), 0));
      const statement = number(values, "statementBalance", "closingBalance"); if (Math.abs(closing - statement) > 0.005) throw new Error(`Reconciliation difference is ${(statement - closing).toFixed(2)}. Statement balance must equal ${closing.toFixed(2)}.`);
      artifact("reconciliations", { ...values, name: `Reconciliation · ${account.name}`, closingBalance: closing, transactionIds: ids }, "Reconciled"); transactions.forEach((item) => { item.values.reconciled = true; item.history.push({ at, action: "Reconciled" }); }); break;
    }
    case "document-process": {
      const document = findArtifact(store, selectedId); if (document.status === "Processed") throw new Error("This document was already processed.");
      const target: DataKey = /bill/i.test(text(values, "destination")) ? "bills" : effect.target ?? "expenses";
      const merchant = text(values, "merchant", "party", "vendor");
      const matchedVendor = workspace.vendors.find((vendor) => vendor.id === merchant || vendor.name.toLowerCase() === merchant.toLowerCase());
      if (target === "bills" && !matchedVendor) throw new Error("Choose a vendor already in the local workspace before creating a bill.");
      const created = create(target, { ...document.values, ...values, name: text(values, "name") || document.name, party: target === "bills" ? matchedVendor!.id : merchant || text(values, "category") }, target === "bills" ? "Open" : "Recorded", document.id);
      document.status = "Processed"; document.values.linkedRecord = created.id; document.history.push({ at, action: `Processed as ${created.code}` }); resourceLink(document); break;
    }
    case "automation-test": {
      const rule = findArtifact(store, selectedId);
      const evaluation = evaluateCriteria(String(rule.values.criteria ?? rule.values.conditionValue ?? ""), { name: text(values, "sampleName", "sample"), status: text(values, "sampleStatus"), amount: number(values, "sampleAmount") });
      const { matched } = evaluation; const actions = store.artifacts.filter((item) => item.collection === "automationActions" && item.values.rule === rule.id);
      artifact("automationLogs", { ...values, name: `Test · ${rule.name}`, matched, rule: rule.id, checks: evaluation.checks, actions: actions.map((item) => `${item.name}: ${matched ? "simulated" : "skipped"}`), outcome: matched ? "Matched — actions simulated locally" : "Not matched — no action", scope: "No email, webhook or custom function executed externally" }, matched ? "Passed" : "Not matched");
      rule.history.push({ at, action: `Tested · ${matched ? "matched" : "not matched"}` }); resourceLink(rule); break;
    }
    case "import": {
      const csvField = flow.steps.flatMap((step) => step.fields).find((field) => field.type === "csv");
      const csv = text(values, csvField?.id ?? "csv", "csv", "csvText", "statement", "statementCsv", "file"); const parsed = parseCsv(csv); if (parsed.length < 2) throw new Error("Provide a header and at least one CSV data row."); if (parsed.length > 501) throw new Error("Import up to 500 fictional rows per batch.");
      let mapping: Record<string, string> = {}; try { mapping = JSON.parse(text(values, `${csvField?.id ?? "csv"}Mapping`) || "{}"); } catch { throw new Error("Invalid CSV mapping."); }
      const headers = parsed[0].map((header) => mapping[header] === "__skip__" ? "" : mapping[header] || header.toLowerCase().replace(/[^a-z]/g, ""));
      for (const row of parsed.slice(1)) { const input: FlowValues = {}; for (let i = 0; i < headers.length; i++) if (headers[i]) input[headers[i]] = row[i] || ""; input.name = text(input, "name", "description", "title") || "Imported record"; input.party = text(input, "party", "customer", "vendor"); input.date = text(input, "date") || at.slice(0, 10); const amount = Number(input.amount || input.rate || values.amount || values.rate || 0); if (!Number.isFinite(amount)) throw new Error("CSV amounts must be valid numbers."); input.amount = amount;
        if (effect.entity === "items") { input.category = text(input, "category") || text(values, "category") || "Uncategorized"; input.description = text(input, "description") || text(values, "description"); input.quantity = Number(input.quantity ?? input.openingStock ?? values.quantity ?? values.openingStock ?? 0); if (!Number.isFinite(Number(input.quantity)) || Number(input.quantity) < 0) throw new Error("Imported inventory quantity must be zero or greater."); input.rate = Number(input.rate ?? values.rate ?? input.amount); }
        if (effect.entity) create(effect.entity, input); else { if (/withdraw|debit/i.test(text(input, "direction"))) input.amount = -Math.abs(amount); const account = text(values, "account", "bankAccount"); const duplicate = store.artifacts.some((item) => item.collection === "bankTransactions" && item.values.account === account && item.values.date === input.date && item.values.amount === input.amount && item.values.description === input.description); if (!duplicate || text(values, "duplicateHandling") !== "Skip exact duplicates") artifact(effect.collection ?? "bankTransactions", { ...input, account }, "Uncategorized"); } }
      break;
    }
  }
  const receipt: FlowReceipt = { id: `result-${crypto.randomUUID()}`, flowId: flow.id, title: flow.completionTitle, completedAt: at, summary: flow.completionMessage, values: structuredClone(values), links };
  store.receipts.unshift(receipt); return { workspace, store, receipt };
}
