import { Navigate, Route, Routes, useParams, useSearchParams } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { moduleSpecs } from "./data";
import { Dashboard } from "./screens/Dashboard";
import { ListScreen } from "./screens/ListScreen";
import { ReportsIndex, ReportDetail } from "./screens/ReportsWorkspace";
import { WorkflowHub, WorkflowWorkbench, WorkflowResult, WorkflowResourceDetail } from "./workflows/WorkflowWorkbench";
import { EntityWorkflowDetail, WorkflowDomainScreen } from "./workflows/WorkspaceViews";
import { createFlowFor } from "./workflows/registry";
import type { ModuleSpec } from "./types";

const formSpecs = [...new Map(moduleSpecs.filter((spec) => spec.createPath).map((spec) => [spec.createPath!, spec])).values()];
function CreateRoute({ spec }: { spec: ModuleSpec }) {
  const flow = createFlowFor(spec.key);
  return <Navigate to={flow ? `/workflows/${flow.id}` : "/workflows"} replace />;
}
function WorkflowEntry() {
  const { flowId } = useParams(); const [query] = useSearchParams();
  return <WorkflowWorkbench key={`${flowId}-${query.get("record") || "new"}`} />;
}
function ItemAlias() { const { itemId } = useParams(); return <Navigate to={`/records/items/${itemId}`} replace />; }

export function App() {
  return <Routes><Route element={<AppShell />}>
    <Route index element={<Dashboard />} />
    {moduleSpecs.map((spec) => <Route key={spec.path} path={spec.path} element={<ListScreen key={spec.key} spec={spec} />} />)}
    {formSpecs.map((spec) => <Route key={spec.createPath} path={spec.createPath} element={<CreateRoute spec={spec} />} />)}
    <Route path="/items/:itemId" element={<ItemAlias />} />
    <Route path="/records/:moduleKey/:recordId" element={<EntityWorkflowDetail />} />
    <Route path="/workflows" element={<WorkflowHub />} />
    <Route path="/workflows/:flowId" element={<WorkflowEntry />} />
    <Route path="/workflow-results/:receiptId" element={<WorkflowResult />} />
    <Route path="/workflow-resources/:artifactId" element={<WorkflowResourceDetail />} />
    <Route path="/banking" element={<WorkflowDomainScreen title="Banking" domain="Banking" collections={["bankAccounts", "bankTransactions", "bankRules", "reconciliations"]} />} />
    <Route path="/chart-of-accounts" element={<WorkflowDomainScreen title="Chart of Accounts" domain="Accountant" collections={["accounts", "settings"]} filter="account|balance|currency|tax" />} />
    <Route path="/transaction-locking" element={<WorkflowDomainScreen title="Transaction Locking" domain="Accountant" collections={["settings"]} filter="lock" />} />
    <Route path="/reports" element={<ReportsIndex />} />
    <Route path="/reports/:reportSlug" element={<ReportDetail />} />
    <Route path="/documents" element={<WorkflowDomainScreen title="Documents" domain="Settings & Reports" collections={["documents", "folders"]} filter="document|folder|receipt" />} />
    <Route path="/settings" element={<WorkflowDomainScreen title="Settings" domain="Settings & Reports" collections={["settings", "integrations"]} filter="settings-|integration" />} />
    <Route path="/settings/general" element={<Navigate to="/workflows/settings-general-preferences" replace />} />
    <Route path="/settings/templates" element={<WorkflowDomainScreen title="Document Templates" domain="Settings & Reports" collections={["templates"]} filter="template" />} />
    <Route path="/settings/automation" element={<WorkflowDomainScreen title="Automation" domain="Settings & Reports" collections={["automationRules", "automationActions", "automationLogs"]} filter="automation" />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Route></Routes>;
}
