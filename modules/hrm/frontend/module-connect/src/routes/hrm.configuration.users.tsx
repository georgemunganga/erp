import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { KeyRound, RefreshCw, UserPlus, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import {
  ApiError,
  hrmApi,
  type AuthCapabilities,
  type IdentityAccessUser,
  type IdentityDirectoryUser,
} from "@/platform/api-client";
import { realApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/users")({
  head: () => ({
    meta: [
      { title: "Users — HRM" },
      { name: "description", content: "Manage HRM user accounts, roles and access." },
      { property: "og:title", content: "Users — HRM" },
      { property: "og:description", content: "Manage HRM user accounts, roles and access." },
    ],
  }),
  component: UsersConfiguration,
});

interface RoleOption {
  roleKey: string;
  roleName: string;
  category: string;
  active: boolean;
}

const IDENTITY_ROLE_KEYS = new Set([
  "employee",
  "manager",
  "hr_ops",
  "payroll",
  "finance_approver",
  "hr_admin",
  "investigator",
]);

function messageFor(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : error instanceof Error ? error.message : fallback;
}

function UsersConfiguration() {
  const [users, setUsers] = useState<IdentityAccessUser[]>([]);
  const [capabilities, setCapabilities] = useState<AuthCapabilities | null>(null);
  const [identityAvailable, setIdentityAvailable] = useState(false);
  const [providerWarning, setProviderWarning] = useState<string | null>(null);
  const [roles, setRoles] = useState<RoleOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [role, setRole] = useState("hr_ops");
  const [creating, setCreating] = useState(false);
  const [searching, setSearching] = useState(false);
  const [directorySearched, setDirectorySearched] = useState(false);
  const [directoryUsers, setDirectoryUsers] = useState<IdentityDirectoryUser[]>([]);
  const [selectedDirectoryUser, setSelectedDirectoryUser] = useState<IdentityDirectoryUser | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<IdentityAccessUser | null>(null);

  const activeCount = useMemo(() => users.filter((user) => user.isActive).length, [users]);
  const localOnly = Boolean(capabilities?.localUsersEnabled && !capabilities.identityConfigured);
  const activeRoles = useMemo(
    () => roles.filter((r) => r.active && (localOnly || IDENTITY_ROLE_KEYS.has(r.roleKey))),
    [roles, localOnly],
  );
  const directoryRequired = Boolean(capabilities?.identityConfigured && !localOnly);
  const canAddUser = Boolean(capabilities && (localOnly || identityAvailable));

  useEffect(() => {
    if (activeRoles.length > 0 && !activeRoles.some((item) => item.roleKey === role))
      setRole(activeRoles[0].roleKey);
  }, [activeRoles, role]);

  const loadUsers = async () => {
    setLoading(true);
    setError(null);
    setProviderWarning(null);
    setCapabilities(null);
    setIdentityAvailable(false);
    try {
      const available = await hrmApi.auth.capabilities();
      setCapabilities(available);
      const [localResult, identityResult] = await Promise.allSettled([
        available.localUsersEnabled ? hrmApi.auth.users() : Promise.resolve({ items: [] }),
        available.identityConfigured ? hrmApi.identity.users() : Promise.resolve({ items: [] }),
      ]);
      const local = localResult.status === "fulfilled" ? localResult.value : { items: [] };
      const identity = identityResult.status === "fulfilled" ? identityResult.value : { items: [] };
      const idpReady = available.identityConfigured && identityResult.status === "fulfilled";
      setIdentityAvailable(idpReady);
      if (localResult.status === "rejected" && available.localUsersEnabled) {
        if (!idpReady) throw localResult.reason;
        setProviderWarning("Local HRM accounts could not be loaded. Organisation identities remain available.");
      }
      if (available.identityConfigured && identityResult.status === "rejected") {
        if (!available.localUsersEnabled) throw identityResult.reason;
        setProviderWarning("The organisation identity provider is unavailable. Existing local accounts can still be managed; adding users needs a directory check before it can resume.");
      } else if (!available.identityConfigured && !available.localUsersEnabled) {
        setProviderWarning("User management is unavailable: this server has no local accounts enabled and no identity provider configured.");
      }
      setUsers([
        ...(identity.items ?? []).map((user) => ({ ...user, source: "idp" as const })),
        ...(local.items ?? []).map((user) => ({
          id: user.id,
          email: user.email,
          displayName: user.displayName,
          roles: user.roles,
          isActive: user.isActive,
          federated: false,
          source: "local" as const,
        })),
      ]);
    } catch (err) {
      setError(messageFor(err, "Unable to load ERP identity users."));
    } finally {
      setLoading(false);
    }
  };

  const loadRoles = async () => {
    const rows = (await realApi.roles()) as Record<string, unknown>[];
    const nextRoles = rows.map((r) => ({
        roleKey: String(r.roleKey ?? ""),
        roleName: String(r.roleName ?? r.roleKey ?? ""),
        category: String(r.category ?? "hrm"),
        active: Boolean(r.active ?? true),
      }));
    setRoles(nextRoles);
    setRole((current) => nextRoles.some((item) => item.active && item.roleKey === current)
      ? current : nextRoles.find((item) => item.active)?.roleKey ?? current);
  };

  useEffect(() => {
    void Promise.all([loadUsers(), loadRoles()]).catch((err) =>
      setError(messageFor(err, "Unable to load local HRM access settings.")),
    );
  }, []);

  const searchDirectory = async () => {
    if (!directoryRequired || !identityAvailable) return;
    setSearching(true);
    setError(null);
    setSelectedDirectoryUser(null);
    try {
      const response = await hrmApi.identity.searchDirectory(email.trim());
      setDirectoryUsers(response.items ?? []);
      setDirectorySearched(true);
    } catch (err) {
      setError(messageFor(err, "Unable to search the organisation directory."));
    } finally {
      setSearching(false);
    }
  };

  const createUser = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setCreating(true);
    setError(null);
    setNotice(null);
    try {
      if (!capabilities || !canAddUser)
        throw new Error("User management is not available right now. Refresh the page and try again.");
      if (directoryRequired && !directorySearched)
        throw new Error("Search the organisation directory before adding this user.");
      if (directoryRequired && directoryUsers.length > 0 && !selectedDirectoryUser)
        throw new Error("Select the matching organisation identity before granting access.");
      if (selectedDirectoryUser) {
        await hrmApi.identity.inviteUser({
          email: selectedDirectoryUser.email,
          displayName: selectedDirectoryUser.displayName,
          roles: [role],
          sourceUserId: selectedDirectoryUser.id,
        });
      } else {
        if (!capabilities.localUsersEnabled)
          throw new Error("This server only accepts organisation identities. Select a directory user to grant access.");
        await hrmApi.auth.createUser({
          email: email.trim(),
          displayName: displayName.trim(),
          roles: [role],
        });
      }
      setEmail("");
      setDisplayName("");
      setRole(activeRoles[0]?.roleKey ?? "employee");
      setDirectoryUsers([]);
      setDirectorySearched(false);
      setSelectedDirectoryUser(null);
      setCreateOpen(false);
      setNotice(selectedDirectoryUser
        ? "The existing organisation identity was granted ERP access. No duplicate identity was created."
        : localOnly ? "The local HRM account was created. A password setup link was queued for email."
          : "A local HRM account was created because no matching organisation identity was found.");
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to invite the ERP user."));
    } finally {
      setCreating(false);
    }
  };

  const toggleUser = async (user: IdentityAccessUser, isActive: boolean) => {
    setError(null);
    setNotice(null);
    try {
      if (user.source === "local") await hrmApi.auth.updateUser(user.id, { isActive });
      else await hrmApi.identity.updateUser(user.id, { isActive });
      setNotice(`${user.email} is now ${isActive ? "active" : "inactive"}.`);
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to update account status."));
    }
  };

  const changeRole = async (user: IdentityAccessUser, nextRole: string) => {
    setError(null);
    setNotice(null);
    try {
      if (user.source === "local") await hrmApi.auth.updateUser(user.id, { roles: [nextRole] });
      else await hrmApi.identity.updateUser(user.id, { roles: [nextRole] });
      setNotice(`Role updated for ${user.email}.`);
      await loadUsers();
    } catch (err) {
      setError(messageFor(err, "Unable to update the account role."));
    }
  };

  const resetUserPassword = async () => {
    if (!selectedUser) return;
    setError(null);
    setNotice(null);
    try {
      if (selectedUser.source === "local") await hrmApi.auth.sendPasswordLink(selectedUser.id);
      else await hrmApi.identity.sendPasswordLink(selectedUser.id);
      setNotice(`A secure password reset link was emailed to ${selectedUser.email}.`);
      setSelectedUser(null);
    } catch (err) {
      setError(messageFor(err, "Unable to reset the account password."));
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Configuration · Security"
          title="HRM user access"
          description={localOnly
            ? "Manage local sign-in accounts and their HRM roles. New users set their password from an emailed link."
            : "Grant HRM access to organisation identities, with local accounts where this server allows them."}
          meta={<div className="flex flex-wrap justify-end gap-2"><Button size="sm" onClick={() => setCreateOpen(true)} disabled={!canAddUser || loading || activeRoles.length === 0}><UserPlus className="mr-2 size-3.5" aria-hidden />{localOnly ? "Add local user" : "Invite user"}</Button><Button variant="outline" size="sm" onClick={() => void loadUsers()}><RefreshCw className="mr-2 size-3.5" aria-hidden />Refresh</Button></div>}
        />

        {error ? <div className="mb-4 rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive" role="alert">{error}</div> : null}
        {providerWarning ? <div className="mb-4 rounded-lg border border-warning/40 bg-warning-soft p-3 text-sm text-warning" role="status">{providerWarning}</div> : null}
        {notice ? <div className="mb-4 rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm text-primary" role="status">{notice}</div> : null}

        <div className="grid gap-5">
          <section className="rounded-lg border bg-surface p-5" aria-labelledby="accounts-heading">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 id="accounts-heading" className="flex items-center gap-2 text-sm font-semibold"><Users className="size-4 text-primary" aria-hidden />Accounts</h2>
                <p className="mt-1 text-xs text-muted-foreground">{activeCount} active of {users.length} ERP user{users.length === 1 ? "" : "s"}. {activeRoles.length} assignable role{activeRoles.length === 1 ? "" : "s"}.</p>
              </div>
            </div>
            <div className="mt-4 overflow-x-auto rounded-md border">
              <table className="w-full min-w-[700px] text-left text-sm">
                <thead className="bg-surface-muted text-xs uppercase tracking-wide text-muted-foreground">
                  <tr><th className="px-3 py-2 font-medium">User</th><th className="px-3 py-2 font-medium">Role</th><th className="px-3 py-2 font-medium">Status</th><th className="px-3 py-2 text-right font-medium">Actions</th></tr>
                </thead>
                <tbody className="divide-y">
                  {loading ? <tr><td className="px-3 py-5 text-muted-foreground" colSpan={4}>Loading user accounts…</td></tr> : null}
                  {!loading && users.length === 0 ? <tr><td className="px-3 py-5 text-muted-foreground" colSpan={4}>No HRM users found.</td></tr> : null}
                  {!loading && users.map((user) => {
                    const currentRole = user.roles.find((candidate) => roles.some((item) => item.roleKey === candidate)) ?? "employee";
                    return <tr key={user.id}>
                      <td className="px-3 py-3"><span className="font-medium text-foreground">{user.displayName}</span><div className="text-xs text-muted-foreground">{user.email}</div><div className="mt-1 text-[11px] text-muted-foreground">{user.source === "local" ? "HRMS local account" : user.federated ? "Shared staff identity" : "ERP realm identity"}</div></td>
                      <td className="px-3 py-3"><select className="h-8 rounded-md border bg-background px-2 text-xs" value={currentRole} onChange={(event) => void changeRole(user, event.target.value)} aria-label={`Role for ${user.email}`}>{roles.filter((role) => user.source === "local" || IDENTITY_ROLE_KEYS.has(role.roleKey)).map((role) => <option key={role.roleKey} value={role.roleKey} disabled={!role.active}>{role.roleName}{role.active ? "" : " (inactive)"}</option>)}</select></td>
                      <td className="px-3 py-3"><StatusBadge status={user.isActive ? "active" : "inactive"} /></td>
                      <td className="px-3 py-3"><div className="flex justify-end gap-2"><Button variant="outline" size="sm" onClick={() => setSelectedUser(user)}><KeyRound className="mr-1.5 size-3.5" aria-hidden />Send reset link</Button><Switch checked={user.isActive} onCheckedChange={(checked) => void toggleUser(user, checked)} aria-label={`${user.isActive ? "Deactivate" : "Activate"} ${user.email}`} /></div></td>
                    </tr>;
                  })}
                </tbody>
              </table>
            </div>
          </section>
        </div>

        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{localOnly ? "Add local HRM user" : "Invite HRM user"}</DialogTitle>
              <DialogDescription>{localOnly
                ? "Enter the account holder's name, email and role. They will choose a password using an emailed setup link."
                : "Search the organisation directory first. Select an existing identity when found; otherwise HRM can create a local account if enabled."}</DialogDescription>
            </DialogHeader>
            <form className="space-y-3" onSubmit={createUser}>
              <div>
                <Label htmlFor="new-email">{localOnly ? "Email address" : "Work email or directory search"}</Label>
                <div className="mt-1 flex gap-2">
                  <Input id="new-email" type="email" autoComplete="off" value={email} onChange={(event) => { setEmail(event.target.value); setDirectorySearched(false); setDirectoryUsers([]); setSelectedDirectoryUser(null); }} required />
                  {directoryRequired ? <Button type="button" variant="outline" onClick={() => void searchDirectory()} disabled={searching || email.trim().length < 2 || !identityAvailable}>{searching ? "Searching…" : "Search directory"}</Button> : null}
                </div>
              </div>
              {directoryRequired && directorySearched && directoryUsers.length > 0 ? (
                <div className="space-y-2 rounded-md border p-3">
                  <p className="text-xs font-medium">Select from organisation identity provider</p>
                  {directoryUsers.map((candidate) => (
                    <button key={candidate.id} type="button" className={`block w-full rounded border p-2 text-left text-sm ${selectedDirectoryUser?.id === candidate.id ? "border-primary bg-primary/5" : "border-border"}`} onClick={() => { setSelectedDirectoryUser(candidate); setEmail(candidate.email); setDisplayName(candidate.displayName); }}>
                      <span className="block font-medium">{candidate.displayName}</span>
                      <span className="block text-xs text-muted-foreground">{candidate.email}</span>
                    </button>
                  ))}
                  <p className="text-xs text-muted-foreground">An existing IdP identity must be selected; HRMS will not duplicate it.</p>
                </div>
              ) : null}
              {directoryRequired && directorySearched && directoryUsers.length === 0 ? (
                <div className="rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
                  {capabilities?.localUsersEnabled
                    ? "No organisation identity matched. Saving will create a local HRM account."
                    : "No organisation identity matched. Ask the identity provider administrator to create one before granting HRM access."}
                </div>
              ) : null}
              <div><Label htmlFor="new-display-name">Name</Label><Input id="new-display-name" className="mt-1" value={displayName} onChange={(event) => setDisplayName(event.target.value)} readOnly={Boolean(selectedDirectoryUser)} required /></div>
              <div><Label htmlFor="new-role">Role</Label><select id="new-role" className="mt-1 flex h-9 w-full rounded-md border bg-background px-3 text-sm" value={role} onChange={(event) => setRole(event.target.value)}>{activeRoles.map((role) => <option key={role.roleKey} value={role.roleKey}>{role.roleName}</option>)}</select></div>
              <DialogFooter><Button variant="outline" type="button" onClick={() => setCreateOpen(false)}>Cancel</Button><Button type="submit" disabled={creating || !canAddUser || (directoryRequired && !directorySearched) || (directoryRequired && !capabilities?.localUsersEnabled && !selectedDirectoryUser)}>{creating ? "Saving…" : selectedDirectoryUser ? "Grant HRM access" : capabilities?.localUsersEnabled ? "Create local HRM account" : "Select a directory user"}</Button></DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        <Dialog open={Boolean(selectedUser)} onOpenChange={(open) => { if (!open) setSelectedUser(null); }}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Send password reset link</DialogTitle>
              <DialogDescription>Email a one-time password reset link to {selectedUser?.email}. The link expires after 24 hours.</DialogDescription>
            </DialogHeader>
            <DialogFooter><Button variant="outline" type="button" onClick={() => setSelectedUser(null)}>Cancel</Button><Button onClick={() => void resetUserPassword()}>Send reset link</Button></DialogFooter>
          </DialogContent>
        </Dialog>
      </AppShell>
    </AuthGate>
  );
}
