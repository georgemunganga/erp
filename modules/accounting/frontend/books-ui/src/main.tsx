import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./App";
import { WorkspaceProvider } from "./state/WorkspaceContext";
import { WorkflowProvider } from "./workflows/store";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <WorkspaceProvider>
        <WorkflowProvider><App /></WorkflowProvider>
      </WorkspaceProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
