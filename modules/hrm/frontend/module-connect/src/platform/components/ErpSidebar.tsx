import { useEffect, useState } from "react";
import { PanelLeftClose, PanelLeftOpen } from "lucide-react";

export const ERP_SIDEBAR_STORAGE_KEY = "erp.sidebar.collapsed.v1";

export function useErpSidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setCollapsed(window.localStorage.getItem(ERP_SIDEBAR_STORAGE_KEY) === "true");
    setReady(true);
  }, []);

  useEffect(() => {
    if (!ready) return;
    window.localStorage.setItem(ERP_SIDEBAR_STORAGE_KEY, String(collapsed));
  }, [collapsed, ready]);

  return { collapsed, setCollapsed, toggle: () => setCollapsed((value) => !value) };
}

export function ErpSidebarHeader({ moduleName, collapsed, onToggle }: {
  moduleName: string;
  collapsed: boolean;
  onToggle: () => void;
}) {
  return (
    <div className={`flex items-center gap-2 px-3 py-4 ${collapsed ? "justify-center px-2" : "justify-between"}`}>
      {!collapsed && <div>
        <p className="px-2 text-xs font-semibold uppercase tracking-wide text-rail-muted">Module</p>
        <p className="px-2 text-sm font-semibold text-rail-foreground">{moduleName}</p>
      </div>}
      <button
        type="button"
        onClick={onToggle}
        aria-label={collapsed ? "Expand side menu" : "Collapse side menu"}
        title={collapsed ? "Expand side menu" : "Collapse side menu"}
        className="hidden size-8 items-center justify-center rounded-md text-rail-muted hover:bg-rail-active hover:text-rail-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rail-foreground lg:inline-flex"
      >
        {collapsed ? <PanelLeftOpen className="size-4" aria-hidden /> : <PanelLeftClose className="size-4" aria-hidden />}
      </button>
    </div>
  );
}
