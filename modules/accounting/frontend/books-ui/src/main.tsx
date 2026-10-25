import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./App";
import { WorkspaceProvider } from "./state/WorkspaceContext";
import { WorkflowProvider } from "./workflows/store";
import "./styles.css";
import "./erp-theme.css";
import { ErpAccountingProvider, useErpAccounting } from "./platform/ErpAccountingContext";

function PreviewWorkspace() {
  const { storageNamespace } = useErpAccounting();
  return <WorkspaceProvider key={storageNamespace}><WorkflowProvider><App /></WorkflowProvider></WorkspaceProvider>;
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter basename="/accounting/workspace">
      <ErpAccountingProvider><PreviewWorkspace /></ErpAccountingProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
