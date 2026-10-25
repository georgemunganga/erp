import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ArrowLeft, ArrowRight, Check, CheckCircle2, ClipboardList, FileText, History, Pencil, Play, Search, Sparkles, Trash2 } from "lucide-react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, EmptyState, Notice, PageHeading, Panel } from "../components/Primitives";
import type { FlowDefinition, FlowField, FlowReceipt, FlowRow, FlowValue, FlowValues, LocalArtifact } from "./contracts";
import { allFlows } from "./registry";
import { useWorkflow } from "./store";
import { validateValues } from "./validation";
import { calculatedTotal } from "./engine";
import { WORKFLOW_DEFAULT_DATE, WorkflowField, type ResourceLookup, type WorkflowResource } from "./WorkflowFields";
import "./workflow.css";

type WizardDraft = { values: FlowValues; stepIndex: number; updatedAt: string };

const DRAFT_PREFIX = "books-workflows:draft:";
const DOMAIN_OPTIONS = ["All", "Sales", "Purchases", "Banking", "Accountant", "Items & Projects", "Settings & Reports"] as const;
type DomainFilter = (typeof DOMAIN_OPTIONS)[number];
const flowCatalog: FlowDefinition[] = allFlows as FlowDefinition[];

function safeReadDraft(flowId: string): WizardDraft | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(`${DRAFT_PREFIX}${flowId}`);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null || !("values" in parsed) || !("stepIndex" in parsed)) return null;
    const values = parsed.values;
    const stepIndex = parsed.stepIndex;
    if (typeof values !== "object" || values === null || Array.isArray(values) || typeof stepIndex !== "number") return null;
    const record = parsed as Record<string, unknown>;
    return { values: values as FlowValues, stepIndex: Math.max(0, Math.floor(stepIndex)), updatedAt: typeof record.updatedAt === "string" ? record.updatedAt : "" };
  } catch {
    return null;
  }
}

function safeWriteDraft(flowId: string, draft: WizardDraft): void {
  if (typeof window === "undefined") return;
  try { window.localStorage.setItem(`${DRAFT_PREFIX}${flowId}`, JSON.stringify(draft)); } catch { /* Storage can be unavailable in private browsing. */ }
}

function safeRemoveDraft(flowId: string): void {
  if (typeof window === "undefined") return;
  try { window.localStorage.removeItem(`${DRAFT_PREFIX}${flowId}`); } catch { /* Best effort only. */ }
}

function defaultValue(field: FlowField): FlowValue {
  if (field.defaultValue !== undefined) return field.defaultValue;
  switch (field.type) {
    case "date": return WORKFLOW_DEFAULT_DATE;
    case "checkbox": return false;
    case "lines": return [];
    case "journal-lines": return [];
    case "allocations": return [];
    case "multi-record": return [];
    default: return "";
  }
}

function fieldValues(flow: FlowDefinition): FlowValues {
  return Object.fromEntries(flow.steps.flatMap((step) => step.fields.map((field) => [field.id, defaultValue(field)])));
}

function resourceMatches(resources: WorkflowResource[], id: string): WorkflowResource | undefined {
  return resources.find((resource) => resource.id === id);
}

function prefillFromRecord(flow: FlowDefinition, values: FlowValues, recordId: string | undefined, resources: ResourceLookup): FlowValues {
  if (!recordId) return values;
  const next: FlowValues = { ...values };
  const selectedByCollection = new Map<string, WorkflowResource>();
  if (flow.effect.collection) {
    const artifact = resources(flow.effect.collection).find((item) => item.id === recordId);
    if (artifact) { selectedByCollection.set(flow.effect.collection, artifact); next.record = artifact.id; }
  }
  for (const field of flow.steps.flatMap((step) => step.fields)) {
    if (field.type !== "record" && field.type !== "multi-record") continue;
    const match = resourceMatches(resources(field.collection ?? ""), recordId);
    if (match) selectedByCollection.set(field.collection ?? "", match);
  }
  if (selectedByCollection.size === 0) {
    for (const field of flow.steps.flatMap((step) => step.fields)) {
      if (field.type !== "record" && field.type !== "multi-record") continue;
      const match = resourceMatches(resources(field.collection ?? ""), recordId);
      if (match) selectedByCollection.set(field.collection ?? "", match);
    }
  }
  const primary = [...selectedByCollection.values()][0];
  const sourceValues = primary?.values ?? {};
  for (const field of flow.steps.flatMap((step) => step.fields)) {
    if (field.id !== "record" && !["status", "convert", "payment", "credit", "recurring", "journal"].includes(flow.effect.kind) && sourceValues[field.id] !== undefined) next[field.id] = sourceValues[field.id];
    if (field.type === "record" && typeof next[field.id] === "string" && field.id !== "record") {
      const option = resources(field.collection ?? "").find((resource) => resource.id === next[field.id] || resource.name === next[field.id]);
      if (option) next[field.id] = option.id;
    }
    if (flow.effect.action === "reverse" && field.type === "journal-lines" && Array.isArray(sourceValues.lines)) next[field.id] = (sourceValues.lines as FlowRow[]).map((row) => ({ ...row, debit: row.credit || 0, credit: row.debit || 0 }));
    const selected = selectedByCollection.get(field.collection ?? flow.effect.collection ?? "");
    if (!selected) continue;
    if (field.type === "record") next[field.id] = selected.id;
    if (field.type === "multi-record") next[field.id] = [selected.id];
    const key = field.id.toLowerCase();
    const empty = next[field.id] === "" || next[field.id] === undefined;
    if (!empty) continue;
    if (key.includes("name") || key.includes("customer") || key.includes("vendor") || key.includes("party")) next[field.id] = selected.name;
    else if (key.includes("code") || key.includes("reference") || key.includes("number")) next[field.id] = selected.code ?? selected.id;
    else if (key.includes("amount") || key.includes("balance") || key.includes("total")) next[field.id] = selected.amount;
    else if (key.includes("status")) next[field.id] = selected.status;
  }
  return next;
}

function createInitialDraft(flow: FlowDefinition, recordId: string | undefined, resources: ResourceLookup): WizardDraft {
  const defaults = prefillFromRecord(flow, fieldValues(flow), recordId, resources);
  const saved = safeReadDraft(flow.id);
  const usableSaved = saved && (!recordId || !saved.values.record || saved.values.record === recordId) ? saved : null;
  const savedValues = usableSaved ? { ...defaults, ...usableSaved.values, ...(recordId ? { record: recordId } : {}) } : defaults;
  return { values: savedValues, stepIndex: Math.min(usableSaved?.stepIndex ?? 0, Math.max(0, flow.steps.length - 1)), updatedAt: usableSaved?.updatedAt ?? "" };
}

function formatAmount(amount: number): string {
  return amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function isAmountKey(key: string): boolean {
  return /(amount|total|subtotal|tax|rate|debit|credit|balance|price|quantity)/i.test(key);
}

function displayValue(value: FlowValue | undefined): string {
  if (value === undefined || value === "") return "Not provided";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "number") return formatAmount(value);
  if (Array.isArray(value)) return value.length === 0 ? "None selected" : `${value.length} selected`;
  try { const attachment = JSON.parse(value); if (attachment && typeof attachment.filename === "string") return attachment.filename; } catch { /* Regular text value. */ }
  return value;
}

function ValueSummary({ field, value, resources }: { field: FlowField; value: FlowValue | undefined; resources: ResourceLookup }) {
  if (field.type === "lines" || field.type === "journal-lines" || field.type === "allocations") {
    const rows = Array.isArray(value) ? value.filter((row): row is FlowRow => typeof row === "object" && row !== null && !Array.isArray(row)) : [];
    if (rows.length === 0) return <span className="workflow-muted">No rows</span>;
    const keys = field.type === "lines" ? ["description", "quantity", "rate", "taxRate"] : field.type === "journal-lines" ? ["account", "debit", "credit"] : ["recordId", "amount"];
    const labels: Record<string, string> = { description: "Description", quantity: "Quantity", rate: "Rate", taxRate: "Tax rate", account: "Account", debit: "Debit", credit: "Credit", recordId: "Record", amount: "Amount" };
    const options = field.type === "journal-lines" ? resources("accounts") : resources(field.collection ?? "");
    return <div className="workflow-summary-table-wrap"><table className="workflow-summary-table" aria-label={`${field.label} summary`}><thead><tr>{keys.filter((key) => rows.some((row) => row[key] !== undefined)).map((key) => <th key={key}>{labels[key]}</th>)}</tr></thead><tbody>{rows.map((row, rowIndex) => <tr key={`summary-${rowIndex}`}>{keys.filter((key) => rows.some((item) => item[key] !== undefined)).map((key) => { const raw = row[key]; const option = (key === "account" || key === "recordId") ? options.find((item) => item.id === String(raw ?? "")) : undefined; return <td key={key}>{option?.name ?? (isAmountKey(key) && typeof raw === "number" ? formatAmount(raw) : String(raw ?? "—"))}</td>; })}</tr>)}</tbody></table></div>;
  }
  if (field.type === "record" && typeof value === "string") {
    const option = resources(field.collection ?? "").find((item) => item.id === value);
    return <span>{option ? `${option.name}${option.code ? ` · ${option.code}` : ""}` : displayValue(value)}</span>;
  }
  if (field.type === "multi-record" && Array.isArray(value)) {
    const options = resources(field.collection ?? "");
    return <span>{value.map((item) => options.find((option) => option.id === item)?.name ?? item).join(", ") || "None selected"}</span>;
  }
  return <span>{displayValue(value)}</span>;
}

function ScopeMeta() {
  return <small className="workflow-scope-meta">Browser-local workflow record · stored only in this browser · no external request is made</small>;
}

function NotFound({ title, description }: { title: string; description: string }) {
  return <EmptyState icon={<FileText size={20} />} title={title} description={description} action={<Link to="/workflows"><Button variant="secondary">Back to workflows</Button></Link>} />;
}

function FlowCard({ flow }: { flow: FlowDefinition }) {
  return <article className="workflow-card"><div className="workflow-card-top"><span className="workflow-domain">{flow.domain}</span><span className="workflow-evidence">{flow.steps.length} steps</span></div><h2>{flow.title}</h2><p>{flow.description}</p><div className="workflow-card-footer"><span>{flow.effect.action ?? flow.effect.kind}</span><Link to={`/workflows/${flow.id}`}><Button size="sm"><Play size={13} /> Start workflow</Button></Link></div></article>;
}

export function WorkflowHub() {
  const { store } = useWorkflow();
  const [params] = useSearchParams();
  const requestedDomain = params.get("domain") as DomainFilter;
  const [domain, setDomain] = useState<DomainFilter>(DOMAIN_OPTIONS.includes(requestedDomain) ? requestedDomain : "All");
  const [search, setSearch] = useState("");
  const filtered = useMemo(() => flowCatalog.filter((flow) => (domain === "All" || flow.domain === domain) && `${flow.title} ${flow.description} ${flow.domain}`.toLowerCase().includes(search.toLowerCase().trim())), [domain, search]);
  const receipts = [...store.receipts].sort((left, right) => right.completedAt.localeCompare(left.completedAt)).slice(0, 5);
  return <div className="workflow-screen"><PageHeading eyebrow="Workflow library" title="Books workflows" description="Run repeatable, reviewable local flows across your Books workspace." actions={<Link to="/"><Button variant="secondary">Back to dashboard</Button></Link>} />
    <div className="workflow-catalog-toolbar"><div className="workflow-tabs" role="tablist" aria-label="Workflow domains">{DOMAIN_OPTIONS.map((option) => <button key={option} type="button" role="tab" aria-selected={domain === option} className={domain === option ? "active" : ""} onClick={() => setDomain(option)}>{option}</button>)}</div><label className="workflow-search"><Search size={15} /><span className="sr-only">Search workflows</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search workflows" /></label></div>
    <div className="workflow-catalog-layout"><section><div className="workflow-section-heading"><div><h2>{domain === "All" ? "Available workflows" : domain}</h2><p>{filtered.length} reusable flow{filtered.length === 1 ? "" : "s"} with a review step before completion.</p></div></div>{filtered.length > 0 ? <div className="workflow-card-grid">{filtered.map((flow) => <FlowCard key={flow.id} flow={flow} />)}</div> : <Panel><EmptyState icon={<Search size={20} />} title="No matching workflows" description="Try another domain or search term." /></Panel>}</section><aside className="workflow-catalog-aside"><Panel title="Recent completed runs"><div className="workflow-recent-list">{receipts.length === 0 && <p className="workflow-muted">Completed runs will appear here after a workflow is confirmed.</p>}{receipts.map((receipt) => <Link className="workflow-recent-row" key={receipt.id} to={`/workflow-results/${receipt.id}`}><CheckCircle2 size={16} /><span><strong>{receipt.title}</strong><small>{new Date(receipt.completedAt).toLocaleString()}</small></span><ArrowRight size={14} /></Link>)}</div></Panel><Panel title="Local artifacts" action={<span className="workflow-count">{store.artifacts.length}</span>}><div className="workflow-recent-list">{store.artifacts.length === 0 && <p className="workflow-muted">Created records and imported artifacts will be listed here.</p>}{store.artifacts.slice(0, 5).map((artifact) => <Link className="workflow-recent-row" key={artifact.id} to={`/workflow-resources/${artifact.id}`}><ClipboardList size={16} /><span><strong>{artifact.name}</strong><small>{artifact.collection} · {artifact.status}</small></span><ArrowRight size={14} /></Link>)}</div></Panel></aside></div>
  </div>;
}

function StepRail({ flow, stepIndex, review }: { flow: FlowDefinition; stepIndex: number; review: boolean }) {
  return <nav className="workflow-step-rail" aria-label="Workflow steps">{flow.steps.map((step, index) => { const done = review || index < stepIndex; const current = !review && index === stepIndex; return <div className={`workflow-step ${done ? "done" : ""} ${current ? "current" : ""}`} key={step.title}><span className="workflow-step-marker">{done ? <Check size={14} /> : index + 1}</span><span><strong>{step.title}</strong><small>{done ? "Complete" : current ? "In progress" : "Upcoming"}</small></span></div>; })}<div className={`workflow-step ${review ? "current" : ""}`}><span className="workflow-step-marker">{review ? <Check size={14} /> : flow.steps.length + 1}</span><span><strong>Review & complete</strong><small>{review ? "Ready to confirm" : "Final check"}</small></span></div></nav>;
}

function ReviewPanel({ flow, values, resources, errors }: { flow: FlowDefinition; values: FlowValues; resources: ResourceLookup; errors: Record<string, string> }) {
  return <div className="workflow-review"><div className="workflow-review-intro"><Sparkles size={19} /><div><h2>Review {flow.title}</h2><p>Check the values, amounts, and editable tables before completing this workflow.</p></div></div>{Object.keys(errors).length > 0 && <Notice tone="amber"><span role="alert">Resolve the highlighted fields before completing this workflow. Nothing has been changed.</span></Notice>} {flow.steps.map((step) => <section className="workflow-review-section" key={step.title}><div className="workflow-review-heading"><h3>{step.title}</h3><span>{step.description}</span></div><div className="workflow-review-grid">{step.fields.map((field) => <div className="workflow-review-value" key={field.id}><div><span>{field.label}</span>{errors[field.id] && <small className="workflow-field-error">{errors[field.id]}</small>}</div><ValueSummary field={field} value={field.id === "amount" && Array.isArray(values.lines) && values.lines.length ? calculatedTotal(values) : values[field.id]} resources={resources} /></div>)}</div></section>)}</div>;
}

export function WorkflowWorkbench() {
  const { flowId } = useParams<{ flowId: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const recordId = searchParams.get("record") ?? undefined;
  const workflow = useWorkflow();
  const resources = useCallback<ResourceLookup>((collection) => workflow.resources(collection), [workflow]);
  const flow = useMemo(() => flowCatalog.find((candidate) => candidate.id === flowId), [flowId]);
  // WorkflowEntry is keyed by flow and record; rehydrate only on that mount,
  // never on provider/resource refreshes that could discard in-progress input.
  const [initialDraft] = useState(() => flow ? createInitialDraft(flow, recordId, resources) : null);
  const [values, setValues] = useState<FlowValues>(() => initialDraft?.values ?? {});
  const [stepIndex, setStepIndex] = useState(() => initialDraft?.stepIndex ?? 0);
  const [review, setReview] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [savedAt, setSavedAt] = useState(initialDraft?.updatedAt ?? "");
  const completedRef = useRef(false);

  useEffect(() => {
    if (!flow || review) return;
    const updatedAt = new Date().toISOString();
    safeWriteDraft(flow.id, { values, stepIndex, updatedAt });
    setSavedAt(updatedAt);
  }, [flow, values, stepIndex, review]);

  if (!flow) return <NotFound title="Workflow not found" description="That workflow may have been removed or the link may be incomplete." />;
  const step = flow.steps[stepIndex];
  const validate = (index?: number): boolean => {
    const nextErrors = validateValues(flow, values, index);
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };
  const updateValue = (fieldId: string, value: FlowValue) => setValues((current) => ({ ...current, [fieldId]: value }));
  const goNext = () => {
    if (!validate(stepIndex)) return;
    if (stepIndex < flow.steps.length - 1) setStepIndex((current) => current + 1);
    else { setErrors({}); setReview(true); }
  };
  const goBack = () => {
    setErrors({});
    if (review) setReview(false);
    else if (stepIndex > 0) setStepIndex((current) => current - 1);
  };
  const complete = () => {
    if (completedRef.current || !validate()) return;
    completedRef.current = true;
    try {
      const receipt = workflow.execute(flow, values);
      safeRemoveDraft(flow.id);
      navigate(`/workflow-results/${receipt.id}`);
    } catch (error) {
      completedRef.current = false;
      setErrors({ _form: error instanceof Error ? error.message : "Review the values and try again." });
    }
  };
  const discard = () => { safeRemoveDraft(flow.id); navigate("/workflows"); };
  return <div className="workflow-screen"><div className="workflow-breadcrumb"><Link to="/workflows"><ArrowLeft size={14} /> Workflow library</Link><span>/</span><span>{flow.domain}</span></div><PageHeading eyebrow={flow.domain} title={flow.title} description={flow.description} actions={<Button variant="ghost" onClick={() => navigate("/workflows")}><XIcon /> Cancel</Button>} /><div className="workflow-workbench"><StepRail flow={flow} stepIndex={stepIndex} review={review} /><main className="workflow-main"><div className="workflow-main-header"><div><span className="workflow-progress-label">{review ? "Final review" : `Step ${stepIndex + 1} of ${flow.steps.length}`}</span><h2>{review ? "Review and complete" : step.title}</h2><p>{review ? "Confirm the complete set of values before creating this workflow." : step.description}</p></div><span className="workflow-save-status">{savedAt ? "Draft saved locally" : "New draft"}</span></div>{!review && <div className="workflow-field-groups">{step.fields.map((field) => <WorkflowField key={field.id} field={field} value={values[field.id]} onChange={(value) => updateValue(field.id, value)} resources={resources} error={errors[field.id]} idPrefix={`${flow.id}-${stepIndex}`} onAuxChange={updateValue} auxValues={values} allValues={values} />)}</div>}{review && <ReviewPanel flow={flow} values={values} resources={resources} errors={errors} />}{errors._form && <div className="workflow-form-error" role="alert">{errors._form}</div>}<footer className="workflow-footer"><div className="workflow-footer-left"><Button type="button" variant="ghost" onClick={discard}><Trash2 size={14} /> Discard draft</Button></div><div className="workflow-footer-actions"><Button type="button" variant="secondary" onClick={goBack} disabled={!review && stepIndex === 0}><ArrowLeft size={14} /> Back</Button>{review ? <Button type="button" onClick={complete} disabled={completedRef.current}>{completedRef.current ? "Completing…" : `Complete ${flow.title}`}<Check size={14} /></Button> : <Button type="button" onClick={goNext}>Next <ArrowRight size={14} /></Button>}</div></footer></main></div></div>;
}

function XIcon() { return <span aria-hidden="true">×</span>; }

function ReceiptValues({ flow, receipt, resources }: { flow: FlowDefinition | undefined; receipt: FlowReceipt; resources: ResourceLookup }) {
  if (!flow) return <dl className="workflow-receipt-values">{Object.entries(receipt.values).map(([key, value]) => <div key={key}><dt>{key}</dt><dd>{displayValue(value)}</dd></div>)}</dl>;
  return <div className="workflow-receipt-values">{flow.steps.flatMap((step) => step.fields).map((field) => <div key={field.id}><dt>{field.label}</dt><dd><ValueSummary field={field} value={receipt.values[field.id]} resources={resources} /></dd></div>)}</div>;
}

export function WorkflowResult() {
  const { receiptId } = useParams<{ receiptId: string }>();
  const navigate = useNavigate();
  const workflow = useWorkflow();
  const resources = useCallback<ResourceLookup>((collection) => workflow.resources(collection), [workflow]);
  const receipt = workflow.store.receipts.find((candidate) => candidate.id === receiptId);
  if (!receipt) return <NotFound title="Receipt not found" description="This workflow receipt is not available in this browser." />;
  const flow = flowCatalog.find((candidate) => candidate.id === receipt.flowId);
  return <div className="workflow-screen"><PageHeading eyebrow="Workflow result" title={receipt.title} description={receipt.summary} actions={<Button variant="secondary" onClick={() => navigate("/workflows")}><ClipboardList size={14} /> Workflow library</Button>} /><div className="workflow-result-layout"><main><Panel><div className="workflow-result-hero"><CheckCircle2 size={30} /><div><h2>{flow?.completionTitle ?? "Workflow completed"}</h2><p>{flow?.completionMessage ?? receipt.summary}</p></div></div><div className="workflow-result-meta"><span>Completed {new Date(receipt.completedAt).toLocaleString()}</span><span>Receipt {receipt.id}</span></div></Panel><Panel title="Completed values"><ReceiptValues flow={flow} receipt={receipt} resources={resources} /></Panel></main><aside className="workflow-result-aside"><Panel title="Next steps"><div className="workflow-link-list">{receipt.links.map((link) => <Link key={link.path} to={link.path}><ArrowRight size={14} /> {link.label}</Link>)}{flow?.nextFlowIds?.map((nextId) => { const nextFlow = flowCatalog.find((candidate) => candidate.id === nextId); return nextFlow ? <Link key={nextFlow.id} to={`/workflows/${nextFlow.id}`}><Play size={14} /> Next: {nextFlow.title}</Link> : null; })}</div></Panel></aside></div></div>;
}

function artifactValueRows(artifact: LocalArtifact): Array<[string, FlowValue]> {
  return Object.entries(artifact.values);
}

export function WorkflowResourceDetail() {
  const { artifactId } = useParams<{ artifactId: string }>();
  const workflow = useWorkflow();
  const artifact = workflow.store.artifacts.find((candidate) => candidate.id === artifactId);
  if (!artifact) return <NotFound title="Artifact not found" description="This local workflow artifact is not available in this browser." />;
  const matchingFlows = flowCatalog.filter((flow) => flow.effect.collection === artifact.collection || flow.effect.entity === artifact.collection || flow.effect.target === artifact.collection);
  const actionable = matchingFlows.filter((flow) => flow.effect.kind === "edit" || flow.effect.kind === "status" || flow.effect.kind === "artifact" || flow.effect.kind === "artifact-status" || flow.effect.kind === "create");
  return <div className="workflow-screen"><div className="workflow-breadcrumb"><Link to="/workflows"><ArrowLeft size={14} /> Workflow library</Link><span>/</span><span>Artifact</span></div><PageHeading eyebrow={artifact.collection} title={artifact.name} description="Inspect the browser-local artifact values and the history created by completed workflows." actions={<span className="workflow-artifact-status">{artifact.status}</span>} /><ScopeMeta /><div className="workflow-resource-layout"><main><Panel title="Artifact values"><dl className="workflow-artifact-values">{artifactValueRows(artifact).map(([key, value]) => <div key={key}><dt>{key}</dt><dd>{displayValue(value)}</dd></div>)}</dl></Panel><Panel title="History"><div className="workflow-history">{artifact.history.length === 0 && <p className="workflow-muted">No history entries have been recorded.</p>}{artifact.history.map((entry, index) => <div className="workflow-history-entry" key={`${entry.at}-${index}`}><History size={15} /><span><strong>{entry.action}</strong><small>{new Date(entry.at).toLocaleString()}</small></span></div>)}</div></Panel></main><aside><Panel title="Available local actions"><p className="workflow-panel-copy">Start a matching flow with this artifact preselected. Any edits stay in the wizard until you explicitly complete it.</p><div className="workflow-link-list">{actionable.length === 0 && <span className="workflow-muted">No matching edit or status flows are registered.</span>}{actionable.map((flow) => <Link key={flow.id} to={`/workflows/${flow.id}?record=${encodeURIComponent(artifact.id)}`}><Pencil size={14} /> {flow.title}</Link>)}</div></Panel><Panel title="Related workflows"><div className="workflow-link-list">{matchingFlows.filter((flow) => !actionable.includes(flow)).map((flow) => <Link key={flow.id} to={`/workflows/${flow.id}?record=${encodeURIComponent(artifact.id)}`}><Play size={14} /> {flow.title}</Link>)}</div></Panel></aside></div></div>;
}

export const WorkflowCatalog = WorkflowHub;
export const WorkflowReceipt = WorkflowResult;
export const WorkflowArtifactDetail = WorkflowResourceDetail;
