import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { Activity, BarChart3, Bell, BookOpen, Building2, ChevronDown, CircleHelp, ClipboardList, Clock3, ContactRound, FileText, FolderKanban, Home, Landmark, Menu, Package, Plus, Search, Settings, ShoppingBag, ShoppingCart, Users, Wallet, X } from "lucide-react";
import { ErpSidebarHeader, ErpTopBar } from "@amizopower/erp-ui";
import { Button } from "./Primitives";
import { moduleSpecs } from "../data";
import { useWorkspace } from "../state/WorkspaceContext";
import { useErpAccounting } from "../platform/ErpAccountingContext";
import "../erp-theme.css";

const primaryNav = [
  { label: "Home", path: "/", icon: Home }, { label: "Workflows", path: "/workflows", icon: ClipboardList },
  { label: "Items", path: "/items", icon: Package }, { label: "Sales", path: "/invoices", icon: ShoppingCart },
  { label: "Purchases", path: "/bills", icon: ShoppingBag }, { label: "Time Tracking", path: "/projects", icon: Clock3 },
  { label: "Banking", path: "/banking", icon: Landmark }, { label: "Accountant", path: "/journals", icon: BookOpen },
  { label: "Reports", path: "/reports", icon: BarChart3 }, { label: "Documents", path: "/documents", icon: FileText },
  { label: "Settings", path: "/settings", icon: Settings },
];
const quickLinks = [
  { label: "Customers", path: "/customers", icon: Users }, { label: "Quotes", path: "/quotes", icon: FileText },
  { label: "Invoices", path: "/invoices", icon: ClipboardList }, { label: "Payments Received", path: "/payments-received", icon: Wallet },
  { label: "Vendors", path: "/vendors", icon: ContactRound }, { label: "Expenses", path: "/expenses", icon: Activity },
  { label: "Projects", path: "/projects", icon: FolderKanban }, { label: "Chart of Accounts", path: "/chart-of-accounts", icon: Building2 },
];
const createLinks = [
  { label: "Invoice", path: "/invoices/new" }, { label: "Quote", path: "/quotes/new" },
  { label: "Bill", path: "/bills/new" }, { label: "Customer", path: "/customers/new" },
  { label: "Journal", path: "/journals/new" },
];
// Navigation hints only. The destination and API own authorization.
// Match Core's existing assignedModules role labels; this only filters links.
const procurementRoles = new Set(["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"]);
const hrmRoles = new Set(["employee", "manager", "hr_ops", "payroll", "finance_approver", "hr_admin", "investigator", "timesheet_operator"]);

export function AppShell() {
  const location = useLocation();
  const navigate = useNavigate();
  const { data } = useWorkspace();
  const { branding, user, selection, entities, branches, selectEntity, selectBranch, selectAllBranches, signOut } = useErpAccounting();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => window.localStorage.getItem("erp.sidebar.collapsed.v1") === "true");
  const [moduleOpen, setModuleOpen] = useState(false);
  const [contextOpen, setContextOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [searchValue, setSearchValue] = useState("");
  const [toast, setToast] = useState("");
  const activeTitle = quickLinks.find((item) => location.pathname.startsWith(item.path.split("/").slice(0, 2).join("/")))?.label;
  const roleSet = new Set(user.roles.map((role) => role.toLowerCase()));
  const coreAdmin = roleSet.has("erp_admin") || roleSet.has("hr_admin");
  const showHrm = [...roleSet].some((role) => hrmRoles.has(role));
  const showProcurement = roleSet.has("erp_admin") || [...roleSet].some((role) => procurementRoles.has(role));
  const moduleLinks = [
    { label: "Accounting", href: "/accounting/workspace/", visible: true },
    { label: "Procurement", href: "/procurement/workspace/", visible: showProcurement },
    { label: "HRM", href: "/hrm", visible: showHrm },
    { label: "ERP Core", href: "/core", visible: coreAdmin },
  ];
  const company = entities.find((entity) => entity.id === selection.entityId);
  const selectedBranch = branches.find((branch) => branch.id === selection.branch);
  const companyName = company?.name || branding.companyName || branding.displayName || "ERP";
  const logo = branding.logoDarkDataUri || branding.logoLightDataUri;
  const closeDrawer = () => setDrawerOpen(false);
  const navClass = ({ isActive }: { isActive: boolean }) => `side-link ${isActive ? "side-link-active" : ""}`;
  const submitSearch = () => {
    const term = searchValue.trim();
    if (!term) return;
    const target = moduleSpecs.find((spec) => data[spec.key].some((record) => `${record.code} ${record.name} ${record.party} ${record.category ?? ""}`.toLowerCase().includes(term.toLowerCase())));
    if (target) navigate(`${target.path}?q=${encodeURIComponent(term)}`);
    else window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "No sample record matched that search." }));
  };
  useEffect(() => { window.localStorage.setItem("erp.sidebar.collapsed.v1", String(collapsed)); }, [collapsed]);
  useEffect(() => { setDrawerOpen(false); setCreateOpen(false); setModuleOpen(false); setContextOpen(false); }, [location.pathname]);
  useEffect(() => {
    const showToast = (event: Event) => { const message = (event as CustomEvent<string>).detail; if (!message) return; setToast(message); window.setTimeout(() => setToast(""), 3200); };
    window.addEventListener("books-demo-toast", showToast);
    return () => window.removeEventListener("books-demo-toast", showToast);
  }, []);
  useEffect(() => {
    const closeMenus = (event: KeyboardEvent) => { if (event.key === "Escape") { setModuleOpen(false); setContextOpen(false); setCreateOpen(false); setDrawerOpen(false); } };
    window.addEventListener("keydown", closeMenus);
    return () => window.removeEventListener("keydown", closeMenus);
  }, []);

  return <div className={`app-shell erp-accounting-shell ${collapsed ? "erp-sidebar-collapsed" : ""}`}>
    <ErpTopBar position="fixed" className="topbar">
      <div className="brand-area">
        <button type="button" className="mobile-menu icon-button" aria-label="Open navigation" onClick={() => setDrawerOpen(true)}><Menu size={20} /></button>
        <a href="/" className="brand-lockup" aria-label="ERP home"><span className="brand-mark">{logo ? <img src={logo} alt="" /> : <Building2 size={19} aria-hidden />}</span><span className="brand-copy"><strong>{branding.displayName || companyName}</strong><small>ACCOUNTING</small></span></a>
        <div className="erp-top-menu">
          <button type="button" className="erp-top-menu-trigger" aria-label="Switch ERP module" aria-expanded={moduleOpen} onClick={() => { setModuleOpen(!moduleOpen); setContextOpen(false); }}>Accounting <ChevronDown size={14} aria-hidden /></button>
          {moduleOpen && <nav className="erp-top-menu-panel" aria-label="ERP modules">{moduleLinks.filter((item) => item.visible).map((item) => <a key={item.href} href={item.href} aria-current={item.label === "Accounting" ? "page" : undefined} onClick={() => setModuleOpen(false)}>{item.label}</a>)}</nav>}
        </div>
      </div>
      <div className="top-search-wrap"><Search size={16} aria-hidden /><input value={searchValue} onChange={(event) => setSearchValue(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") submitSearch(); }} placeholder={`Search ${activeTitle ? `in ${activeTitle}` : "Accounting samples"}`} aria-label="Search sample Accounting records" />{searchValue && <button type="button" className="search-clear" onClick={() => setSearchValue("")} aria-label="Clear search"><X size={14} /></button>}</div>
      <div className="top-actions">
        <span className="demo-chip">Preview</span>
        <div className="erp-top-menu erp-context-menu">
          <button type="button" className="org-switcher" aria-label="Organization and branch" aria-expanded={contextOpen} onClick={() => { setContextOpen(!contextOpen); setModuleOpen(false); }}><span className="org-avatar">{companyName.slice(0, 1).toUpperCase()}</span><span className="org-name">{companyName}{selectedBranch ? ` · ${selectedBranch.name}` : ""}</span><ChevronDown size={14} aria-hidden /></button>
          {contextOpen && <div className="erp-top-menu-panel erp-context-panel" role="group" aria-label="Organization context">{entities.length ? entities.map((entity) => <div key={entity.id}>
            <button type="button" onClick={() => { selectEntity(entity.id); setContextOpen(false); }} aria-current={selection.entityId === entity.id && !selection.branch ? "true" : undefined}>{entity.name}</button>
            {selection.entityId === entity.id && <><button type="button" onClick={() => { selectAllBranches(); setContextOpen(false); }}>All permitted branches</button>{branches.filter((branch) => branch.legalEntityId === entity.id).map((branch) => <button key={branch.id} type="button" onClick={() => { selectBranch(branch.id); setContextOpen(false); }} aria-current={selection.branch === branch.id ? "true" : undefined}>{branch.name}</button>)}</>}
          </div>) : <p>No company context is available.</p>}</div>}
        </div>
        <Button className="top-create" size="sm" onClick={() => setCreateOpen((open) => !open)} aria-expanded={createOpen}><Plus size={16} /> <span>Create</span></Button>
        <button type="button" className="icon-button top-utility" aria-label="Notifications unavailable in preview" onClick={() => window.dispatchEvent(new CustomEvent("books-demo-toast", { detail: "This preview does not deliver notifications." }))}><Bell size={17} /></button>
        <button type="button" className="icon-button top-utility" aria-label="Workflow help" onClick={() => navigate("/workflows")}><CircleHelp size={17} /></button>
        <button type="button" className="erp-account-menu" onClick={() => signOut()} aria-label="Sign out">{user.displayName || "Account"} · Sign out</button>
      </div>
      {createOpen && <div className="create-menu" role="menu"><div className="menu-caption">Create a sample record</div>{createLinks.map((item) => <Link role="menuitem" key={item.path} to={item.path} onClick={() => setCreateOpen(false)}>{item.label}</Link>)}</div>}
    </ErpTopBar>
    <div className="workspace-layout"><aside className={`sidebar ${drawerOpen ? "sidebar-open" : ""}`} aria-label="Accounting navigation">
      <div className="sidebar-mobile-head"><span>Navigation</span><button type="button" className="icon-button" aria-label="Close navigation" onClick={closeDrawer}><X size={18} /></button></div>
      <ErpSidebarHeader moduleName="Accounting" collapsed={collapsed} onToggle={() => setCollapsed((value) => !value)} />
      <nav aria-label="Main navigation" className="primary-nav">{primaryNav.map(({ label, path, icon: Icon }) => <NavLink key={path} to={path} end={path === "/"} onClick={closeDrawer} className={navClass} title={label}><Icon size={18} strokeWidth={1.8} aria-hidden /><span>{label}</span></NavLink>)}</nav>
      <div className="sidebar-divider" /><div className="sidebar-section-label">Quick access</div>
      <nav aria-label="Quick access" className="quick-nav">{quickLinks.map(({ label, path, icon: Icon }) => <NavLink key={path} to={path} onClick={closeDrawer} className={navClass} title={label}><Icon size={16} strokeWidth={1.8} aria-hidden /><span>{label}</span></NavLink>)}</nav>
      {coreAdmin && <a className="erp-core-settings-link" href="/core"><Settings size={16} aria-hidden /><span>ERP Core settings</span></a>}
    </aside>
    {drawerOpen && <button type="button" className="drawer-scrim" aria-label="Close navigation" onClick={closeDrawer} />}
    <main className="main-area"><div className="demo-disclosure" role="note">Accounting preview. Sample records stay in this browser; no payment, posting, email, or external integration occurs.</div><div className="screen-content"><Outlet /></div><footer className="app-footer"><span>Accounting workspace</span></footer></main></div>
    {toast && <div className="demo-toast" role="status" aria-live="polite">{toast}</div>}
  </div>;
}
