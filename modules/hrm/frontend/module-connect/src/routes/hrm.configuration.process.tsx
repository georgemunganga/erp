import { createFileRoute, redirect } from "@tanstack/react-router";
import { useState } from "react";
import { ArrowRight, Lock, Pencil } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EditDrawer } from "@/platform/components/EditDrawer";
import { feedback } from "@/platform/feedback";
import { Switch } from "@/components/ui/switch";
import { configurationApi } from "@/mock/configuration";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/configuration/process")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "process" }, replace: true }); },
  head: () => ({
    meta: [
      { title: "Process design — Newworldcargo HRM" },
      { name: "description", content: "Leave policies, shift rules, approval routing, request categories, forms, automation and templates." },
      { property: "og:title", content: "Process design — Newworldcargo HRM" },
      { property: "og:description", content: "Leave policies, shift rules, approval routing, request categories, forms, automation and templates." },
    ],
  }),
  component: ProcessConfig,
});

const SECTIONS = [
  { id: "leave", label: "Leave policies" },
  { id: "shift", label: "Attendance and shifts" },
  { id: "routing", label: "Approval routing" },
  { id: "requests", label: "Request categories" },
  { id: "forms", label: "Forms and fields" },
  { id: "auto", label: "Automation" },
  { id: "templates", label: "Templates" },
  { id: "self", label: "Self-service" },
];

export function ProcessConfig() {
  const [tab, setTab] = useState("leave");
  const [editing, setEditing] = useState<{ name: string; entitlement: string; carryOver: string } | null>(null);
  const leave = useMock(() => configurationApi.leavePolicies());
  const shifts = useMock(() => configurationApi.shiftRules());
  const routes = useMock(() => configurationApi.approvalRoutes());
  const cats = useMock(() => configurationApi.requestCategories());
  const forms = useMock(() => configurationApi.forms());
  const autos = useMock(() => configurationApi.automations());
  const templates = useMock(() => configurationApi.templates());
  const self = useMock(() => configurationApi.selfService());

  return (
    <ConfigPage
      title="Process design"
      description="How work moves: what people are entitled to, who decides, and what happens automatically."
      sections={SECTIONS}
      active={tab}
      onSelect={setTab}
    >
      {tab === "leave" ? (
        <Async state={leave} rows={5}>
          {(rows) => (
            <ConfigTable
              caption="Leave policies with entitlement, accrual and evidence"
              minWidth="44rem"
              headers={["Policy", "Entitlement", "Accrual", "Carry-over", "Evidence", "Status", ""]}
              rows={rows.map((p) => [
                <span className="font-medium">{p.name}</span>,
                <span className="text-xs">{p.entitlement}</span>,
                <span className="text-xs">{p.accrual}</span>,
                <span className="text-xs">{p.carryOver}</span>,
                <span className="text-xs">{p.evidence}</span>,
                <span className={`text-xs ${p.status === "Draft" ? "text-warning" : ""}`}>{p.status}</span>,
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 gap-1.5 px-2 text-xs"
                  onClick={() => setEditing({ name: p.name, entitlement: p.entitlement, carryOver: p.carryOver })}
                >
                  <Pencil className="size-3.5" aria-hidden />
                  Edit
                </Button>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "shift" ? (
        <Async state={shifts} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Attendance and shift rules"
              minWidth="30rem"
              headers={["Rule", "Value", "Applies to"]}
              rows={rows.map((r) => [
                <span className="font-medium">{r.rule}</span>,
                <span className="text-sm">{r.value}</span>,
                <span className="text-xs text-muted-foreground">{r.appliesTo}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "routing" ? (
        <Async state={routes} rows={5}>
          {(rows) => (
            <ul className="space-y-2">
              {rows.map((r) => (
                <li key={r.id} className="rounded-lg border bg-surface p-4">
                  <p className="text-sm font-medium">{r.what}</p>
                  <ol className="mt-2 flex flex-wrap items-center gap-1.5">
                    {r.steps.map((s, i) => (
                      <li key={s} className="flex items-center gap-1.5">
                        <span className="rounded-full border bg-surface-muted px-2.5 py-1 text-xs">{s}</span>
                        {i < r.steps.length - 1 ? (
                          <ArrowRight className="size-3 shrink-0 text-muted-foreground" aria-hidden />
                        ) : null}
                      </li>
                    ))}
                  </ol>
                  <p className="mt-2 text-xs text-muted-foreground">Escalates to {r.escalation}</p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "requests" ? (
        <Async state={cats} rows={5}>
          {(rows) => (
            <ConfigTable
              caption="HR request categories with service targets"
              minWidth="32rem"
              headers={["Category", "Response target", "Owner", "Confidential"]}
              rows={rows.map((c) => [
                <span className="font-medium">{c.category}</span>,
                <span className="text-sm">{c.target}</span>,
                <span className="text-xs">{c.owner}</span>,
                c.confidential ? (
                  <span className="inline-flex items-center gap-1.5 text-xs text-info">
                    <Lock className="size-3.5 shrink-0" aria-hidden />
                    Restricted to the owner
                  </span>
                ) : (
                  <span className="text-xs text-muted-foreground">Standard</span>
                ),
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "forms" ? (
        <Async state={forms} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Forms and their custom fields"
              minWidth="34rem"
              headers={["Form", "Fields", "Custom", "Used by", "Status"]}
              rows={rows.map((f) => [
                <span className="font-medium">{f.name}</span>,
                <span className="tabular">{f.fields}</span>,
                <span className="tabular">{f.custom}</span>,
                <span className="text-xs text-muted-foreground">{f.usedBy}</span>,
                <span className={`text-xs ${f.status === "Draft" ? "text-warning" : ""}`}>{f.status}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "auto" ? (
        <Async state={autos} rows={4}>
          {(rows) => (
            <ul className="space-y-2">
              {rows.map((a) => (
                <li key={a.id} className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface p-4">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium">{a.name}</span>
                    <span className="block text-xs text-muted-foreground">
                      When {a.trigger.toLowerCase()} → {a.action.toLowerCase()}
                    </span>
                    {a.lastRun ? (
                      <span className="block text-[11px] text-muted-foreground">Last ran {a.lastRun}</span>
                    ) : (
                      <span className="block text-[11px] text-muted-foreground">Never run</span>
                    )}
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    <span className="text-xs">{a.state}</span>
                    <Switch
                      checked={a.state === "On"}
                      aria-label={a.name}
                      onCheckedChange={(on) =>
                        feedback.saved(`${a.name} turned ${on ? "on" : "off"}.`)
                      }
                    />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "templates" ? (
        <Async state={templates} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Letter and notification templates"
              minWidth="32rem"
              headers={["Template", "Channel", "Languages", "Updated"]}
              rows={rows.map((t) => [
                <span className="font-medium">{t.name}</span>,
                t.channel,
                <span className="text-xs">{t.language}</span>,
                <span className="tabular text-xs">{t.updated}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "self" ? (
        <Async state={self} rows={4}>
          {(rows) => (
            <ul className="space-y-2">
              {rows.map((s) => (
                <li key={s.id} className="flex flex-wrap items-start gap-3 rounded-lg border bg-surface p-4">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium">{s.what}</span>
                    <span className="block text-xs text-muted-foreground">{s.who}</span>
                    {s.note ? <span className="mt-1 block text-xs">{s.note}</span> : null}
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    <span className="text-xs">{s.on ? "On" : "Off"}</span>
                    <Switch
                      checked={s.on}
                      aria-label={s.what}
                      onCheckedChange={(on) =>
                        on || s.id !== "SS-04"
                          ? feedback.saved(`${s.what} turned ${on ? "on" : "off"}.`)
                          : feedback.blocked("Cannot be turned on", "Leave always needs a decision from someone.")
                      }
                    />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      <EditDrawer
        open={editing !== null}
        onOpenChange={(o) => !o && setEditing(null)}
        title={editing ? `Edit ${editing.name}` : "Edit policy"}
        description="Applies to leave taken from the effective date. Balances already accrued are not recalculated."
        initial={{ entitlement: editing?.entitlement ?? "", carryOver: editing?.carryOver ?? "" }}
        fields={[
          { name: "entitlement", label: "Entitlement", required: true, hint: "Zambian statutory minimum is 24 days a year." },
          { name: "carryOver", label: "Carry-over rule", required: true },
        ]}
        saveLabel="Save policy"
        onSave={() =>
          feedback.saved(
            `${editing?.name ?? "Policy"} updated.`,
            () => feedback.note("Policy change reverted."),
          )
        }
        footerNote="Employees are notified of a change that reduces an entitlement."
      />
    </ConfigPage>
  );
}
