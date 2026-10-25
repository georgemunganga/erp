import { useEffect, useMemo, useState } from "react";
import { ArrowUpDown, FileDown, Filter, MoreHorizontal, Plus, Search, SlidersHorizontal } from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import { formatMoney } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";
import type { DemoRecord, ModuleSpec } from "../types";
import { Button, EmptyState, Modal, PageHeading, StatusPill } from "../components/Primitives";
import { allFlows, createFlowFor } from "../workflows/registry";

export function ListScreen({ spec }: { spec: ModuleSpec }) {
  const { data } = useWorkspace(); const location = useLocation(); const navigate = useNavigate();
  const records = data[spec.key];
  const [search, setSearch] = useState(() => new URLSearchParams(location.search).get("q") ?? "");
  const [status, setStatus] = useState("All"); const [descending, setDescending] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false); const [filtersOpen, setFiltersOpen] = useState(false);
  const [fromDate, setFromDate] = useState(""); const [toDate, setToDate] = useState(""); const [minimum, setMinimum] = useState("");
  useEffect(() => setSearch(new URLSearchParams(location.search).get("q") ?? ""), [location.search]);
  const createFlow = createFlowFor(spec.key) ?? allFlows.find((flow) => flow.effect.target === spec.key && flow.effect.kind === "payment");
  const importFlow = allFlows.find((flow) => flow.effect.kind === "import" && flow.effect.entity === spec.key);
  const filtered = useMemo(() => records.filter((item) => `${item.code} ${item.name} ${item.party} ${item.email ?? ""} ${item.category ?? ""}`.toLowerCase().includes(search.toLowerCase()) && (status === "All" || item.status === status) && (!fromDate || item.date >= fromDate) && (!toDate || item.date <= toDate) && (!minimum || item.amount >= Number(minimum))).sort((a, b) => {
    const nameFirst = ["items", "customers", "vendors", "projects", "budgets"].includes(spec.key);
    const one = spec.key === "timesheets" ? a.date : nameFirst ? a.name : a.code;
    const two = spec.key === "timesheets" ? b.date : nameFirst ? b.name : b.code;
    return one.localeCompare(two, undefined, { numeric: true }) * (descending ? -1 : 1);
  }), [records, search, status, fromDate, toDate, minimum, descending, spec.key]);
  const openRecord = (item: DemoRecord) => navigate(`/records/${spec.key}/${item.id}`);
  const clear = () => { setSearch(""); setStatus("All"); setFromDate(""); setToDate(""); setMinimum(""); };
  const exportCsv = () => {
    const cell = (value: string | number) => { const text = typeof value === "number" ? String(value) : /^[=+@\-\t\r]/.test(value) ? `'${value}` : value; return `"${text.replaceAll('"', '""')}"`; };
    const rows = [["Reference", "Name", "Party", "Date", "Status", "Amount"], ...filtered.map((item) => [item.code, item.name, item.party, item.date, item.status, item.amount])];
    const url = URL.createObjectURL(new Blob([rows.map((row) => row.map(cell).join(",")).join("\n")], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a"); link.href = url; link.download = `${spec.key}-browser-local.csv`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); setMoreOpen(false);
  };
  return <div className="list-screen">
    <PageHeading eyebrow="Books" title={spec.title} description={`Manage ${spec.title.toLowerCase()} and continue their linked workflows.`} actions={<>{importFlow && <Button variant="secondary" onClick={() => navigate(`/workflows/${importFlow.id}`)}><FileDown size={15} /> Import</Button>}{createFlow && <Button onClick={() => navigate(`/workflows/${createFlow.id}`)}><Plus size={16} /> {spec.createLabel ?? createFlow.title}</Button>}</>} />
    <div className="list-toolbar panel"><label className="list-search"><Search size={16} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`Search ${spec.title.toLowerCase()}...`} /></label><div className="toolbar-right"><button className="icon-button" aria-label={`Sort ${descending ? "ascending" : "descending"}`} onClick={() => setDescending((value) => !value)}><ArrowUpDown size={15} /></button><label className="filter-select"><Filter size={15} /><select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Filter by status">{["All", ...new Set(records.map((item) => item.status))].map((value) => <option key={value}>{value}</option>)}</select></label><button className="icon-button" aria-label="Additional filters" onClick={() => setFiltersOpen(true)}><SlidersHorizontal size={16} /></button><div className="more-wrap"><button className="icon-button" aria-label="More list actions" onClick={() => setMoreOpen((value) => !value)}><MoreHorizontal size={18} /></button>{moreOpen && <div className="small-menu"><button onClick={exportCsv}>Export filtered CSV</button><button onClick={() => { clear(); setMoreOpen(false); }}>Clear filters</button></div>}</div></div></div>
    <div className="list-state-line"><span>{filtered.length} {filtered.length === 1 ? "record" : "records"}</span><span>Browser-local data</span></div>
    {!filtered.length ? <div className="panel"><EmptyState title={records.length ? "No matching records" : spec.emptyTitle} description={records.length ? "Adjust the search or filters to find your records." : "Create the first record, then continue its lifecycle from the detail page."} action={createFlow && <Button onClick={() => navigate(`/workflows/${createFlow.id}`)}><Plus size={15} /> {createFlow.title}</Button>} /></div> : <div className="records-panel panel"><div className="table-scroll"><table className="records-table"><thead><tr>{spec.columns.map((column, index) => <th key={column}>{column}{index === 0 && <ArrowUpDown size={12} />}</th>)}<th aria-label="Row actions" /></tr></thead><tbody>{filtered.map((item) => <tr key={item.id}>{renderCells(item, spec, () => openRecord(item))}<td className="row-actions-cell"><button className="icon-button" aria-label={`Open ${item.code} details`} onClick={() => openRecord(item)}><MoreHorizontal size={17} /></button></td></tr>)}</tbody></table></div><div className="table-footer">Showing {filtered.length} of {records.length}</div></div>}
    {!!filtered.length && <div className="mobile-record-list">{filtered.map((item) => <button key={item.id} className="mobile-record-card" onClick={() => openRecord(item)}><span className="mobile-card-head"><strong>{item.code}</strong><StatusPill status={item.status} /></span><span className="mobile-card-title">{item.name}</span><span className="mobile-card-meta">{item.party} · {item.date}</span><span className="mobile-card-total">{spec.key === "timesheets" ? `${item.amount} hours` : formatMoney(item.amount)}</span></button>)}</div>}
    <Modal open={filtersOpen} title="Filter records" onClose={() => setFiltersOpen(false)} footer={<><Button variant="secondary" onClick={clear}>Clear all</Button><Button onClick={() => setFiltersOpen(false)}>Apply filters</Button></>}><div className="form-grid"><label className="form-field"><span>From date</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></label><label className="form-field"><span>To date</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></label><label className="form-field"><span>Minimum amount</span><input type="number" min="0" value={minimum} onChange={(event) => setMinimum(event.target.value)} /></label></div></Modal>
  </div>;
}
function renderCells(item: DemoRecord, spec: ModuleSpec, onOpen: () => void) {
  if (spec.key === "items") return <><td><button className="table-primary-link" onClick={onOpen}><strong>{item.name}</strong><small>{item.code}</small></button></td><td>{item.category || "Service"}</td><td className="amount-cell">{formatMoney(item.amount)}</td><td><StatusPill status={item.status} /></td></>;
  if (["customers", "vendors"].includes(spec.key)) return <><td><button className="table-primary-link" onClick={onOpen}><strong>{item.name}</strong><small>{item.code}</small></button></td><td>{item.party}</td><td>{item.email || "—"}</td><td className="amount-cell">{formatMoney(item.amount)}</td></>;
  if (spec.key === "timesheets") return <><td><button className="table-primary-link" onClick={onOpen}>{item.date}<small>{item.name}</small></button></td><td>Alex Morgan</td><td>{item.party}</td><td>{item.amount} hours</td><td><StatusPill status={item.status} /></td></>;
  const nameFirst = ["projects", "budgets"].includes(spec.key);
  return <><td><button className="table-primary-link" onClick={onOpen}><strong>{nameFirst ? item.name : item.code}</strong><small>{nameFirst ? item.code : item.name}</small></button></td>{spec.columns.slice(1).map((column) => <td key={column} className={/amount|budget/i.test(column) ? "amount-cell" : ""}>{renderColumn(column, item)}</td>)}</>;
}
function renderColumn(column: string, item: DemoRecord) {
  const value = column.toLowerCase();
  if (value.includes("status")) return <StatusPill status={item.status} />;
  if (/due|next date/.test(value)) return item.dueDate || "—";
  if (value.includes("date")) return item.date;
  if (/amount|budget|receivable|payable/.test(value)) return formatMoney(item.amount);
  if (/frequency|mode|category|type|period/.test(value)) return item.category || "—";
  return item.party || "—";
}
