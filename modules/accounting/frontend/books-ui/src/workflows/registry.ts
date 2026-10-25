import { flows as sales } from "./domains/sales";
import { flows as purchases } from "./domains/purchases";
import { flows as banking } from "./domains/banking";
import { flows as accountant } from "./domains/accountant";
import { flows as itemsProjects } from "./domains/items-projects";
import { flows as settingsReports } from "./domains/settings-reports";
import { moduleSpecs } from "../data";
import type { DataKey } from "../types";
import type { FlowDefinition } from "./contracts";

const imports: FlowDefinition[] = moduleSpecs.map((spec) => ({
  id: `import-${spec.key}`, domain: ["vendors", "expenses", "bills", "purchaseOrders", "vendorCredits", "paymentsMade"].includes(spec.key) ? "Purchases" : ["journals", "budgets"].includes(spec.key) ? "Accountant" : ["items", "projects", "timesheets"].includes(spec.key) ? "Items & Projects" : "Sales",
  title: `Import ${spec.title}`, description: "Map a small fictional CSV batch, review its rows, then add the records to this workspace.", module: spec.key,
  steps: [
    { title: "Source data", description: "Paste CSV with a header row. Use fictional names and amounts only.", fields: [{ id: "csv", label: "CSV data", type: "csv", required: true, defaultValue: "name,party,date,amount\nExample record,Example company,2026-10-21,120" }] },
    { title: "Field mapping", description: "Supported columns map to name, party, date, amount, category and email. Check the preview before completion.", fields: [{ id: "mappingConfirmed", label: "I have reviewed the header mapping and fictional data", type: "checkbox", required: true }] },
  ], effect: { kind: "import", entity: spec.key }, completionTitle: `${spec.title} imported`, completionMessage: "The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.", evidence: "local-concept",
}));

const domainFlows = [...sales, ...purchases, ...banking, ...accountant, ...itemsProjects, ...settingsReports];
const lifecycle: FlowDefinition[] = [
  { id: "edit-customer", domain: "Sales", title: "Edit customer", description: "Update customer details and review the changes before saving.", module: "customers", steps: [{ title: "Customer details", description: "Select the customer and update their contact information.", fields: [{ id: "record", label: "Customer", type: "record", collection: "customers", required: true }, { id: "name", label: "Display name", type: "text", required: true }, { id: "email", label: "Email", type: "email" }, { id: "phone", label: "Phone", type: "text" }] }], effect: { kind: "edit", entity: "customers" }, completionTitle: "Customer updated", completionMessage: "The contact information and activity history were updated.", evidence: "local-concept" },
  ...(["recurringInvoices", "recurringBills"] as const).map((key): FlowDefinition => ({ id: key === "recurringInvoices" ? "resume-recurring-invoice" : "resume-recurring-bill", domain: key === "recurringInvoices" ? "Sales" : "Purchases", title: "Resume " + (key === "recurringInvoices" ? "recurring invoice" : "recurring bill"), description: "Reactivate a paused profile and continue generating its documents.", module: key, steps: [{ title: "Profile", description: "Select the paused profile to resume.", fields: [{ id: "record", label: "Recurring profile", type: "record", collection: key, required: true }] }], effect: { kind: "status", entity: key, status: "Active" }, completionTitle: "Recurring profile resumed", completionMessage: "The profile is active and can generate its next document on demand.", evidence: "local-concept" })),
];
function normalizeFlow(input: FlowDefinition): FlowDefinition {
  const flow = structuredClone(input);
  const linkAliases: Record<string, string> = { "invite-customer-portal": "manage-customer-portal-message", "create-report-schedule": "schedule-report" };
  flow.nextFlowIds = flow.nextFlowIds?.map((id) => linkAliases[id] || id);
  const entityStatuses: Record<string, { entity: DataKey; status: "Declined" | "Void" | "Paused" | "Completed" | "Accepted" | "Published" }> = {
    "decline-quote": { entity: "quotes", status: "Declined" }, "void-invoice": { entity: "invoices", status: "Void" },
    "pause-recurring-invoice": { entity: "recurringInvoices", status: "Paused" }, "pause-recurring-bill": { entity: "recurringBills", status: "Paused" },
    "close-project": { entity: "projects", status: "Completed" }, "accept-quote": { entity: "quotes", status: "Accepted" }, "publish-journal": { entity: "journals", status: "Published" },
  };
  if (entityStatuses[flow.id]) flow.effect = { ...flow.effect, ...entityStatuses[flow.id], kind: "status" };
  if (flow.id === "refund-credit-note") flow.effect = { kind: "credit", entity: "creditNotes", target: "invoices", action: "refund" };
  if (flow.id === "create-batch-payment") flow.effect.target = "batchPayments";
  if (["lock-transactions", "unlock-transactions", "simulate-bank-provider-connection"].includes(flow.id)) flow.effect.kind = "artifact";
  for (const step of flow.steps) for (const field of step.fields) {
    if (field.id === "sections" && field.collection === "templates") field.collection = "templateSections";
    if (field.id === "permissions") field.collection = "permissions";
    if (field.id === "filters") field.collection = "reportFilters";
    if (field.id === "scopes") field.collection = "integrationScopes";
    if (field.id === "module" && ["lock-transactions", "unlock-transactions"].includes(flow.id)) field.options = ["Sales", "Purchases", "Banking", "Accountant"];
    if (field.id === "criteria") field.help = "Use status=Overdue, amount>=500, or name contains workshop. Combine clauses with AND; the local test evaluates only these conditions.";
  }
  if (flow.steps.some((step) => step.fields.some((field) => field.type === "lines"))) {
    for (const amount of flow.steps.flatMap((step) => step.fields).filter((field) => field.id === "amount" && field.required)) amount.required = false;
    if (flow.effect.kind === "create") {
      const last = flow.steps[flow.steps.length - 1];
      for (const field of [{ id: "discount", label: "Discount amount", type: "number" as const, min: 0, defaultValue: 0 }, { id: "taxRate", label: "Tax rate (%)", type: "number" as const, min: 0, defaultValue: 0 }, { id: "shipping", label: "Shipping / additional charge", type: "number" as const, min: 0, defaultValue: 0 }]) if (!last.fields.some((old) => old.id === field.id)) last.fields.push(field);
    }
  }
  return flow;
}
export const allFlows: FlowDefinition[] = [...domainFlows, ...lifecycle, ...imports.filter((flow) => !domainFlows.some((existing) => existing.id === flow.id))].map(normalizeFlow);
const preferredCreationFlow: Partial<Record<DataKey, string>> = { journals: "create-journal" };
export function createFlowFor(key: DataKey) {
  const preferred = preferredCreationFlow[key];
  return (preferred ? allFlows.find((flow) => flow.id === preferred) : undefined)
    ?? allFlows.find((flow) => flow.effect.entity === key && flow.id.startsWith("create-") && ["create", "journal", "recurring"].includes(flow.effect.kind))
    ?? allFlows.find((flow) => flow.effect.target === key && ["payment", "recurring", "time-invoice"].includes(flow.effect.kind));
}
export function recordFlowsFor(key: DataKey) { return allFlows.filter((flow) => flow.effect.entity === key && !["create", "import"].includes(flow.effect.kind) && !(flow.effect.kind === "journal" && !flow.effect.action?.includes("reverse"))); }
