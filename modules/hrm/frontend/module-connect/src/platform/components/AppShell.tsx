import { Link, useRouter, useRouterState } from "@tanstack/react-router";
import {
  Bell,
  Building2,
  CheckSquare,
  ChevronDown,
  ChevronRight,
  CircleHelp,
  LogOut,
  Menu,
  Moon,
  Search,
  Sun,
  LayoutGrid,
  ArrowDownToLine,
  Briefcase,
  Banknote,
  CalendarDays,
  Clock3,
  Sparkles,
  ShieldCheck,
  Upload,
  UserRound,
  Users,
  WalletCards,
  Settings,
  UserCog,
  BarChart3,
  PanelLeftClose,
  PanelLeftOpen,
} from "lucide-react";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import {
  attendanceCorrections,
  employees,
  entities,
  hrCases,
  leaveRequests,
  notifications,
  workspaces,
} from "@/mock/data";
import { derivePayslips } from "@/mock/payrollrun";
import type { Role } from "@/mock/types";
import { hrmModule } from "@/modules/hrm/nav";
import { isPathEnabled, isSectionEnabled } from "@/modules/hrm/scope";
import { ComingSoon } from "./ComingSoon";
import { ScopeSwitchOverlay } from "./ScopeSwitchOverlay";
import type { ModuleDefinition, NavItem, NavSection } from "@/platform/nav";
import { useApp, useRoleGate } from "@/platform/app-context";
import { HRM_STAFF_ROLES, useAuth } from "@/platform/auth";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { SignedInBadge } from "@/platform/components/AuthGate";
import { modules } from "@/platform/modules";
import { cn } from "@/lib/utils";

function useVisibleSections(mod: ModuleDefinition, role: Role) {
  return mod.sections.filter((s) => !s.roles || s.roles.includes(role));
}

/** Out-of-scope sections stay in the rail, greyed, so the roadmap is visible. */
function SoonSection({ section, collapsed = false }: { section: NavSection; collapsed?: boolean }) {
  const Icon = section.icon;
  return (
    <div
      className={cn(
        "flex cursor-not-allowed items-center gap-2.5 rounded-md py-2 text-sm font-medium text-rail-muted/50",
        collapsed ? "justify-center px-2" : "px-2.5",
      )}
      aria-disabled="true"
      title={`${section.label} — coming soon`}
    >
      <Icon className="size-4 shrink-0" aria-hidden />
      {collapsed ? <span className="sr-only">{section.label}</span> : (
        <>
          <span className="min-w-0 flex-1 text-left">{section.label}</span>
          <span className="shrink-0 rounded-full border border-rail-active px-1.5 py-0.5 text-[10px] font-normal">
            Soon
          </span>
        </>
      )}
    </div>
  );
}

function NavLink({ item, onNavigate, collapsed = false }: { item: NavItem; onNavigate?: () => void; collapsed?: boolean }) {
  return (
    <Link
      to={item.to}
      params={item.params as never}
      onClick={onNavigate}
      activeProps={{ className: "bg-rail-active text-rail-foreground font-medium" }}
      activeOptions={{ exact: true }}
      className={cn(
        "rounded-md text-sm text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground",
        collapsed ? "mx-auto flex size-8 items-center justify-center px-0 py-0" : "block px-3 py-1.5",
      )}
      title={item.label}
    >
      {collapsed ? (
        <>
          <span className="size-1.5 rounded-full bg-current" aria-hidden />
          <span className="sr-only">{item.label}</span>
        </>
      ) : item.label}
    </Link>
  );
}

function Section({ section, onNavigate, collapsed = false }: { section: NavSection; onNavigate?: () => void; collapsed?: boolean }) {
  const Icon = section.icon;
  const { role } = useApp();
  const visible = (i: NavItem) => (!i.roles || i.roles.includes(role)) && isPathEnabled(i.to.split("/$")[0]);
  const items = section.items?.filter(visible);
  const groups = section.groups
    ?.map((g) => ({ ...g, items: g.items.filter(visible) }))
    .filter((g) => g.items.length > 0);
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const groupedItems = groups?.flatMap((g) => g.items) ?? [];
  const childActive =
    [...(items ?? []), ...groupedItems].some((i) => pathname.startsWith(i.to.split("/$")[0]));
  const [open, setOpen] = useState(childActive);
  useEffect(() => {
    if (childActive) setOpen(true);
  }, [childActive]);

  if (section.to) {
    return (
      <Link
        to={section.to}
        onClick={onNavigate}
        activeProps={{ className: "bg-rail-active text-rail-foreground" }}
        activeOptions={{ exact: section.to === "/hrm" }}
        className={cn(
          "flex items-center gap-2.5 rounded-md py-2 text-sm font-medium text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground",
          collapsed ? "justify-center px-2" : "px-2.5",
        )}
        title={section.label}
      >
        <Icon className="size-4 shrink-0" aria-hidden />
        {collapsed ? <span className="sr-only">{section.label}</span> : section.label}
      </Link>
    );
  }

  if (collapsed) {
    return (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            aria-label={section.label}
            className={cn(
              "mx-auto flex size-10 items-center justify-center rounded-md text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rail-active",
              childActive && "bg-rail-active text-rail-foreground",
            )}
            title={section.label}
          >
            <Icon className="size-4 shrink-0" aria-hidden />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          side="right"
          align="start"
          sideOffset={10}
          className="w-72 rounded-lg border bg-card p-2 text-card-foreground shadow-xl"
        >
          <DropdownMenuLabel className="flex items-center gap-2 px-2 py-2 text-sm">
            <span className="flex size-8 items-center justify-center rounded-md bg-primary/10 text-primary">
              <Icon className="size-4" aria-hidden />
            </span>
            <span>{section.label}</span>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <div className="max-h-[70vh] space-y-1 overflow-y-auto">
            {items?.map((i) => (
              <DropdownMenuItem key={i.to + i.label} asChild>
                <Link
                  to={i.to}
                  params={i.params as never}
                  onClick={onNavigate}
                  className="flex cursor-pointer items-center justify-between rounded-md px-2 py-2 text-sm"
                >
                  <span className="min-w-0 truncate">{i.label}</span>
                  {pathname.startsWith(i.to.split("/$")[0]) ? (
                    <span className="ml-2 size-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                  ) : null}
                </Link>
              </DropdownMenuItem>
            ))}
            {groups?.map((g) => (
              <div key={g.label} className="pt-1">
                <p className="px-2 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                  {g.label}
                </p>
                {g.items.map((i) => (
                  <DropdownMenuItem key={i.to + i.label} asChild>
                    <Link
                      to={i.to}
                      params={i.params as never}
                      onClick={onNavigate}
                      className="flex cursor-pointer items-center justify-between rounded-md px-2 py-2 text-sm"
                    >
                      <span className="min-w-0 truncate">{i.label}</span>
                      {pathname.startsWith(i.to.split("/$")[0]) ? (
                        <span className="ml-2 size-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                      ) : null}
                    </Link>
                  </DropdownMenuItem>
                ))}
              </div>
            ))}
          </div>
        </DropdownMenuContent>
      </DropdownMenu>
    );
  }

  return (
    <div>
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className={cn(
          "flex w-full items-center gap-2.5 rounded-md py-2 text-sm font-medium text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground",
          collapsed ? "justify-center px-2" : "px-2.5",
        )}
        title={section.label}
      >
        <Icon className="size-4 shrink-0" aria-hidden />
        {collapsed ? <span className="sr-only">{section.label}</span> : (
          <>
            <span className="min-w-0 flex-1 text-left">{section.label}</span>
            <ChevronDown className={cn("size-3.5 transition-transform", open && "rotate-180")} aria-hidden />
          </>
        )}
      </button>
      {open ? (
        <div className={cn("mt-0.5 space-y-0.5", collapsed ? "px-1" : "ml-4 border-l border-rail-active pl-3")}>
          {items?.map((i) => <NavLink key={i.to + i.label} item={i} onNavigate={onNavigate} collapsed={collapsed} />)}
          {groups?.map((g) => (
            <div key={g.label} className="pt-2">
              <p className={cn("pb-1 text-[11px] font-semibold uppercase tracking-wide text-rail-muted/80", collapsed ? "sr-only" : "px-3")}>
                {g.label}
              </p>
              {g.items.map((i) => <NavLink key={i.to + i.label} item={i} onNavigate={onNavigate} collapsed={collapsed} />)}
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function RailContent({ onNavigate, collapsed = false, onToggleCollapsed }: { onNavigate?: () => void; collapsed?: boolean; onToggleCollapsed?: () => void }) {
  const { role } = useApp();
  const sections = useVisibleSections(hrmModule, role);
  const main = sections.filter((s) => s.id !== "configuration");
  const config = sections.find((s) => s.id === "configuration");

  return (
    <div className="flex h-full flex-col">
      <div className={cn("flex items-center gap-2 px-3 py-4", collapsed ? "flex-col justify-center px-2" : "justify-between")}>
        {collapsed ? (
          <span className="flex size-9 items-center justify-center rounded-md bg-rail-active" title={hrmModule.name}>
            <Users className="size-4 text-rail-foreground" aria-hidden />
            <span className="sr-only">{hrmModule.name}</span>
          </span>
        ) : (
          <div>
            <p className="px-2 text-xs font-semibold uppercase tracking-wide text-rail-muted">Module</p>
            <p className="px-2 text-sm font-semibold text-rail-foreground">{hrmModule.name}</p>
          </div>
        )}
        {onToggleCollapsed ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="hidden size-8 text-rail-muted hover:bg-rail-active hover:text-rail-foreground lg:inline-flex"
            onClick={onToggleCollapsed}
            aria-label={collapsed ? "Expand side menu" : "Collapse side menu"}
            title={collapsed ? "Expand side menu" : "Collapse side menu"}
          >
            {collapsed ? <PanelLeftOpen className="size-4" aria-hidden /> : <PanelLeftClose className="size-4" aria-hidden />}
          </Button>
        ) : null}
      </div>
      <nav aria-label="Main" className="flex-1 space-y-1 overflow-y-auto px-3 pb-4">
        {main
          .filter((s) => isSectionEnabled(s.id))
          .map((s) => (
            <Section key={s.id} section={s} onNavigate={onNavigate} collapsed={collapsed} />
          ))}
        {main.some((s) => !isSectionEnabled(s.id)) ? (
          <div className="pt-3">
            <p className={cn("pb-1 text-[10px] font-semibold uppercase tracking-wide text-rail-muted/70", collapsed ? "sr-only" : "px-2.5")}>
              Coming soon
            </p>
            {main
              .filter((s) => !isSectionEnabled(s.id))
              .map((s) => (
                <SoonSection key={s.id} section={s} collapsed={collapsed} />
              ))}
          </div>
        ) : null}
      </nav>
      {config ? (
        <div className="border-t border-rail-active px-3 py-3">
          <Section section={config} onNavigate={onNavigate} collapsed={collapsed} />
          <p className={cn("px-2.5 pt-1 text-[11px] text-rail-muted", collapsed && "sr-only")}>
            All setup and admin lives here.
          </p>
        </div>
      ) : null}
    </div>
  );
}

function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const { role } = useApp();
  const workerState = useApi(
    () => USE_REAL ? realApi.employees({ page: 1, pageSize: 100 }) : Promise.resolve({ items: [] as unknown[], totalCount: 0 }),
    [],
  );
  const searchableEmployees = USE_REAL ? adaptWorkers(workerState.data ?? { items: [] }) : employees;
  const sections = useVisibleSections(hrmModule, role);
  const links = sections
    .filter((s) => isSectionEnabled(s.id))
    .flatMap((s) => {
      if (s.to) return [{ label: s.label, to: s.to, params: undefined }];
      const items = [
        ...(s.items ?? []),
        ...((s.groups ?? []).flatMap((g) => g.items)),
      ];
      return items.map((i) => ({ label: `${s.label}: ${i.label}`, to: i.to, params: i.params }));
    })
    .filter((l) => isPathEnabled(l.to.split("/$")[0]));

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Search screens, records and actions…" />
      <CommandList>
        <CommandEmpty>Nothing matched. Try a reference like LV-2026-0412.</CommandEmpty>
        <CommandGroup heading="Go to">
          {links.map((l) => (
            <CommandItem key={l.label} value={l.label} asChild>
              <Link to={l.to} params={l.params as never} onClick={() => onOpenChange(false)}>
                {l.label}
              </Link>
            </CommandItem>
          ))}
        </CommandGroup>
        <CommandGroup heading="People">
          {searchableEmployees.map((e) => (
            <CommandItem key={e.id} value={`${e.fullName} ${e.employeeNo} ${e.jobTitle}`} asChild>
              <Link to="/hrm/employees/$id" params={{ id: e.id }} onClick={() => onOpenChange(false)}>
                <span className="flex min-w-0 flex-col">
                  <span className="truncate">{e.fullName}</span>
                  <span className="truncate text-xs text-muted-foreground">
                    {e.employeeNo} · {e.jobTitle}
                  </span>
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup>

        {!USE_REAL ? <CommandGroup heading="Requests and cases">
          {leaveRequests.map((r) => (
            <CommandItem key={r.id} value={`${r.id} ${r.type} leave`} asChild>
              <Link to="/hrm/leave/$id" params={{ id: r.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {r.id} — {r.type} leave
                </span>
              </Link>
            </CommandItem>
          ))}
          {attendanceCorrections.map((r) => (
            <CommandItem key={r.id} value={`${r.id} attendance correction`} asChild>
              <Link to="/hrm/attendance/$id" params={{ id: r.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">{r.id} — Attendance correction</span>
              </Link>
            </CommandItem>
          ))}
          {hrCases.map((c) => (
            <CommandItem key={c.id} value={`${c.id} ${c.subject} ${c.category}`} asChild>
              <Link to="/hrm/requests/$id" params={{ id: c.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {c.id} — {c.subject}
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup> : null}

        {!USE_REAL ? <CommandGroup heading="Payslips">
          {derivePayslips().map((p) => (
            <CommandItem key={p.id} value={`${p.id} payslip ${p.period} ${p.employee}`} asChild>
              <Link to="/hrm/payslips/$id" params={{ id: p.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {p.employee} — {p.period}
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup> : null}
      </CommandList>
    </CommandDialog>
  );
}

type QuickAccessItem = { label: string; detail: string; to: string; icon: typeof Search; roles?: Role[] };
type QuickAccessGroup = { label: string; items: QuickAccessItem[] };

const QUICK_ACCESS_GROUPS: QuickAccessGroup[] = [
  {
    label: "Time & attendance",
    items: [
      { label: "Timesheets", detail: "Today, week, month and custom attendance views", to: "/hrm/time/timesheets", icon: Clock3 },
      { label: "Import attendance", detail: "Bring in clock records through the shared importer", to: "/hrm/time/attendance/import", icon: Upload, roles: ["hr_ops", "hr_admin"] },
      { label: "Overtime review", detail: "Review derived overtime before payroll", to: "/hrm/time/operations", icon: ShieldCheck, roles: ["manager", "hr_ops", "hr_admin"] },
      { label: "My leave", detail: "View balances and leave requests", to: "/hrm/leave", icon: CalendarDays },
    ],
  },
  {
    label: "People",
    items: [
      { label: "Employees", detail: "Find and manage the employee directory", to: "/hrm/employees", icon: Users },
      { label: "My profile", detail: "View your own worker record", to: "/hrm/my-profile", icon: UserRound },
      { label: "Organization chart", detail: "See reporting relationships", to: "/hrm/org-chart", icon: Users, roles: ["hr_ops", "hr_admin"] },
    ],
  },
  {
    label: "Payroll & benefits",
    items: [
      { label: "My payslips", detail: "View your payroll statements", to: "/hrm/payslips", icon: WalletCards },
      { label: "Pay runs", detail: "Open payroll processing", to: "/hrm/payroll/runs", icon: Banknote, roles: ["payroll", "hr_admin"] },
      { label: "Salary advances", detail: "Record advances and payslip deductions", to: "/hrm/payroll/salary-advances", icon: WalletCards, roles: ["hr_ops", "payroll", "hr_admin"] },
      { label: "Benefits", detail: "Manage benefits and claims", to: "/hrm/benefits", icon: WalletCards, roles: ["hr_ops", "hr_admin", "payroll"] },
    ],
  },
  {
    label: "Performance & recruitment",
    items: [
      { label: "Performance cycles", detail: "Manage reviews and cycles", to: "/hrm/performance", icon: Sparkles },
      { label: "Goals", detail: "Track employee goals", to: "/hrm/talent/goals", icon: Sparkles },
      { label: "Hiring operations", detail: "Manage recruitment work", to: "/hrm/recruitment/operations", icon: Briefcase, roles: ["hr_ops", "hr_admin", "manager"] },
    ],
  },
  {
    label: "Reports & setup",
    items: [
      { label: "Analytics", detail: "Review workforce analytics", to: "/hrm/analytics", icon: BarChart3, roles: ["hr_ops", "hr_admin"] },
      { label: "Import and export", detail: "Use shared CSV and Excel tools", to: "/hrm/data/import-export", icon: ArrowDownToLine, roles: ["manager", "hr_ops", "hr_admin", "payroll"] },
      { label: "HR setup", detail: "Configure employer and HR rules", to: "/hrm/configuration", icon: Settings, roles: ["hr_admin"] },
      { label: "User access", detail: "Manage local users and roles", to: "/hrm/configuration/users", icon: UserCog, roles: ["hr_admin"] },
    ],
  },
];

function QuickAccessDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { role } = useApp();
  const [query, setQuery] = useState("");
  useEffect(() => { if (!open) setQuery(""); }, [open]);
  const needle = query.trim().toLowerCase();
  const groups = QUICK_ACCESS_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => (!item.roles || item.roles.includes(role)) && (!needle || `${item.label} ${item.detail} ${group.label}`.toLowerCase().includes(needle))),
  })).filter((group) => group.items.length > 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Quick access</DialogTitle>
          <DialogDescription>Jump to a common HRM task. These shortcuts open the real page and respect your role.</DialogDescription>
        </DialogHeader>
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input autoFocus value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search tasks…" className="pl-9" aria-label="Search quick access tasks" />
        </div>
        <div className="grid max-h-[58vh] gap-5 overflow-y-auto pr-1 sm:grid-cols-2">
          {groups.map((group) => (
            <section key={group.label} aria-labelledby={`quick-access-${group.label}`}>
              <h3 id={`quick-access-${group.label}`} className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.label}</h3>
              <div className="space-y-2">
                {group.items.map((item) => { const Icon = item.icon; return <Link key={item.to} to={item.to} onClick={() => onOpenChange(false)} className="group flex items-start gap-3 rounded-xl border bg-card p-3 transition hover:border-primary/40 hover:bg-surface-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="size-4" aria-hidden /></span><span className="min-w-0 flex-1"><span className="block font-medium">{item.label}</span><span className="mt-0.5 block text-xs leading-5 text-muted-foreground">{item.detail}</span></span><ChevronRight className="mt-1 size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden /></Link>; })}
              </div>
            </section>
          ))}
          {groups.length === 0 ? <p className="py-10 text-center text-sm text-muted-foreground sm:col-span-2">No quick access tasks matched “{query}”.</p> : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}

const APPROVER_ROLES: Role[] = ["manager", "hr_ops", "hr_admin", "payroll"];

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/** User display line: real identity when OIDC-signed-in, otherwise the demo name. */
function RealUserLine() {
  const { user, worker, resolvingWorker } = useAuth();

  if (USE_REAL && user?.name) {
    return (
      <span className="flex min-w-0 items-center gap-2.5">
        <span className="flex size-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary/10">
          {worker?.photoUrl ? (
            <img src={worker.photoUrl} alt="" className="size-8 object-cover" />
          ) : (
            <UserRound className="size-4 text-primary" aria-hidden />
          )}
        </span>
        <span className="min-w-0">
          {/* M14 identity link: linked worker name wins; falls back to the IdP name. */}
          <span className="block truncate">
            {worker?.fullName || user.name}
            {worker ? (
              <span className="ml-1.5 rounded border px-1 text-[10px] font-normal text-muted-foreground">
                {worker.employeeNo}
              </span>
            ) : resolvingWorker ? (
              <span className="ml-1.5 text-[10px] font-normal text-muted-foreground">…</span>
            ) : null}
          </span>
          {worker?.jobTitle ? (
            <span className="block truncate text-xs font-normal text-muted-foreground">
              {worker.jobTitle}
            </span>
          ) : user.email ? (
            <span className="block truncate text-xs font-normal text-muted-foreground">{user.email}</span>
          ) : null}
        </span>
      </span>
    );
  }
  return <span>Chanda Mwansa-Chileshe</span>;
}

/** Sign-out action: real OIDC logout in hybrid mode, demo link otherwise. */
function RealSignOut() {
  const { signOut } = useAuth();
  if (USE_REAL) {
    return (
      <button type="button" className="flex w-full items-center gap-2 px-2 py-1.5 text-sm" onClick={() => signOut()}>
        <LogOut className="size-4" aria-hidden /> Sign out
      </button>
    );
  }
  return (
    <Link to="/sign-in">
      <LogOut className="size-4" aria-hidden /> Sign out
    </Link>
  );
}

// M27 P0 UX audit: same open-for-decision predicate used by the Approvals
// page — the shell badge must agree with the page it points to.
const OPEN_STATUSES = new Set(["pending", "submitted", "open", "in progress", "in-review", "in progress", "returned", "awaiting employee"]);

function countOpen(items: unknown[]): number {
  return items.filter((raw) => {
    const x = raw as Record<string, unknown>;
    const s = String(x?.status ?? "").toLowerCase();
    return OPEN_STATUSES.has(s);
  }).length;
}

export function AppShell({ children }: { children: ReactNode }) {
  const router = useRouter();
  const { role, setRole, entityId, setEntityId, branch, setBranch, theme, toggleTheme } = useApp();
  const { worker: myWorker, user } = useAuth();
  const shellState = useApi(async () => {
    if (!USE_REAL) return null;
    const [legalEntities, locations, shellScope, notificationInbox, queue, leave, corrections] = await Promise.all([
      realApi.legalEntities().catch(() => []),
      realApi.locations().catch(() => ({ items: [] as unknown[] })),
      realApi.shell().catch(() => null),
      realApi.myNotifications().catch(() => ({ items: [], unreadCount: 0 })),
      realApi.workflowQueue().catch(() => ({ items: [], totalCount: 0 })),
      realApi.leaveRequests({ page: 1, pageSize: 1 }).catch(() => ({ items: [], totalCount: 0 })),
      realApi.timeCorrections({ page: 1, pageSize: 1 }).catch(() => ({ items: [], totalCount: 0 })),
    ]);
    return {
      // The backend wraps list endpoints in a `{ items: [...] }` envelope, so
      // unwrap before mapping. An empty tree would otherwise leave the
      // switcher stuck with no rows.
      legalEntities: (Array.isArray(legalEntities)
        ? legalEntities
        : ((legalEntities as Record<string, unknown>)?.items as unknown[]) ?? []
      ).map((raw) => {
        const e = raw as Record<string, unknown>;
        return { id: String(e.id ?? ""), registeredName: String(e.registeredName ?? ""), countryCode: String(e.countryCode ?? e.country ?? "") };
      }),
      locations: (Array.isArray(locations)
        ? locations
        : ((locations as Record<string, unknown>)?.items as unknown[]) ?? []
      ).map((raw) => {
        const l = raw as Record<string, unknown>;
        return { id: String(l.id ?? ""), name: String(l.name ?? ""), legalEntityId: String(l.legalEntityId ?? ""), type: String(l.type ?? "branch") };
      }),
      // M45: confinement metadata from the resolved scope — an operator WITH
      // branch assignments can only switch inside those branches; operators
      // WITHOUT assignments (top-level HR) keep the full tree.
      assignedLocationIds: shellScope?.assignedLocationIds ?? [],
      confined: Boolean(shellScope?.confined),
      // M49: first-time setup gate — pending orgs see the welcome overlay on
      // every HRM page until the wizard finishes. Confined branch HR are
      // excluded: they can never run the wizard (the backend refuses it).
      setupState: await realApi.setupState().catch(() => null),
      notificationInbox,
      // M27 P0 UX audit: the approvals badge was a hardcoded mock "3". The
      // shell now counts everything still open for a decision, matching the
      // same is-decidable predicate the Approvals page uses.
      pendingDecisions:
        countOpen(Array.isArray(queue?.items) ? queue.items : []) +
        countOpen(Array.isArray(leave?.items) ? leave.items : []) +
        countOpen(Array.isArray(corrections?.items) ? corrections.items : []),
    };
  }, []);
  const canApprove = useRoleGate()(APPROVER_ROLES);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [quickAccessOpen, setQuickAccessOpen] = useState(false);
  const [railCollapsed, setRailCollapsed] = useState(false);
  const [railPreferenceReady, setRailPreferenceReady] = useState(false);
  const liveEntities = shellState.data?.legalEntities ?? [];
  const liveLocations = shellState.data?.locations ?? [];
  const pendingDecisions = shellState.data?.pendingDecisions ?? 0;
  const assignedIds = shellState.data?.assignedLocationIds ?? [];
  const entity = USE_REAL
    ? liveEntities.find((e) => e.id === entityId) ?? liveEntities[0]
    : entities.find((e) => e.id === entityId) ?? entities[0];
  const entityLocations: { id: string; name: string }[] = USE_REAL
    ? liveLocations.filter((location) => !entity || location.legalEntityId === entity.id)
    : (entity as { branches?: string[] })?.branches?.map((name) => ({ id: String(name), name: String(name) })) ?? [];

  /** Tree used by the organisation switcher: every legal entity with its branches nested underneath. */
  const entityTree = (USE_REAL ? liveEntities : entities).map((e) => {
    const raw = e as Record<string, unknown>;
    const branches = USE_REAL
      ? liveLocations
          .filter((l) => l.legalEntityId === String(e.id))
          // M45: a confined operator only sees their assigned branches in the switcher.
          .filter((l) => !assignedIds.length || assignedIds.includes(String(l.id)))
          .map((l) => ({ id: String(l.id), name: String(l.name ?? l.id), type: String(l.type ?? "branch") }))
      : ((raw.branches as string[] | undefined) ?? []).map((name) => ({ id: String(name), name: String(name), type: "" }));
    return {
      entityId: String(e.id),
      entityName: String(raw.registeredName ?? raw.tradingName ?? raw.name ?? e.id),
      entityCode: String(raw.code ?? ""),
      branches,
    };
  });
  const pathname = useRouterState({ select: (st) => st.location.pathname });

  const inScope = isPathEnabled(pathname);

  // M47 scope-switch overlay label: resolves the human target name from the
  // currently persisted shell scope so "Switching to …" stays accurate when
  // the operator flips between entity-wide and branch views.
  // M50.11: the welcome overlay was replaced by a dedicated /hrm/setup page.
  // While setup is PENDING the shell keeps non-confined operators on that
  // route so first-time operators land on the wizard; an already-complete
  // org is passed straight through by the backend's "complete" status.
  const setupPending =
    USE_REAL && !shellState.data?.confined && Boolean(shellState.data?.setupState) && (shellState.data?.setupState as { status?: string } | null)?.status === "pending";

  const switchTargetLabel = (() => {
    try {
      const raw = typeof localStorage !== "undefined" ? localStorage.getItem("erp.shell.state.v1") : null;
      if (raw) {
        const shell = JSON.parse(raw) as { entityId?: string; branch?: string } | null;
        if (shell?.branch) {
          const loc = liveLocations.find((l) => l.id === shell.branch);
          return loc ? `Switching to ${loc.name}…` : "Switching to branch…";
        }
        if (shell?.entityId) {
          const e = liveEntities.find((l) => String(l.id) === shell.entityId);
          const name = e ? String((e as Record<string, unknown>).registeredName ?? (e as Record<string, unknown>).tradingName ?? (e as Record<string, unknown>).name ?? "Organisation") : "Organisation";
          return `Switching to ${name} (organisation-wide)…`;
        }
      }
    } catch {
      /* corrupt shell state — fall through to nothing */
    }
    return "Switching context…";
  })();
  const liveNotifications = shellState.data?.notificationInbox.items ?? [];
  const unread = USE_REAL
    ? shellState.data?.notificationInbox.unreadCount ?? 0
    : notifications.filter((n) => n.unread).length;

  useEffect(() => {
    if (!USE_REAL || !liveEntities.length) return;
    if (!liveEntities.some((candidate) => String(candidate.id) === entityId))
      setEntityId(String(liveEntities[0].id));
  }, [entityId, liveEntities, setEntityId]);

  // M42: if the shell data fetch failed (e.g. a transient 401 during a session
  // refresh) the switcher would otherwise stay stuck with an empty entity
  // tree. Retry periodically while in real-API mode and errored.
  useEffect(() => {
    if (!USE_REAL) return;
    if (!shellState.error) return;
    const t = setTimeout(() => shellState.reload(), 3000);
    return () => clearTimeout(t);
  }, [shellState.error, shellState.reload]);

  useEffect(() => {
    if (!USE_REAL || !entityLocations.length) return;
    if (!entityLocations.some((candidate) => String(candidate.name ?? candidate.id) === branch))
      setBranch(String(entityLocations[0].name ?? entityLocations[0].id));
  }, [branch, entityLocations, setBranch]);

  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      const key = e.key.toLowerCase();
      if (key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setPaletteOpen((o) => !o);
      }
      if (key === "j" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setQuickAccessOpen((o) => !o);
      }
    };
    window.addEventListener("keydown", h);
    return () => window.removeEventListener("keydown", h);
  }, []);

  useEffect(() => {
    if (typeof localStorage !== "undefined") {
      setRailCollapsed(localStorage.getItem("erp.hrm.rail.collapsed") === "true");
    }
    setRailPreferenceReady(true);
  }, []);

  useEffect(() => {
    if (!railPreferenceReady) return;
    if (typeof localStorage === "undefined") return;
    localStorage.setItem("erp.hrm.rail.collapsed", String(railCollapsed));
  }, [railCollapsed, railPreferenceReady]);

  // M50.11: first-time gate — while setup is PENDING, keep non-confined
  // operators on the dedicated /hrm/setup wizard page (a full route, no
  // overlay). Avoids rendering side-effects during render by using an effect.
  useEffect(() => {
    if (setupPending && pathname !== "/hrm/setup") {
      router.navigate({ to: "/hrm/setup" });
    }
  }, [setupPending, pathname, router]);

  return (
    <div className="min-h-screen bg-background">
      {/* M47: full-screen frosted overlay with a rotating switch mark and
          "Switching to …" label while the org switcher changes scope. */}
      <ScopeSwitchOverlay targetLabel={switchTargetLabel} />

      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-50 focus:rounded focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-foreground"
      >
        Skip to content
      </a>

      <header className="sticky top-0 z-40 border-b border-primary/40 bg-primary text-primary-foreground shadow-sm">
        <div className="flex h-14 items-center gap-2 px-3">
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation">
                <Menu className="size-5" aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 bg-rail p-0 text-rail-foreground">
              <SheetHeader className="sr-only">
                <SheetTitle>Navigation</SheetTitle>
              </SheetHeader>
              <RailContent />
            </SheetContent>
          </Sheet>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="gap-2 px-2 text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground">
                <img data-company-logo="light" src="/mightyfin-logo-light.png" alt="Mightyfin HRMS" className="h-8 w-auto max-w-[132px] object-contain" />
                <span className="hidden font-semibold sm:inline">Mightyfin HRMS</span>
                <ChevronDown className="size-3.5" aria-hidden />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-64">
              <DropdownMenuLabel>Modules</DropdownMenuLabel>
              {modules.map((m) => (
                <DropdownMenuItem key={m.id} disabled={!m.available}>
                  {m.label}
                  {!m.available ? <span className="ml-auto text-xs text-muted-foreground">Not enabled</span> : null}
                </DropdownMenuItem>
              ))}
              <DropdownMenuSeparator />
              <DropdownMenuLabel>Workspace</DropdownMenuLabel>
              {USE_REAL ? (
                <div className="px-2 py-1.5 text-xs text-muted-foreground">
                  Assigned roles: {(user?.roles ?? []).filter((r) => HRM_STAFF_ROLES.includes(r as never)).join(", ") || "employee"}
                </div>
              ) : <DropdownMenuRadioGroup value={role} onValueChange={(v) => setRole(v as Role)}>
                {workspaces.map((w) => (
                  <DropdownMenuRadioItem key={w.id} value={w.id}>
                    {w.label}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>}
            </DropdownMenuContent>
          </DropdownMenu>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="hidden min-w-0 gap-2 md:flex">
                <Building2 className="size-4 shrink-0" aria-hidden />
                <span className="max-w-40 truncate font-medium">{entity ? String((entity as Record<string, unknown>).registeredName ?? (entity as Record<string, unknown>).tradingName ?? (entity as Record<string, unknown>).name ?? "Organisation") : "Organisation"}</span>
                {branch ? <span className="max-w-32 truncate text-muted-foreground">· {liveLocations.find((l) => l.id === branch)?.name ?? branch}</span> : null}
                <ChevronDown className="size-3.5 shrink-0" aria-hidden />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-96 max-h-[60vh] overflow-y-auto">
              <DropdownMenuLabel className="text-[10px] uppercase tracking-wide text-muted-foreground">
                Organisation context — entity and branch
              </DropdownMenuLabel>
              {entityTree.map((node) => (
                <div key={node.entityId}>
                  <DropdownMenuRadioGroup value={entityId} onValueChange={setEntityId}>
                    <DropdownMenuRadioItem value={node.entityId}>
                      <span className="min-w-0">
                        <span className="flex items-center gap-1.5">
                          <Building2 className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                          <span className="block truncate font-medium">{node.entityName}</span>
                        </span>
                        {node.entityCode ? (
                          <span className="block text-xs text-muted-foreground">{node.entityCode}</span>
                        ) : null}
                      </span>
                    </DropdownMenuRadioItem>
                  </DropdownMenuRadioGroup>
                  {node.branches.length > 0 ? (
                    <div className="ml-5 border-l border-border pl-3 py-0.5">
                      <DropdownMenuRadioGroup value={branch} onValueChange={setBranch}>
                        {node.branches.map((b) => (
                          <DropdownMenuRadioItem key={b.id} value={b.id} className="text-sm">
                            <span className="min-w-0 truncate">{b.name}</span>
                            {b.type ? <span className="text-xs text-muted-foreground">· {b.type}</span> : null}
                          </DropdownMenuRadioItem>
                        ))}
                      </DropdownMenuRadioGroup>
                    </div>
                  ) : (
                    <p className="ml-5 pl-4 pb-1.5 text-xs text-muted-foreground">No branches configured</p>
                  )}
                </div>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>

          <div className="ml-auto flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              className="gap-2 text-muted-foreground"
              onClick={() => setPaletteOpen(true)}
            >
              <Search className="size-4" aria-hidden />
              <span className="hidden sm:inline">Search</span>
              <kbd className="hidden rounded border px-1 text-[10px] sm:inline">⌘K</kbd>
            </Button>

            <Button
              variant="ghost"
              size="sm"
              className="gap-2 text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground"
              onClick={() => setQuickAccessOpen(true)}
              aria-label="Open Quick access"
              title="Quick access (⌘J)"
            >
              <LayoutGrid className="size-4" aria-hidden />
              <span className="hidden lg:inline">Quick access</span>
              <kbd className="hidden rounded border border-primary-foreground/30 px-1 text-[10px] lg:inline">⌘J</kbd>
            </Button>

            {canApprove ? (
              <Button asChild variant="ghost" size="icon" className="relative" aria-label="Tasks and approvals">
                <Link to="/hrm/approvals">
                  <CheckSquare className="size-5" aria-hidden />
                  {Number(pendingDecisions) > 0 ? (
                    <span className="absolute right-1 top-1 rounded-full bg-warning px-1 text-[10px] font-semibold text-warning-foreground">
                      {pendingDecisions}
                    </span>
                  ) : null}
                </Link>
              </Button>
            ) : null}

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="relative" aria-label={`Notifications, ${unread} unread`}>
                  <Bell className="size-5" aria-hidden />
                  {unread ? (
                    <span className="absolute right-1 top-1 rounded-full bg-danger px-1 text-[10px] font-semibold text-danger-foreground">
                      {unread}
                    </span>
                  ) : null}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-80">
                <DropdownMenuLabel>Notifications</DropdownMenuLabel>
                {(USE_REAL ? liveNotifications : notifications).map((n) => {
                  const row = n as Record<string, unknown>;
                  const target = String(row.actionUrl ?? row.to ?? "/hrm/self-service");
                  return <DropdownMenuItem key={String(row.id)} asChild>
                    <Link
                      to={target}
                      className="flex cursor-pointer flex-col items-start gap-0.5"
                    >
                      <span className="text-sm font-medium">{String(row.title ?? "HR update")}</span>
                      <span className="text-xs text-muted-foreground">{String(row.status ?? row.body ?? "")}</span>
                      <span className="text-[11px] text-muted-foreground">{row.createdAt ? new Date(String(row.createdAt)).toLocaleString() : String(row.at ?? "")}</span>
                    </Link>
                  </DropdownMenuItem>
                })}
                {USE_REAL && liveNotifications.length === 0 ? (
                  <DropdownMenuItem disabled>No notifications</DropdownMenuItem>
                ) : null}
              </DropdownMenuContent>
            </DropdownMenu>

            <Button variant="ghost" size="icon" aria-label="Help" asChild>
              <Link to="/hrm/help">
                <CircleHelp className="size-5" aria-hidden />
              </Link>
            </Button>

            <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label={`Switch to ${theme === "light" ? "dark" : "light"} mode`}>
              {theme === "light" ? <Moon className="size-5" aria-hidden /> : <Sun className="size-5" aria-hidden />}
            </Button>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="User menu">
                  <UserRound className="size-5" aria-hidden />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-64">
                <DropdownMenuLabel className="flex flex-col">
                  <RealUserLine />
                  <span className="text-xs font-normal text-muted-foreground">
                    Acting as {workspaces.find((w) => w.id === role)?.label}
                  </span>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  {/* M14 identity link: jump to the signed-in user's own worker record when linked. */}
                  <Link to={myWorker ? "/hrm/my-profile" : "/hrm/employees/$id"} params={{ id: myWorker?.id ?? "w-1001" }}>
                    My profile{myWorker ? "" : USE_REAL ? " (not linked)" : ""}
                  </Link>
                </DropdownMenuItem>
                {!USE_REAL ? <DropdownMenuItem asChild>
                  <Link to="/hrm/setup">Setup guide</Link>
                </DropdownMenuItem> : null}
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <RealSignOut />
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
        <Separator />
      </header>

      <div className="flex">
        <aside className={cn(
          "sticky top-14 hidden h-[calc(100vh-3.5rem)] shrink-0 bg-rail text-rail-foreground transition-[width] duration-200 lg:block",
          railCollapsed ? "w-16" : "w-64",
        )}>
          <RailContent
            collapsed={railCollapsed}
            onToggleCollapsed={() => setRailCollapsed((value) => !value)}
          />
        </aside>
        <main id="main" className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <div className="mx-auto flex max-w-6xl flex-col space-y-6">
            {inScope ? children : <ComingSoon />}
          </div>
        </main>
      </div>

      <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />
      <QuickAccessDialog open={quickAccessOpen} onOpenChange={setQuickAccessOpen} />
    </div>
  );
}
