import { useMemo, useState } from "react";
import type { ChangeEvent, ReactNode } from "react";
import { Plus, Trash2, Upload, X } from "lucide-react";
import { Button } from "../components/Primitives";
import type { FlowField, FlowRow, FlowValue, FlowValues } from "./contracts";
import { calculatedTotal } from "./engine";

export interface WorkflowResource {
  id: string;
  name: string;
  code?: string;
  party?: string;
  amount: number;
  status: string;
  values?: FlowValues;
}

export type ResourceLookup = (collection: string) => WorkflowResource[];

export interface WorkflowFieldProps {
  field: FlowField;
  value: FlowValue | undefined;
  onChange: (value: FlowValue) => void;
  resources: ResourceLookup;
  error?: string;
  idPrefix?: string;
  onAuxChange?: (id: string, value: FlowValue) => void;
  auxValues?: FlowValues;
  allValues?: FlowValues;
}

const MAX_ATTACHMENT_BYTES = 1024 * 1024;
const DEFAULT_DATE = "2026-10-21";

interface AttachmentPayload {
  filename: string;
  base64: string;
}

function fieldId(prefix: string, id: string): string {
  return `${prefix}-${id}`.replace(/[^a-zA-Z0-9_-]/g, "-");
}

function asString(value: FlowValue | undefined): string {
  return typeof value === "string" || typeof value === "number" ? String(value) : "";
}

function asRows(value: FlowValue | undefined): FlowRow[] {
  return Array.isArray(value)
    ? value.filter((row): row is FlowRow => typeof row === "object" && row !== null && !Array.isArray(row))
    : [];
}

function asStrings(value: FlowValue | undefined): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : [];
}

function optionLabel(option: WorkflowResource): string {
  const reference = option.code ? ` · ${option.code}` : "";
  const party = option.party ? ` · ${option.party}` : "";
  const balance = option.amount !== 0 ? ` · ${option.amount.toFixed(2)}` : "";
  return `${option.name}${reference}${party}${balance}`;
}

function numericRowValue(row: FlowRow, key: string): number {
  const raw = row[key];
  const value = typeof raw === "number" ? raw : Number(raw);
  return Number.isFinite(value) ? value : 0;
}

function formatAmount(amount: number): string {
  return amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    const next = text[index + 1];
    if (character === '"' && quoted && next === '"') {
      cell += '"';
      index += 1;
    } else if (character === '"') {
      quoted = !quoted;
    } else if (character === "," && !quoted) {
      row.push(cell.trim());
      cell = "";
    } else if ((character === "\n" || character === "\r") && !quoted) {
      if (character === "\r" && next === "\n") index += 1;
      row.push(cell.trim());
      if (row.some((item) => item.length > 0)) rows.push(row);
      row = [];
      cell = "";
    } else {
      cell += character;
    }
  }
  if (cell.length > 0 || row.length > 0) {
    row.push(cell.trim());
    if (row.some((item) => item.length > 0)) rows.push(row);
  }
  return rows;
}

function ErrorText({ id, message }: { id: string; message?: string }): ReactNode {
  return message ? <span className="workflow-field-error" id={id} role="alert">{message}</span> : null;
}

function LinesField({ field, value, onChange, allValues }: { field: FlowField; value: FlowValue | undefined; onChange: (value: FlowValue) => void; allValues?: FlowValues }) {
  const rows = asRows(value);
  const hasTax = (field.options ?? []).includes("taxRate") || rows.some((row) => Object.prototype.hasOwnProperty.call(row, "taxRate"));
  const subtotal = rows.reduce((sum, row) => sum + numericRowValue(row, "quantity") * numericRowValue(row, "rate"), 0);
  const discount = Math.min(subtotal, Number(allValues?.discount || 0));
  const rowTax = hasTax ? rows.reduce((sum, row) => sum + (numericRowValue(row, "quantity") * numericRowValue(row, "rate") * numericRowValue(row, "taxRate")) / 100, 0) : 0;
  const globalTax = Math.max(0, subtotal - discount) * Math.max(0, Number(allValues?.taxRate || 0)) / 100;
  const tax = rowTax + globalTax;
  const total = calculatedTotal({ ...(allValues ?? {}), lines: rows });

  const updateRow = (index: number, key: string, next: string | number) => {
    const nextRows = rows.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: next } : row);
    onChange(nextRows);
  };
  const addRow = () => onChange([...rows, hasTax ? { description: "", quantity: 1, rate: 0, taxRate: 0 } : { description: "", quantity: 1, rate: 0 }]);
  const removeRow = (index: number) => onChange(rows.filter((_, rowIndex) => rowIndex !== index));

  return (
    <div className="workflow-grid-wrap">
      <div className="workflow-grid-scroll">
        <table className="workflow-grid" aria-label={`${field.label} editable lines`}>
          <thead><tr><th>Description</th><th>Quantity</th><th>Rate</th>{hasTax && <th>Tax rate %</th>}<th>Total</th><th><span className="sr-only">Remove line</span></th></tr></thead>
          <tbody>
            {rows.length === 0 && <tr><td colSpan={hasTax ? 6 : 5} className="workflow-grid-empty">Add a line to start building this transaction.</td></tr>}
            {rows.map((row, index) => {
              const total = numericRowValue(row, "quantity") * numericRowValue(row, "rate");
              return <tr key={`line-${index}`}>
                <td><input aria-label={`Line ${index + 1} description`} value={String(row.description ?? "")} onChange={(event) => updateRow(index, "description", event.target.value)} /></td>
                <td><input aria-label={`Line ${index + 1} quantity`} type="number" min="0" step="1" value={String(row.quantity ?? "")} onChange={(event) => updateRow(index, "quantity", event.target.value)} /></td>
                <td><input aria-label={`Line ${index + 1} rate`} type="number" min="0" step="0.01" value={String(row.rate ?? "")} onChange={(event) => updateRow(index, "rate", event.target.value)} /></td>
                {hasTax && <td><input aria-label={`Line ${index + 1} tax rate`} type="number" min="0" step="0.01" value={String(row.taxRate ?? "")} onChange={(event) => updateRow(index, "taxRate", event.target.value)} /></td>}
                <td className="workflow-number">{formatAmount(total)}</td>
                <td><button className="workflow-icon-button" type="button" aria-label={`Remove line ${index + 1}`} onClick={() => removeRow(index)}><Trash2 size={15} /></button></td>
              </tr>;
            })}
          </tbody>
        </table>
      </div>
      <Button type="button" variant="secondary" size="sm" onClick={addRow}><Plus size={14} /> Add line</Button>
      <div className="workflow-totals" aria-label={`${field.label} totals`}>
        <span>Subtotal <strong>{formatAmount(subtotal)}</strong></span>
        {discount > 0 && <span>Discount <strong>−{formatAmount(discount)}</strong></span>}
        {(hasTax || globalTax > 0) && <span>Tax <strong>{formatAmount(tax)}</strong></span>}
        {Number(allValues?.shipping || 0) > 0 && <span>Shipping <strong>{formatAmount(Number(allValues?.shipping || 0))}</strong></span>}
        <span className="workflow-total-emphasis">Total <strong>{formatAmount(total)}</strong></span>
      </div>
    </div>
  );
}

function JournalLinesField({ field, value, onChange, resources }: { field: FlowField; value: FlowValue | undefined; onChange: (value: FlowValue) => void; resources: ResourceLookup }) {
  const rows = asRows(value);
  const accounts = resources("accounts");
  const debit = rows.reduce((sum, row) => sum + numericRowValue(row, "debit"), 0);
  const credit = rows.reduce((sum, row) => sum + numericRowValue(row, "credit"), 0);
  const updateRow = (index: number, key: string, next: string | number) => onChange(rows.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: next } : row));
  const addRow = () => onChange([...rows, { account: "", debit: 0, credit: 0 }]);
  const removeRow = (index: number) => onChange(rows.filter((_, rowIndex) => rowIndex !== index));

  return <div className="workflow-grid-wrap">
    <div className="workflow-grid-scroll">
      <table className="workflow-grid workflow-journal-grid" aria-label={`${field.label} journal grid`}>
        <thead><tr><th>Account</th><th>Debit</th><th>Credit</th><th>Difference</th><th><span className="sr-only">Remove line</span></th></tr></thead>
        <tbody>
          {rows.length === 0 && <tr><td colSpan={5} className="workflow-grid-empty">Add debit and credit lines. The review step checks that they balance.</td></tr>}
          {rows.map((row, index) => <tr key={`journal-${index}`}>
            <td><select aria-label={`Journal line ${index + 1} account`} value={String(row.account ?? "")} onChange={(event) => updateRow(index, "account", event.target.value)}><option value="">Choose account</option>{accounts.map((account) => <option key={account.id} value={account.id}>{optionLabel(account)}</option>)}</select></td>
            <td><input aria-label={`Journal line ${index + 1} debit`} type="number" min="0" step="0.01" value={String(row.debit ?? "")} onChange={(event) => updateRow(index, "debit", event.target.value)} /></td>
            <td><input aria-label={`Journal line ${index + 1} credit`} type="number" min="0" step="0.01" value={String(row.credit ?? "")} onChange={(event) => updateRow(index, "credit", event.target.value)} /></td>
            <td className="workflow-number">{formatAmount(numericRowValue(row, "debit") - numericRowValue(row, "credit"))}</td>
            <td><button className="workflow-icon-button" type="button" aria-label={`Remove journal line ${index + 1}`} onClick={() => removeRow(index)}><Trash2 size={15} /></button></td>
          </tr>)}
        </tbody>
      </table>
    </div>
    <Button type="button" variant="secondary" size="sm" onClick={addRow}><Plus size={14} /> Add journal line</Button>
    <div className="workflow-totals workflow-journal-totals" aria-label="Journal debit and credit totals"><span>Debits <strong>{formatAmount(debit)}</strong></span><span>Credits <strong>{formatAmount(credit)}</strong></span><span className={Math.abs(debit - credit) < 0.005 ? "workflow-balanced" : "workflow-unbalanced"}>{Math.abs(debit - credit) < 0.005 ? "Balanced" : `Difference ${formatAmount(Math.abs(debit - credit))}`}</span></div>
  </div>;
}

function AllocationField({ field, value, onChange, resources }: { field: FlowField; value: FlowValue | undefined; onChange: (value: FlowValue) => void; resources: ResourceLookup }) {
  const options = resources(field.collection ?? "");
  const rows = asRows(value);
  const selected = new Set(rows.map((row) => String(row.recordId ?? "")));
  const toggle = (id: string) => {
    if (selected.has(id)) onChange(rows.filter((row) => String(row.recordId ?? "") !== id));
    else onChange([...rows, { recordId: id, amount: 0 }]);
  };
  const updateAmount = (id: string, amount: string) => onChange(rows.map((row) => String(row.recordId ?? "") === id ? { ...row, amount } : row));
  const allocated = rows.reduce((sum, row) => sum + numericRowValue(row, "amount"), 0);
  return <div className="workflow-allocation">
    <div className="workflow-allocation-options" aria-label={`${field.label} records to allocate`}>
      {options.length === 0 && <p className="workflow-muted">No matching records are available in this local workspace.</p>}
      {options.map((option) => <label className="workflow-check-row" key={option.id} data-resource-id={option.id}><input type="checkbox" checked={selected.has(option.id)} onChange={() => toggle(option.id)} /> <span><strong>{option.name}</strong><small>{option.code ?? option.party ?? option.status} · Available {formatAmount(option.amount)}</small></span></label>)}
    </div>
    {rows.length > 0 && <div className="workflow-grid-scroll"><table className="workflow-grid" aria-label={`${field.label} allocation amounts`}><thead><tr><th>Record</th><th>Amount</th></tr></thead><tbody>{rows.map((row, index) => { const option = options.find((item) => item.id === String(row.recordId ?? "")); return <tr key={`allocation-${index}`}><td>{option?.name ?? String(row.recordId ?? "Unknown record")}</td><td><input aria-label={`Allocation amount for ${option?.name ?? String(row.recordId ?? "Unknown record")}`} type="number" min="0" step="0.01" value={String(row.amount ?? "")} onChange={(event) => updateAmount(String(row.recordId ?? ""), event.target.value)} /></td></tr>; })}</tbody></table></div>}
    <div className="workflow-totals"><span>Allocated <strong>{formatAmount(allocated)}</strong></span></div>
  </div>;
}

function AttachmentField({ value, onChange, fieldId }: { value: FlowValue | undefined; onChange: (value: FlowValue) => void; fieldId: string }) {
  const [message, setMessage] = useState<string>("");
  const attachment = useMemo<AttachmentPayload | null>(() => {
    if (typeof value !== "string" || !value) return null;
    try {
      const parsed: unknown = JSON.parse(value);
      if (typeof parsed === "object" && parsed !== null && "filename" in parsed && "base64" in parsed && typeof parsed.filename === "string" && typeof parsed.base64 === "string") return { filename: parsed.filename, base64: parsed.base64 };
    } catch {
      return null;
    }
    return null;
  }, [value]);
  const handleFile = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    if (file.size > MAX_ATTACHMENT_BYTES) {
      setMessage("Choose a file no larger than 1 MiB.");
      event.target.value = "";
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result !== "string") {
        setMessage("This file could not be read in the browser.");
        return;
      }
      const comma = reader.result.indexOf(",");
      const base64 = comma >= 0 ? reader.result.slice(comma + 1) : reader.result;
      onChange(JSON.stringify({ filename: file.name, base64 }));
      setMessage("");
    };
    reader.onerror = () => setMessage("This file could not be read in the browser.");
    reader.readAsDataURL(file);
  };
  return <div className="workflow-attachment"><label className="workflow-upload" htmlFor={fieldId}><Upload size={15} /><span>{attachment ? attachment.filename : "Choose a local file"}</span><small>Max 1 MiB · stays in this browser</small></label><input className="workflow-file-input" id={fieldId} type="file" onChange={handleFile} aria-label="Choose attachment file" />{attachment && <button className="workflow-remove-link" type="button" onClick={() => onChange("")}><X size={14} /> Remove file</button>}{message && <span className="workflow-field-error" role="alert">{message}</span>}</div>;
}

function CsvField({ field, value, onChange, mappingValue, onMappingChange }: { field: FlowField; value: FlowValue | undefined; onChange: (value: FlowValue) => void; mappingValue?: FlowValue; onMappingChange?: (value: string) => void }) {
  const text = asString(value);
  const mapping = useMemo<Record<string, string>>(() => { try { return typeof mappingValue === "string" ? JSON.parse(mappingValue) : {}; } catch { return {}; } }, [mappingValue]);
  const rows = useMemo(() => parseCsv(text), [text]);
  const headers = rows[0] ?? [];
  const sample = "name,party,date,amount,description\nWorkshop invoice,Aster & Oak Studio,2026-10-21,1250,Workshop services\nDesign review,Juniper Table Co.,2026-10-21,840,Design review pack";
  const setMappingValue = (header: string, target: string) => onMappingChange?.(JSON.stringify({ ...mapping, [header]: target }));
  return <div className="workflow-csv"><textarea aria-label={`${field.label} CSV text`} rows={7} value={text} placeholder={field.placeholder ?? "Paste CSV with a header row"} onChange={(event) => onChange(event.target.value)} />
    <div className="workflow-csv-actions"><Button type="button" variant="secondary" size="sm" onClick={() => onChange(sample)}>Use sample CSV</Button><span className="workflow-muted">Comma-separated values are parsed locally; formulas and code are never executed.</span></div>
    {headers.length > 0 && <><div className="workflow-csv-map"><strong>Map columns</strong>{headers.map((header) => <label key={header}><span>{header}</span><select aria-label={`Map CSV column ${header}`} value={mapping[header] ?? header.toLowerCase().replace(/[^a-z]/g, "")} onChange={(event) => setMappingValue(header, event.target.value)}><option value="__skip__">Do not import</option>{(field.options ?? ["reference", "name", "party", "date", "amount", "description", "category", "email"]).map((target) => <option key={target} value={target}>{target}</option>)}</select></label>)}</div><div className="workflow-grid-scroll"><table className="workflow-grid workflow-csv-preview" aria-label="Parsed CSV preview"><thead><tr>{headers.map((header, index) => <th key={`${header}-${index}`}>{header || `Column ${index + 1}`}</th>)}</tr></thead><tbody>{rows.slice(1, 6).map((row, rowIndex) => <tr key={`csv-row-${rowIndex}`}>{headers.map((_, columnIndex) => <td key={`csv-cell-${rowIndex}-${columnIndex}`}>{row[columnIndex] ?? ""}</td>)}</tr>)}</tbody></table></div><small className="workflow-muted">Previewing {Math.max(rows.length - 1, 0)} data row{rows.length - 1 === 1 ? "" : "s"}. Review the mapping before continuing.</small></>}
  </div>;
}

function RecordOptions({ field, value, onChange, resources, multiple }: { field: FlowField; value: FlowValue | undefined; onChange: (value: FlowValue) => void; resources: ResourceLookup; multiple: boolean }) {
  const options = resources(field.collection ?? "");
  if (multiple) {
    const selected = new Set(asStrings(value));
    return <div className="workflow-record-options" aria-label={`${field.label} options`}>{options.length === 0 && <p className="workflow-muted">No records are available in this local workspace.</p>}{options.map((option) => <label className="workflow-check-row" key={option.id} data-resource-id={option.id}><input type="checkbox" checked={selected.has(option.id)} onChange={() => { const next = new Set(selected); if (next.has(option.id)) next.delete(option.id); else next.add(option.id); onChange(Array.from(next)); }} /><span><strong>{option.name}</strong><small>{optionLabel(option)} · {option.status}</small></span></label>)}</div>;
  }
  return <select aria-label={field.label} value={asString(value)} onChange={(event) => onChange(event.target.value)}><option value="">Choose a record</option>{options.map((option) => <option key={option.id} value={option.id}>{optionLabel(option)}</option>)}</select>;
}

export function WorkflowField({ field, value, onChange, resources, error, idPrefix = "workflow", onAuxChange, auxValues, allValues }: WorkflowFieldProps) {
  const inputId = fieldId(idPrefix, field.id);
  const errorId = `${inputId}-error`;
  const describedBy = error ? errorId : undefined;
  const common = { id: inputId, "aria-label": field.label, "aria-invalid": Boolean(error), "aria-describedby": describedBy };
  let control: ReactNode;
  switch (field.type) {
    case "text": case "email": case "date":
      control = <input {...common} type={field.type} value={asString(value)} placeholder={field.placeholder} required={field.required} onChange={(event) => onChange(event.target.value)} />;
      break;
    case "number":
      control = field.id === "amount" && asRows(allValues?.lines).length > 0
        ? <div className="workflow-derived-amount" aria-label={`${field.label} calculated from line items`}>{formatAmount(calculatedTotal(allValues ?? {}))}<small>Calculated from line items</small></div>
        : <input {...common} type="number" value={asString(value)} min={field.min} placeholder={field.placeholder} required={field.required} onChange={(event) => onChange(event.target.value === "" ? "" : Number(event.target.value))} />;
      break;
    case "select":
      control = <select {...common} value={asString(value)} required={field.required} onChange={(event) => onChange(event.target.value)}><option value="">Choose an option</option>{(field.options ?? []).map((option) => <option key={option} value={option}>{option}</option>)}</select>;
      break;
    case "textarea":
      control = <textarea {...common} rows={4} value={asString(value)} placeholder={field.placeholder} required={field.required} onChange={(event) => onChange(event.target.value)} />;
      break;
    case "checkbox":
      control = <label className="workflow-checkbox"><input {...common} type="checkbox" checked={value === true} onChange={(event) => onChange(event.target.checked)} /><span>{field.label}{field.required ? " *" : ""}</span></label>;
      break;
    case "record":
      control = <RecordOptions field={field} value={value} onChange={onChange} resources={resources} multiple={false} />;
      break;
    case "multi-record":
      control = <RecordOptions field={field} value={value} onChange={onChange} resources={resources} multiple />;
      break;
    case "lines":
      control = <LinesField field={field} value={value} onChange={onChange} allValues={allValues} />;
      break;
    case "journal-lines":
      control = <JournalLinesField field={field} value={value} onChange={onChange} resources={resources} />;
      break;
    case "allocations":
      control = <AllocationField field={field} value={value} onChange={onChange} resources={resources} />;
      break;
    case "attachment":
      control = <AttachmentField value={value} onChange={onChange} fieldId={inputId} />;
      break;
    case "csv":
      control = <CsvField field={field} value={value} onChange={onChange} mappingValue={auxValues?.[`${field.id}Mapping`]} onMappingChange={(mapping) => onAuxChange?.(`${field.id}Mapping`, mapping)} />;
      break;
    default:
      control = null;
  }
  return <div className={`workflow-field workflow-field-${field.type}`} data-field-id={field.id}>
    {field.type !== "checkbox" && <label className="workflow-field-label" htmlFor={field.type === "lines" || field.type === "journal-lines" || field.type === "allocations" || field.type === "csv" ? undefined : inputId}>{field.label}{field.required && <span aria-hidden="true"> *</span>}</label>}
    {field.help && <p className="workflow-field-help">{field.help}</p>}
    {control}
    <ErrorText id={errorId} message={error} />
  </div>;
}

export const WORKFLOW_DEFAULT_DATE = DEFAULT_DATE;
