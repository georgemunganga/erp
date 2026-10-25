import { useState } from "react";
import { BellRing, ChevronRight, FileText, Plus, Settings2, Workflow } from "lucide-react";
import { Button, EmptyState, Modal, Notice, PageHeading, Panel } from "../components/Primitives";

const templateGroups = ["Quotes", "Invoices", "Credit Notes", "Purchase Orders", "Payment Receipts", "Customer Statements", "Bills", "Expenses", "Vendor Credits", "Vendor Payments", "Vendor Statements", "Journals"];
const automationGroups = ["Workflow Rules", "Email Alerts", "In-app Notifications", "Field Updates", "Webhooks", "Functions", "Workflow Logs", "Schedules"];

export function SettingsTemplates() {
  const [group, setGroup] = useState("Invoices");
  const [action, setAction] = useState("");
  const knownInvoiceGroup = group === "Invoices";
  return <div className="workspace-screen">
    <PageHeading eyebrow="Customization · Settings" title={`${group} Templates`} description="A local prototype of the observed document-template workspace." actions={<><Button variant="secondary" onClick={() => setAction("Configure Export File Name")}>Configure Export File Name</Button><Button onClick={() => setAction("New Template")}><Plus size={15} /> New</Button></>} />
    <div className="templates-layout">
      <aside className="panel template-nav"><strong>Templates</strong>{templateGroups.map((value) => <button key={value} className={group === value ? "active" : ""} onClick={() => setGroup(value)}>{value}<ChevronRight size={13} /></button>)}</aside>
      <Panel title={`${group} Templates`} className="template-panel" action={<button className="subtle-tag" onClick={() => setAction("Template status filter")}>Status: All</button>}>
        {knownInvoiceGroup ? <div className="template-grid">
          <article className="template-card">
            <div className="template-preview" aria-label="Fictional invoice template preview"><div className="preview-brand">NORTHSTAR GOODS</div><div className="preview-title">INVOICE</div><div className="preview-meta"><span>Invoice number</span><i /><span>Invoice date</span><i /><span>Due date</span></div><div className="preview-table"><b>Item description</b><b>Amount</b><span>Strategy workshop</span><span>ZMW 1,850.00</span><span>Design review</span><span>ZMW 420.00</span></div><div className="preview-total">Total <strong>ZMW 2,270.00</strong></div></div>
            <div className="template-card-bottom"><span><span className="default-star">★</span> DEFAULT</span><button onClick={() => setAction("Edit Standard Template")}>Edit</button></div>
            <strong className="template-card-name">Standard Template</strong>
          </article>
          <article className="new-template-card"><div className="new-template-art"><FileText size={25} /></div><h3>New Template</h3><p>Customize a fictional title, table headings and column positions.</p><Button onClick={() => setAction("New Template")}><Plus size={15} /> New</Button></article>
        </div> : <EmptyState icon={<FileText size={21} />} title={`${group} layout not captured`} description="This template category was visible in Books Settings, but its detail screen has not been verified. This prototype keeps the family generic." />}
      </Panel>
    </div>
    <Notice tone="amber">Template editing and PDF generation are disabled. This preview never changes an organization template or exports tenant information.</Notice>
    <Modal open={!!action} title={action || "Template action"} description="Template controls are shown for UI reference only." onClose={() => setAction("")} footer={<Button onClick={() => setAction("")}>Close</Button>}>
      <Notice tone="blue">No template, filename rule, PDF or tenant document is created or changed.</Notice>
    </Modal>
  </div>;
}

export function AutomationScreen() {
  const [group, setGroup] = useState("Workflow Rules");
  const [action, setAction] = useState("");
  const isRules = group === "Workflow Rules";
  const isAlerts = group === "Email Alerts";
  const createLabel = isRules ? "New Workflow Rule" : isAlerts ? "New Email Alert" : `New ${group.replace(/s$/, "")}`;
  return <div className="workspace-screen">
    <PageHeading eyebrow="Automation · Settings" title={group} description="An interactive list-family preview, with every automation endpoint disconnected." actions={<><Button variant="secondary" onClick={() => setAction("Configure Usage Limit Notification")}>Usage preferences</Button><Button onClick={() => setAction(createLabel)}><Plus size={15} /> {createLabel}</Button></>} />
    <div className="automation-layout">
      <aside className="panel automation-nav"><strong>Workflow Actions</strong>{automationGroups.map((value) => <button key={value} className={group === value ? "active" : ""} onClick={() => setGroup(value)}>{value}<ChevronRight size={13} /></button>)}</aside>
      <div className="automation-content">
        {isRules && <div className="automation-stats">{[["Functions", "12 / 1,000"], ["Webhooks", "3 / 1,000"], ["Email Alerts", "4 / 500"]].map(([label, value]) => <div className="automation-stat panel" key={label}><span>{label}</span><strong>{value}</strong><small>Fictional daily usage</small></div>)}</div>}
        <Panel title={group} action={<button className="text-action" onClick={() => setAction(isRules ? "View Logs" : isAlerts ? "Configure Failure Preferences" : "Usage Details")}>{isRules ? "View Logs" : isAlerts ? "Configure Failure Preferences" : "More details"}</button>}>
          <div className="automation-filters"><label>Module<select defaultValue="All"><option>All</option><option>Invoices</option><option>Bills</option><option>Contacts</option></select></label>{isRules && <label>Status<select defaultValue="All"><option>All</option><option>Active</option><option>Inactive</option></select></label>}{isRules && <Button variant="secondary" size="sm" onClick={() => setAction("Change Execution Order")}><Settings2 size={14} /> Change Execution Order</Button>}</div>
          <EmptyState icon={isAlerts ? <BellRing size={21} /> : <Workflow size={21} />} title={isRules ? "No workflow rules" : isAlerts ? "No email alerts" : `No ${group.toLowerCase()} in this demo`} description={isRules ? "Workflow rules are not configured in this fictional workspace." : isAlerts ? "Email alerts cannot be created or sent from this prototype." : "This automation category is shown as a local interface concept only."} action={<Button variant="secondary" onClick={() => setAction(createLabel)}><Plus size={14} /> {createLabel}</Button>} />
        </Panel>
      </div>
    </div>
    <Notice tone="amber">No email, webhook, function, schedule, trigger or workflow execution is connected. Usage counters are fictional display values.</Notice>
    <Modal open={!!action} title={action || "Automation action"} description="Automation controls are present for screen-family reference only." onClose={() => setAction("")} footer={<Button onClick={() => setAction("")}>Close</Button>}>
      <Notice tone="amber">Creating, editing, enabling or running automations is disabled. No external communication is sent.</Notice>
    </Modal>
  </div>;
}
