import { ArrowDownLeft, ArrowUpRight, Banknote, BriefcaseBusiness, CalendarDays, CirclePlus, FileText, MoveUpRight, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";
import { formatMoney, sumAmounts } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";
import { Button, Panel, StatusPill } from "../components/Primitives";

const months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const bars = [37, 49, 41, 62, 54, 72, 59, 68];

export function Dashboard() {
  const { data } = useWorkspace();
  const openInvoices = data.invoices.filter((item) => item.status !== "Paid" && item.status !== "Draft");
  const openBills = data.bills.filter((item) => item.status !== "Paid" && item.status !== "Draft");
  const receivables = sumAmounts(openInvoices);
  const payables = sumAmounts(openBills);

  return (
    <div className="dashboard-screen">
      <div className="dashboard-welcome">
        <div>
          <div className="eyebrow"><Sparkles size={13} /> Tuesday, 20 October 2026</div>
          <h1>Good morning, Alex</h1>
          <p>Here’s a clear view of your demo workspace.</p>
        </div>
        <div className="welcome-actions">
          <Button variant="secondary" onClick={() => window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "This calendar is a fictional preview." }))}>
            <CalendarDays size={16} /> This Month
          </Button>
          <Link className="button button-primary button-md" to="/invoices/new"><CirclePlus size={16} /> New Invoice</Link>
        </div>
      </div>

      <div className="summary-grid">
        <SummaryCard title="Total Receivables" subtitle="Unpaid invoices" amount={receivables} icon={<ArrowDownLeft size={18} />} tone="blue" href="/invoices" detail={`${openInvoices.length} open invoices`} />
        <SummaryCard title="Total Payables" subtitle="Open bills" amount={payables} icon={<ArrowUpRight size={18} />} tone="amber" href="/bills" detail={`${openBills.length} open bills`} />
        <SummaryCard title="Cash as on 20 Oct" subtitle="Demo bank balance" amount={28460} icon={<Banknote size={18} />} tone="green" href="/banking" detail="Local sample figure" />
      </div>

      <div className="dashboard-main-grid">
        <Panel title="Cash Flow" action={<button className="text-action" onClick={() => window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "Date ranges are illustrative in this prototype." }))}>This Fiscal Year <MoveUpRight size={13} /></button>} className="cash-panel">
          <div className="cash-legend"><span><i className="legend-in" /> Money in</span><span><i className="legend-out" /> Money out</span><span className="muted">ZMW</span></div>
          <div className="cash-chart" aria-label="Illustrative monthly cash flow bars">
            {months.map((month, index) => <div className="cash-month" key={month}>
              <div className="cash-bar-pair"><span className="cash-bar cash-in" style={{ height: `${bars[index]}%` }} /><span className="cash-bar cash-out" style={{ height: `${Math.max(18, bars[index] - 17)}%` }} /></div>
              <small>{month}</small>
            </div>)}
          </div>
          <div className="cash-summary-row">
            <div><small>Opening balance</small><strong>{formatMoney(22140)}</strong></div>
            <div><small>Incoming</small><strong className="positive-value">{formatMoney(12580)}</strong></div>
            <div><small>Outgoing</small><strong>{formatMoney(6260)}</strong></div>
            <div><small>Closing balance</small><strong>{formatMoney(28460)}</strong></div>
          </div>
        </Panel>
        <Panel title="Income & Expense" action={<span className="subtle-tag">Accrual</span>}>
          <div className="income-total"><span>Total income</span><strong>{formatMoney(18420)}</strong></div>
          <div className="income-progress"><span style={{ width: "73%" }} /></div>
          <div className="income-total"><span>Total expenses</span><strong>{formatMoney(7420)}</strong></div>
          <div className="income-progress expense-progress"><span style={{ width: "38%" }} /></div>
          <div className="report-hint"><FileText size={15} /> Excludes tax in this sample view.</div>
          <Link className="panel-link" to="/reports/profit-and-loss">View Profit and Loss <MoveUpRight size={14} /></Link>
        </Panel>
      </div>

      <div className="dashboard-lower-grid">
        <Panel title="Recent Invoices" action={<Link className="text-action" to="/invoices">View all <MoveUpRight size={13} /></Link>}>
          <div className="recent-list">
            {data.invoices.slice(0, 3).map((invoice) => <Link className="recent-row" key={invoice.id} to="/invoices">
              <span className="recent-icon"><FileText size={16} /></span>
              <span className="recent-description"><strong>{invoice.code}</strong><small>{invoice.party} · {invoice.date}</small></span>
              <span className="recent-amount"><strong>{formatMoney(invoice.amount)}</strong><StatusPill status={invoice.status} /></span>
            </Link>)}
          </div>
        </Panel>
        <Panel title="Projects" action={<Link className="text-action" to="/projects">View all <MoveUpRight size={13} /></Link>}>
          {data.projects.length ? data.projects.slice(0, 2).map((project) => <div className="project-row" key={project.id}>
            <span className="project-icon"><BriefcaseBusiness size={16} /></span>
            <div className="project-content"><strong>{project.name}</strong><small>{project.party}</small><div className="project-progress"><span style={{ width: "54%" }} /></div></div>
            <StatusPill status={project.status} />
          </div>) : <div className="simple-empty">No projects in this demo yet.</div>}
          <Link className="panel-link" to="/projects">Open project list <MoveUpRight size={14} /></Link>
        </Panel>
      </div>

      <Panel title="At a glance" className="quick-panel">
        <div className="quick-tiles">
          <QuickTile label="Customers" value={data.customers.length} href="/customers" />
          <QuickTile label="Items" value={data.items.length} href="/items" />
          <QuickTile label="Open bills" value={openBills.length} href="/bills" />
          <QuickTile label="Expenses this month" value={data.expenses.length} href="/expenses" />
        </div>
      </Panel>
    </div>
  );
}

function SummaryCard({ title, subtitle, amount, icon, tone, href, detail }: { title: string; subtitle: string; amount: number; icon: React.ReactNode; tone: string; href: string; detail: string }) {
  return <Link className="summary-card" to={href}>
    <div className="summary-top"><span>{title}</span><span className={`summary-icon icon-${tone}`}>{icon}</span></div>
    <small>{subtitle}</small>
    <strong className="summary-amount">{formatMoney(amount)}</strong>
    <span className="summary-detail">{detail}<MoveUpRight size={13} /></span>
  </Link>;
}

function QuickTile({ label, value, href }: { label: string; value: number; href: string }) {
  return <Link to={href} className="quick-tile"><span>{label}</span><strong>{value}</strong></Link>;
}
