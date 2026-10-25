import type { FlowDefinition, FlowRow, FlowValues } from "./contracts";

export function rowValues(value: unknown): FlowRow[] {
  return Array.isArray(value) ? value.filter((row): row is FlowRow => !!row && typeof row === "object" && !Array.isArray(row)) : [];
}
export function validateValues(flow: FlowDefinition, values: FlowValues, stepIndex?: number): Record<string, string> {
  const errors: Record<string, string> = {};
  const steps = stepIndex === undefined ? flow.steps : flow.steps.filter((_, index) => index === stepIndex);
  for (const step of steps) for (const field of step.fields) {
    const value = values[field.id];
    const empty = value === undefined || value === "" || (Array.isArray(value) && value.length === 0);
    if (field.required && (empty || (field.type === "checkbox" && value !== true))) errors[field.id] = `${field.label} is required.`;
    if (field.type === "number" && !empty && (!Number.isFinite(Number(value)) || (field.min !== undefined && Number(value) < field.min))) errors[field.id] = `${field.label} must be a valid number${field.min !== undefined ? ` of at least ${field.min}` : ""}.`;
    if (field.type === "email" && !empty && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value))) errors[field.id] = "Enter a valid email address.";
    if (field.type === "lines") {
      const rows = rowValues(value);
      if (field.required && (!rows.length || rows.some((row) => !String(row.description ?? "").trim() || Number(row.quantity) <= 0 || !Number.isFinite(Number(row.rate)) || Number(row.rate) < 0))) errors[field.id] = "Add an item description, positive quantity and non-negative rate for every line.";
    }
    if (field.type === "journal-lines") {
      const rows = rowValues(value);
      const debit = rows.reduce((sum, row) => sum + Number(row.debit || 0), 0);
      const credit = rows.reduce((sum, row) => sum + Number(row.credit || 0), 0);
      if (rows.length < 2 || debit <= 0 || Math.abs(debit - credit) > 0.005 || rows.some((row) => !row.account || Number(row.debit || 0) < 0 || Number(row.credit || 0) < 0 || (Number(row.debit || 0) > 0 && Number(row.credit || 0) > 0))) errors[field.id] = "Use at least two accounts; positive debits must equal credits and each row must contain only a debit or a credit.";
    }
    if (field.type === "allocations") {
      const rows = rowValues(value);
      if (field.required && (!rows.length || rows.some((row) => !row.recordId || !Number.isFinite(Number(row.amount)) || Number(row.amount) <= 0))) errors[field.id] = "Select at least one record and allocate a positive amount.";
      if (new Set(rows.map((row) => row.recordId)).size !== rows.length) errors[field.id] = "A record may only appear once in the allocation grid.";
    }
    if (field.type === "multi-record" && field.required && (!Array.isArray(value) || !value.length)) errors[field.id] = `Select at least one ${field.label.toLowerCase()}.`;
    if (field.type === "date" && !empty && !/^\d{4}-\d{2}-\d{2}$/.test(String(value))) errors[field.id] = "Enter a valid date.";
  }
  if (stepIndex === undefined && values.date && values.dueDate && String(values.dueDate) < String(values.date)) errors.dueDate = "Due date must not be before the transaction date.";
  return errors;
}
