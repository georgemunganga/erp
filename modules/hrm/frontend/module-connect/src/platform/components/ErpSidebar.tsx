import { useEffect, useState } from "react";
export { ErpSidebarHeader } from "@amizopower/erp-ui";

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
