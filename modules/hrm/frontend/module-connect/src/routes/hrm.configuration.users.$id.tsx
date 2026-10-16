import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { ArrowLeft, KeyRound, Monitor, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConfigurationShell } from "@/modules/core/ConfigurationShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { hrmApi, type LocalAuthUser } from "@/platform/api-client";

export const Route = createFileRoute("/hrm/configuration/users/$id")({
  beforeLoad: ({ params }) => { throw redirect({ to: "/core/workspace/settings/users/$id", params: { id: params.id }, replace: true }); },
  component: () => null,
});

type Detail = { user: LocalAuthUser; activity: { action: string; actor: string; at: string }[]; sessions: { createdAt: string; lastSeenAt: string; expiresAt: string; revokedAt?: string | null; userAgent?: string | null }[] };
const date = (value?: string | null) => value ? new Intl.DateTimeFormat("en-ZM", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : "—";

export function UserDetail({ id }: { id: string }) {
  const [data, setData] = useState<Detail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const load = async () => { try { setData(await hrmApi.auth.user(id)); } catch (e) { setError(e instanceof Error ? e.message : "Unable to load this account."); } };
  useEffect(() => { void load(); }, [id]);
  const sendReset = async () => { setSending(true); setError(null); try { await hrmApi.auth.sendPasswordLink(id); await load(); } catch (e) { setError(e instanceof Error ? e.message : "Unable to send a password reset link."); } finally { setSending(false); } };
  return <AuthGate><ConfigurationShell><PageHeader eyebrow="Configuration · Security" title={data?.user.displayName ?? "System user"} description={data?.user.email ?? "Loading account details…"} meta={<div className="flex gap-2"><Button variant="outline" asChild><Link to="/core/workspace/settings/users"><ArrowLeft className="mr-2 size-4" />Users</Link></Button><Button onClick={() => void sendReset()} disabled={sending}><KeyRound className="mr-2 size-4" />{sending ? "Sending…" : "Send password reset"}</Button></div>} />
    {error ? <div className="mb-4 rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive">{error}</div> : null}
    {!data ? <p className="text-sm text-muted-foreground">Loading account…</p> : <div className="grid gap-5 lg:grid-cols-2">
      <section className="rounded-lg border bg-surface p-5"><h2 className="flex items-center gap-2 text-sm font-semibold"><ShieldCheck className="size-4 text-primary" />Account</h2><dl className="mt-4 grid gap-3 text-sm"><div><dt className="text-muted-foreground">Role</dt><dd>{data.user.roles.join(", ") || "Employee"}</dd></div><div><dt className="text-muted-foreground">Status</dt><dd className="mt-1"><StatusBadge status={data.user.isActive ? "active" : "inactive"} /></dd></div><div><dt className="text-muted-foreground">Password</dt><dd>{data.user.mustChangePassword ? "Password setup required" : "Set"}</dd></div><div><dt className="text-muted-foreground">Last sign in</dt><dd>{date(data.user.lastLoginAt)}</dd></div><div><dt className="text-muted-foreground">Created</dt><dd>{date(data.user.createdAt)}</dd></div></dl></section>
      <section className="rounded-lg border bg-surface p-5"><h2 className="flex items-center gap-2 text-sm font-semibold"><Monitor className="size-4 text-primary" />Sign-in sessions</h2><div className="mt-4 space-y-3 text-sm">{data.sessions.length ? data.sessions.map((s, i) => <div key={i} className="border-b pb-3 last:border-0"><div>{s.revokedAt ? "Revoked" : "Active"} · last seen {date(s.lastSeenAt)}</div><div className="mt-1 text-xs text-muted-foreground">{s.userAgent || "Unknown device"}</div></div>) : <p className="text-muted-foreground">No sign-in sessions yet.</p>}</div></section>
      <section className="rounded-lg border bg-surface p-5 lg:col-span-2"><h2 className="text-sm font-semibold">Account activity</h2><div className="mt-4 space-y-3 text-sm">{data.activity.length ? data.activity.map((a, i) => <div key={i} className="flex justify-between gap-4 border-b pb-3 last:border-0"><span className="capitalize">{a.action} account record · {a.actor}</span><span className="shrink-0 text-muted-foreground">{date(a.at)}</span></div>) : <p className="text-muted-foreground">No recorded account changes yet.</p>}</div></section>
    </div>}</ConfigurationShell></AuthGate>;
}
