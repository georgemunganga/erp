import { ArrowLeft, Clock3, Package, ReceiptText } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { formatMoney } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";
import { EmptyState, Notice, PageHeading, Panel, StatusPill } from "../components/Primitives";
import { useState } from "react";

export function ItemDetail() {
  const { itemId = "" } = useParams();
  const { data } = useWorkspace();
  const item = data.items.find((value) => value.id === itemId);
  const [tab, setTab] = useState("Overview");
  if (!item) return <div className="detail-screen"><Link className="back-link" to="/items"><ArrowLeft size={15} /> Back to Items</Link><EmptyState title="Record not found" description="That item is not part of the fictional demo data." /></div>;

  return <div className="detail-screen">
    <Link className="back-link" to="/items"><ArrowLeft size={15} /> All Items</Link>
    <PageHeading eyebrow="Items · Demo record" title={item.name} description={`${item.code} · Fictional catalog entry`} actions={<StatusPill status={item.status} />} />
    <div className="detail-tabs detail-tabs-page">{["Overview", "Transactions", "History"].map((value) => <button key={value} className={tab === value ? "detail-tab active" : "detail-tab"} onClick={() => setTab(value)}>{value}</button>)}</div>
    {tab === "Overview" ? <div className="detail-layout">
      <Panel title="Item information" className="detail-info-panel"><div className="detail-hero-icon"><Package size={22} /></div><div className="detail-grid">
        <Detail label="Item name" value={item.name} /><Detail label="Item code" value={item.code} /><Detail label="Type" value={item.party} /><Detail label="Sales rate" value={formatMoney(item.amount)} /><Detail label="Category" value={item.category ?? "Services"} /><Detail label="Status" value={<StatusPill status={item.status} />} />
      </div></Panel>
      <Panel title="Related activity"><EmptyState icon={<ReceiptText size={20} />} title="No transactions in this demo" description="Related sales and purchase transactions would appear here." /></Panel>
    </div> : tab === "Transactions" ? <Panel title="Transactions"><EmptyState icon={<ReceiptText size={20} />} title="No transactions recorded" description="There are no sales or purchase transactions linked to this fictional item." /></Panel> : <Panel title="History"><div className="history-entry"><span className="history-dot" /><div><strong>Item created in local demo</strong><small>{item.date} · Browser-only data</small></div><Clock3 size={15} /></div></Panel>}
    <Notice tone="amber">This detail view uses fictional data. It is not connected to the Zoho Books organization.</Notice>
  </div>;
}

function Detail({ label, value }: { label: string; value: React.ReactNode }) {
  return <div className="detail-field"><small>{label}</small><strong>{value}</strong></div>;
}
