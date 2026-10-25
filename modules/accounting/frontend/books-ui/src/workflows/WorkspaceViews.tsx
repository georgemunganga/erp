import { useState } from "react";
import { ArrowLeft, ArrowRight, FileText, Plus, Search } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { moduleByKey, formatMoney } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";
import { useWorkflow } from "./store";
import { allFlows, recordFlowsFor } from "./registry";
import { outstanding } from "./engine";
import { Button, EmptyState, PageHeading, Panel, StatusPill } from "../components/Primitives";
import type { DataKey } from "../types";
import type { FlowDomain } from "./contracts";

export function EntityWorkflowDetail() {
  const { moduleKey = "", recordId = "" } = useParams();
  const { data } = useWorkspace(); const { store } = useWorkflow();
  const [tab, setTab] = useState("Overview");
  const key = moduleKey as DataKey; const spec = moduleByKey[key];
  const record = spec ? data[key].find((item) => item.id === recordId) : undefined;
  if (!record || !spec) return <EmptyState title="Record not found" description="The selected record is no longer in this workspace." action={<Link to="/workflows">Workflow library</Link>} />;
  const flows = recordFlowsFor(key);
  const related = Object.entries(data).flatMap(([collection, items]) => items.filter((item) => item.id !== record.id && (item.sourceId === record.id || record.sourceId === item.id || JSON.stringify(item.details ?? {}).includes(record.id))).map((item) => ({ key: collection, item })));
  const linkedArtifacts = store.artifacts.filter((item) => JSON.stringify(item.values).includes(record.id));
  const lineRows = Array.isArray(record.details?.lines) ? record.details.lines as Record<string, unknown>[] : [];
  return <div className="workspace-screen">
    <Link className="back-link" to={spec.path}><ArrowLeft size={15} /> {spec.title}</Link>
    <PageHeading eyebrow={record.code} title={record.name} description={`${record.party || spec.entityLabel} · ${record.date}`} actions={<StatusPill status={record.status} />} />
    <div className="detail-tabs">{["Overview", "Transactions", "History", "Document"].map((item) => <button key={item} className={`detail-tab ${tab === item ? "active" : ""}`} onClick={() => setTab(item)}>{item}</button>)}</div>
    <div className="workflow-resource-layout"><main>
      {tab === "Overview" && <Panel title="Record details"><div className="detail-grid">{[[spec.partyLabel, record.party || "—"], ["Date", record.date], ["Due date", record.dueDate || "—"], ["Total", formatMoney(record.amount)], ["Paid / used", formatMoney(record.paidAmount ?? (record.status === "Paid" ? record.amount : 0))], ["Outstanding / unused", formatMoney(outstanding(record))], ["Category", record.category || "—"], ["Email", record.email || "—"], ["Stock", record.stock === undefined ? "—" : String(record.stock)]].map(([label, value]) => <div className="detail-field" key={label}><small>{label}</small><strong>{value}</strong></div>)}</div>{record.description && <p className="settings-help">{record.description}</p>}</Panel>}
      {tab === "Transactions" && <Panel title="Linked records"><div className="workflow-link-list">{related.map(({ key: relatedKey, item }) => <Link key={item.id} to={`/records/${relatedKey}/${item.id}`}><FileText size={15} /><span>{item.code} · {item.name}</span><strong>{formatMoney(item.amount)}</strong></Link>)}{linkedArtifacts.map((item) => <Link key={item.id} to={`/workflow-resources/${item.id}`}>{item.name}<ArrowRight size={15} /></Link>)}{!related.length && !linkedArtifacts.length && <p className="settings-help">No linked transactions yet. Use a payment, conversion or other action to continue this record.</p>}</div></Panel>}
      {tab === "History" && <Panel title="Activity history"><div className="workflow-history">{(record.history ?? [{ at: `${record.date}T09:00:00.000Z`, action: "Created" }]).map((event, index) => <div className="workflow-history-entry" key={index}><span className="history-dot" /><span><strong>{event.action}</strong><small>{new Date(event.at).toLocaleString()}</small></span></div>)}</div></Panel>}
      {tab === "Document" && <Panel title={`${spec.entityLabel} preview`}><div className="workflow-document"><h2>Northstar Goods</h2><h3>{spec.entityLabel.toUpperCase()} {record.code}</h3><p>{record.party} · {record.date}</p><table className="records-table"><thead><tr><th>Description</th><th>Quantity</th><th>Rate</th><th>Amount</th></tr></thead><tbody>{lineRows.length ? lineRows.map((row, index) => <tr key={index}><td>{String(row.description || row.account || "Entry")}</td><td>{Number(row.quantity || 1)}</td><td>{formatMoney(Number(row.rate || row.debit || 0))}</td><td>{formatMoney(Number(row.quantity || 1) * Number(row.rate || row.debit || 0))}</td></tr>) : <tr><td>{record.name}</td><td>1</td><td>{formatMoney(record.amount)}</td><td>{formatMoney(record.amount)}</td></tr>}</tbody></table><div className="form-total-row total-emphasis"><span>Total</span><strong>{formatMoney(record.amount)}</strong></div><p>{record.description}</p><small>Browser-local document preview · no external delivery</small></div></Panel>}
    </main><aside><Panel title="Continue this record"><div className="workflow-link-list">{flows.map((flow) => <Link key={flow.id} to={`/workflows/${flow.id}?record=${encodeURIComponent(record.id)}`}>{flow.title}<ArrowRight size={15} /></Link>)}{!flows.length && <Link to={`/workflows?domain=${encodeURIComponent(spec.key === "journals" ? "Accountant" : "Items & Projects")}`}>Browse related workflows<ArrowRight size={15} /></Link>}</div></Panel><Panel title="Document total"><div className="form-total-row"><span>Total</span><b>{formatMoney(record.amount)}</b></div><div className="form-total-row"><span>Outstanding</span><b>{formatMoney(outstanding(record))}</b></div></Panel></aside></div>
  </div>;
}

export function WorkflowDomainScreen({ title, domain, collections, filter = "" }: { title: string; domain: FlowDomain; collections: string[]; filter?: string }) {
  const { store } = useWorkflow(); const [search, setSearch] = useState(""); const [section, setSection] = useState(collections[0] || "All");
  const flows = allFlows.filter((flow) => flow.domain === domain && (!filter || new RegExp(filter, "i").test(flow.id + " " + flow.title + " " + flow.effect.collection)));
  const artifacts = store.artifacts.filter((item) => collections.includes(item.collection) && (section === "All" || item.collection === section) && `${item.name} ${item.status}`.toLowerCase().includes(search.toLowerCase()));
  return <div className="workspace-screen"><PageHeading eyebrow={domain} title={title} description="Create, review and complete each workflow, then inspect its saved records and activity." actions={<Link to={`/workflows?domain=${encodeURIComponent(domain)}`}><Button variant="secondary">All {domain} workflows</Button></Link>} />
    <div className="workflow-domain-actions">{flows.map((flow) => <Link className="workflow-domain-action" key={flow.id} to={`/workflows/${flow.id}`}><Plus size={15} /><span>{flow.title}</span><ArrowRight size={14} /></Link>)}</div>
    <Panel title="Workspace records" action={<span className="count-badge">{artifacts.length}</span>}><div className="detail-tabs">{[...collections, "All"].map((item) => <button key={item} className={`detail-tab ${section === item ? "active" : ""}`} onClick={() => setSection(item)}>{item.replace(/([A-Z])/g, " $1")}</button>)}</div><label className="list-search"><Search size={16} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search saved records" /></label><div className="workflow-artifact-grid">{artifacts.map((item) => <Link className="workflow-artifact-card" key={item.id} to={`/workflow-resources/${item.id}`}><span className="workflow-artifact-status">{item.status}</span><strong>{item.name}</strong><small>{item.collection} · {item.history.length} activity events</small><span>Open details <ArrowRight size={14} /></span></Link>)}</div>{!artifacts.length && <p className="settings-help">No saved records in this section. Choose a workflow above to create or configure one.</p>}</Panel>
  </div>;
}
