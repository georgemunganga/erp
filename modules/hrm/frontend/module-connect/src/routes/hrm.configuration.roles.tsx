import { createFileRoute, Navigate } from "@tanstack/react-router";
import { useState } from "react";
import { Plus, Save, ShieldCheck, ShieldOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ConfigurationShell } from "@/modules/core/ConfigurationShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/configuration/roles")({
  head: () => ({
    meta: [
      { title: "Roles and permissions - Newworldcargo HRM" },
      { name: "description", content: "Create HRMS roles and assign the permissions each role grants." },
      { property: "og:title", content: "Roles and permissions - Newworldcargo HRM" },
      { property: "og:description", content: "Create HRMS roles and assign the permissions each role grants." },
    ],
  }),
  component: () => <Navigate to="/core/workspace/settings/roles" replace />,
});

const description =
  "Create tenant roles, decide which HRMS permissions each role grants, and switch roles on or off for new sessions.";

const permissionOptions = [
  { key: "timesheet_operator", label: "Timesheet operations", detail: "View, record and import attendance for employees. Overtime approval, payroll and administration require separate permissions." },
  { key: "employee", label: "Employee self-service", detail: "Own profile, leave, payslips and personal HR requests." },
  { key: "manager", label: "Manager approvals", detail: "Team approvals, time review and manager work queues." },
  { key: "hr_ops", label: "HR operations", detail: "Employee records, onboarding, leave and day-to-day HR administration." },
  { key: "payroll", label: "Payroll operations", detail: "Payroll runs, calculations, payslips, payment files and payroll reports." },
  { key: "finance_approver", label: "Finance approval", detail: "Finance checks and readiness controls around payroll payment release." },
  { key: "investigator", label: "Relations investigation", detail: "Employee relations, protected disclosures and investigation work." },
  { key: "hr_admin", label: "HRMS administration", detail: "Full HRMS setup, users, roles, configuration and privileged controls." },
];

const categoryLabels: Record<string, string> = {
  hrm: "HR administration",
  payroll: "Payroll",
  system: "System",
};

interface RoleRow {
  id: string;
  roleKey: string;
  roleName: string;
  category: string;
  active: boolean;
  permissions: string[];
}

interface DraftRole {
  roleKey: string;
  roleName: string;
  category: string;
  permissions: string[];
  active: boolean;
}

const emptyDraft: DraftRole = {
  roleKey: "",
  roleName: "",
  category: "hrm",
  permissions: ["employee"],
  active: true,
};

function normalizeRows(rows: unknown): RoleRow[] {
  return ((rows ?? []) as Record<string, unknown>[]).map((r) => ({
    id: String(r.id ?? ""),
    roleKey: String(r.roleKey ?? ""),
    roleName: String(r.roleName ?? r.roleKey ?? ""),
    category: String(r.category ?? "hrm"),
    active: Boolean(r.active ?? true),
    permissions: Array.isArray(r.permissions) ? r.permissions.map(String) : [],
  }));
}

function togglePermission(values: string[], key: string, checked: boolean) {
  const next = checked ? [...values, key] : values.filter((value) => value !== key);
  return Array.from(new Set(next));
}

function PermissionGrid({ values, onChange }: { values: string[]; onChange: (next: string[]) => void }) {
  return (
    <div className="grid gap-2 sm:grid-cols-2">
      {permissionOptions.map((permission) => (
        <label key={permission.key} className="flex items-start gap-3 rounded-lg border bg-background p-3 text-sm">
          <Checkbox
            checked={values.includes(permission.key)}
            onCheckedChange={(checked) => onChange(togglePermission(values, permission.key, checked === true))}
            aria-label={permission.label}
          />
          <span className="min-w-0">
            <span className="block font-medium">{permission.label}</span>
            <span className="mt-0.5 block text-xs leading-5 text-muted-foreground">{permission.detail}</span>
          </span>
        </label>
      ))}
    </div>
  );
}

export function RolesConfig() {
  const [tick, setTick] = useState(0);
  const [draft, setDraft] = useState<DraftRole>(emptyDraft);
  const [editing, setEditing] = useState<Record<string, DraftRole>>({});
  const [saving, setSaving] = useState<string | null>(null);
  const state = useApi(() => realApi.roles(), [tick]);

  const createRole = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving("new");
    try {
      await realApi.createRole(draft);
      feedback.saved(`${draft.roleName} created.`);
      setDraft(emptyDraft);
      setTick((t) => t + 1);
    } catch (err) {
      feedback.blocked("Could not create role", err instanceof Error ? err.message : "Unknown error");
    } finally {
      setSaving(null);
    }
  };

  const saveRole = async (role: RoleRow, patch: Partial<DraftRole>) => {
    setSaving(role.roleKey);
    try {
      await realApi.updateRole(role.roleKey, patch);
      feedback.saved(`${role.roleName} updated.`);
      setEditing((current) => {
        const next = { ...current };
        delete next[role.roleKey];
        return next;
      });
      setTick((t) => t + 1);
    } catch (err) {
      feedback.blocked("Could not update role", err instanceof Error ? err.message : "Unknown error");
    } finally {
      setSaving(null);
    }
  };

  return (
    <AuthGate>
      <ConfigurationShell>
        <PageHeader eyebrow="Configuration" title="Roles and permissions" description={description} />
        <Async state={state}>
          {(rows) => {
            const roleRows = normalizeRows(rows);
            const activeCount = roleRows.filter((r) => r.active).length;
            const permissionsInUse = new Set(roleRows.flatMap((r) => r.permissions));

            return (
              <div className="space-y-6">
                <dl className="grid gap-3 sm:grid-cols-3">
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Roles</dt>
                    <dd className="mt-1 text-lg font-semibold">{roleRows.length}</dd>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Active</dt>
                    <dd className="mt-1 text-lg font-semibold">{activeCount}</dd>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Permissions used</dt>
                    <dd className="mt-1 text-lg font-semibold">{permissionsInUse.size}</dd>
                  </div>
                </dl>

                <section className="rounded-lg border bg-surface p-5" aria-labelledby="new-role-heading">
                  <h2 id="new-role-heading" className="flex items-center gap-2 text-sm font-semibold">
                    <Plus className="size-4 text-primary" aria-hidden /> Add role
                  </h2>
                  <form className="mt-4 space-y-4" onSubmit={createRole}>
                    <div className="grid gap-3 md:grid-cols-[0.8fr_1fr_0.7fr]">
                      <div>
                        <Label htmlFor="role-key">Role key</Label>
                        <Input id="role-key" className="mt-1" value={draft.roleKey} onChange={(event) => setDraft((d) => ({ ...d, roleKey: event.target.value }))} placeholder="hr_assistant" required />
                      </div>
                      <div>
                        <Label htmlFor="role-name">Role name</Label>
                        <Input id="role-name" className="mt-1" value={draft.roleName} onChange={(event) => setDraft((d) => ({ ...d, roleName: event.target.value }))} placeholder="HR Assistant" required />
                      </div>
                      <div>
                        <Label htmlFor="role-category">Category</Label>
                        <select id="role-category" className="mt-1 flex h-9 w-full rounded-md border bg-background px-3 text-sm" value={draft.category} onChange={(event) => setDraft((d) => ({ ...d, category: event.target.value }))}>
                          {Object.entries(categoryLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                        </select>
                      </div>
                    </div>
                    <PermissionGrid values={draft.permissions} onChange={(permissions) => setDraft((d) => ({ ...d, permissions }))} />
                    <div className="flex justify-end">
                      <Button type="submit" disabled={saving === "new" || draft.permissions.length === 0}>
                        <Plus className="size-4" aria-hidden /> {saving === "new" ? "Creating..." : "Create role"}
                      </Button>
                    </div>
                  </form>
                </section>

                <div className="space-y-3">
                  {roleRows.map((role) => {
                    const draftRole = editing[role.roleKey] ?? {
                      roleKey: role.roleKey,
                      roleName: role.roleName,
                      category: role.category,
                      permissions: role.permissions.length ? role.permissions : [role.roleKey],
                      active: role.active,
                    };
                    const dirty =
                      draftRole.roleName !== role.roleName ||
                      draftRole.category !== role.category ||
                      draftRole.active !== role.active ||
                      draftRole.permissions.join(",") !== role.permissions.join(",");
                    return (
                      <section key={role.roleKey} className="rounded-lg border bg-surface p-5">
                        <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
                          <div className="flex min-w-0 flex-1 gap-3">
                            {draftRole.active ? <ShieldCheck aria-hidden className="mt-1 size-5 text-primary" /> : <ShieldOff aria-hidden className="mt-1 size-5 text-muted-foreground" />}
                            <div className="min-w-0 flex-1 space-y-3">
                              <div className="flex flex-wrap items-center gap-2">
                                <Input className="h-9 max-w-sm font-medium" value={draftRole.roleName} onChange={(event) => setEditing((current) => ({ ...current, [role.roleKey]: { ...draftRole, roleName: event.target.value } }))} aria-label={`Name for ${role.roleKey}`} />
                                <StatusBadge status={draftRole.active ? "active" : "inactive"} />
                                <code className="rounded bg-surface-muted px-2 py-1 text-xs text-muted-foreground">{role.roleKey}</code>
                              </div>
                              <div className="max-w-xs">
                                <Label className="text-xs text-muted-foreground">Category</Label>
                                <select className="mt-1 flex h-9 w-full rounded-md border bg-background px-3 text-sm" value={draftRole.category} onChange={(event) => setEditing((current) => ({ ...current, [role.roleKey]: { ...draftRole, category: event.target.value } }))} aria-label={`Category for ${role.roleKey}`}>
                                  {Object.entries(categoryLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                                </select>
                              </div>
                              <PermissionGrid values={draftRole.permissions} onChange={(permissions) => setEditing((current) => ({ ...current, [role.roleKey]: { ...draftRole, permissions } }))} />
                            </div>
                          </div>
                          <div className="flex items-center justify-end gap-3 lg:min-w-56">
                            <Switch checked={draftRole.active} onCheckedChange={(active) => setEditing((current) => ({ ...current, [role.roleKey]: { ...draftRole, active } }))} aria-label={`Toggle ${role.roleName}`} />
                            <Button size="sm" disabled={!dirty || saving === role.roleKey || draftRole.permissions.length === 0} onClick={() => void saveRole(role, draftRole)}>
                              <Save className="size-4" aria-hidden /> {saving === role.roleKey ? "Saving..." : "Save"}
                            </Button>
                          </div>
                        </div>
                      </section>
                    );
                  })}
                </div>
              </div>
            );
          }}
        </Async>
      </ConfigurationShell>
    </AuthGate>
  );
}
