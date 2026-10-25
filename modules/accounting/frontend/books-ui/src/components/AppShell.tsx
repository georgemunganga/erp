import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  Activity, BarChart3, Bell, BookOpen, Building2, ChevronDown, CircleHelp, ClipboardList,
  Clock3, ContactRound, FileText, FolderKanban, Home, Landmark, Menu, Package, Plus,
  Search, Settings, ShoppingBag, ShoppingCart, Users, Wallet, X,
} from "lucide-react";
import { Button, Notice } from "./Primitives";
import { moduleSpecs } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";

const primaryNav = [
  { label: "Home", path: "/", icon: Home },
  { label: "Workflows", path: "/workflows", icon: ClipboardList },
  { label: "Items", path: "/items", icon: Package },
  { label: "Sales", path: "/invoices", icon: ShoppingCart },
  { label: "Purchases", path: "/bills", icon: ShoppingBag },
  { label: "Time Tracking", path: "/projects", icon: Clock3 },
  { label: "Banking", path: "/banking", icon: Landmark },
  { label: "Accountant", path: "/journals", icon: BookOpen },
  { label: "Reports", path: "/reports", icon: BarChart3 },
  { label: "Documents", path: "/documents", icon: FileText },
  { label: "Settings", path: "/settings", icon: Settings },
];

const quickLinks = [
  { label: "Customers", path: "/customers", icon: Users },
  { label: "Quotes", path: "/quotes", icon: FileText },
  { label: "Invoices", path: "/invoices", icon: ClipboardList },
  { label: "Payments Received", path: "/payments-received", icon: Wallet },
  { label: "Vendors", path: "/vendors", icon: ContactRound },
  { label: "Expenses", path: "/expenses", icon: Activity },
  { label: "Projects", path: "/projects", icon: FolderKanban },
  { label: "Chart of Accounts", path: "/chart-of-accounts", icon: Building2 },
];

const createLinks = [
  { label: "Invoice", path: "/invoices/new" },
  { label: "Quote", path: "/quotes/new" },
  { label: "Bill", path: "/bills/new" },
  { label: "Customer", path: "/customers/new" },
  { label: "Journal", path: "/journals/new" },
];

export function AppShell() {
  const location = useLocation();
  const navigate = useNavigate();
  const { data } = useWorkspace();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [searchValue, setSearchValue] = useState("");
  const [toast, setToast] = useState("");
  const activeTitle = quickLinks.find((item) => location.pathname.startsWith(item.path.split("/").slice(0, 2).join("/")))?.label;

  const closeDrawer = () => setDrawerOpen(false);
  const navClass = ({ isActive }: { isActive: boolean }) => `side-link ${isActive ? "side-link-active" : ""}`;
  const submitSearch = () => {
    const term = searchValue.trim();
    if (!term) return;
    const target = moduleSpecs.find((spec) => data[spec.key].some((record) => `${record.code} ${record.name} ${record.party} ${record.category ?? ""}`.toLowerCase().includes(term.toLowerCase())));
    if (target) navigate(`${target.path}?q=${encodeURIComponent(term)}`);
    else window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "No fictional demo record matched that search." }));
  };
  useEffect(() => {
    const showToast = (event: Event) => {
      const message = (event as CustomEvent<string>).detail;
      if (!message) return;
      setToast(message);
      window.setTimeout(() => setToast(""), 3200);
    };
    window.addEventListener("books-demo-toast", showToast);
    return () => window.removeEventListener("books-demo-toast", showToast);
  }, []);

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand-area">
          <button className="mobile-menu icon-button" aria-label="Open navigation" onClick={() => setDrawerOpen(true)}>
            <Menu size={20} />
          </button>
          <Link to="/" className="brand-lockup" aria-label="Books Demo home">
            <span className="brand-mark"><img src="/books-demo-mark.svg" alt="" /></span>
            <span className="brand-copy"><strong>Books</strong><small>LOCAL WORKSPACE</small></span>
          </Link>
        </div>
        <div className="top-search-wrap">
          <Search size={16} />
          <input
            value={searchValue}
            onChange={(event) => setSearchValue(event.target.value)}
            onKeyDown={(event) => { if (event.key === "Enter") submitSearch(); }}
            placeholder={`Search ${activeTitle ? `in ${activeTitle}` : "in Books"} ( / )`}
            aria-label="Search the demo workspace"
          />
          {searchValue && <button className="search-clear" onClick={() => setSearchValue("")} aria-label="Clear search"><X size={14} /></button>}
        </div>
        <div className="top-actions">
          <span className="demo-chip"><span /> Browser-local</span>
          <button className="org-switcher" aria-label="Demo organization selector" onClick={() => window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "This prototype contains one fictional organization; switching is not connected." }))}>
            <span className="org-avatar">N</span><span className="org-name">Northstar Goods</span><ChevronDown size={14} />
          </button>
          <Button className="top-create" size="sm" onClick={() => setCreateOpen((open) => !open)} aria-expanded={createOpen}>
            <Plus size={16} /> <span>Create</span>
          </Button>
          <button className="icon-button top-utility" aria-label="Notifications are demo-only" onClick={() => window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "There are no live notifications in this fictional workspace." }))}><Bell size={17} /></button>
          <button className="icon-button top-utility" aria-label="Workflow help" onClick={() => navigate("/workflows")}><CircleHelp size={17} /></button>
        </div>
        {createOpen && (
          <div className="create-menu" role="menu">
            <div className="menu-caption">Create a record</div>
            {createLinks.map((item) => <Link role="menuitem" key={item.path} to={item.path} onClick={() => setCreateOpen(false)}>{item.label}</Link>)}
          </div>
        )}
      </header>

      <div className="workspace-layout">
        <aside className={`sidebar ${drawerOpen ? "sidebar-open" : ""}`}>
          <div className="sidebar-mobile-head">
            <span>Navigation</span>
            <button className="icon-button" aria-label="Close navigation" onClick={closeDrawer}><X size={18} /></button>
          </div>
          <nav aria-label="Main navigation" className="primary-nav">
            {primaryNav.map(({ label, path, icon: Icon }) => (
              <NavLink key={path} to={path} end={path === "/"} onClick={closeDrawer} className={navClass} title={label}>
                <Icon size={18} strokeWidth={1.8} />
                <span>{label}</span>
              </NavLink>
            ))}
          </nav>
          <div className="sidebar-divider" />
          <div className="sidebar-section-label">Quick access</div>
          <nav aria-label="Quick access" className="quick-nav">
            {quickLinks.map(({ label, path, icon: Icon }) => (
              <NavLink key={path} to={path} onClick={closeDrawer} className={navClass}>
                <Icon size={16} strokeWidth={1.8} /><span>{label}</span>
              </NavLink>
            ))}
          </nav>
          <div className="sidebar-foot"><span className="local-dot" /> Data stays in this browser</div>
        </aside>
        {drawerOpen && <button className="drawer-scrim" aria-label="Close navigation" onClick={closeDrawer} />}

        <main className="main-area">
          <div className="demo-disclosure">
            <Notice tone="amber">
              <strong>Browser-local workspace.</strong> Records are fictional. Workflows update this browser only—no live Zoho posting, payment, email or external integration.
            </Notice>
          </div>
          <div className="screen-content"><Outlet /></div>
          <footer className="app-footer">
            <span>Books demo workspace</span><span>Fictional data · local browser storage</span>
          </footer>
        </main>
      </div>
      {toast && <div className="demo-toast" role="status" aria-live="polite">{toast}</div>}
    </div>
  );
}
