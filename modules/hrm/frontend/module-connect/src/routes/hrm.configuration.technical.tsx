import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import {
  BellRing,
  Check,
  CircleDashed,
  Download,
  RefreshCw,
  TriangleAlert,
  Upload,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { configurationApi } from "@/mock/configuration";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/technical")({
  head: () => ({
    meta: [
      { title: "Technical settings — Newworldcargo HRM" },
      {
        name: "description",
        content: "Integrations, import and export, numbering series and HR vendors.",
      },
      { property: "og:title", content: "Technical settings — Newworldcargo HRM" },
      {
        property: "og:description",
        content: "Integrations, import and export, numbering series and HR vendors.",
      },
    ],
  }),
  component: TechnicalConfig,
});

const SECTIONS = [
  { id: "integrations", label: "Integrations" },
  { id: "notifications", label: "Notification delivery" },
  { id: "data", label: "Import and export" },
  { id: "numbering", label: "Numbering" },
  { id: "vendors", label: "Vendors" },
];
const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function TechnicalConfig() {
  const [tab, setTab] = useState(USE_REAL ? "notifications" : "integrations");
  const integrations = useMock(() => configurationApi.integrations());
  const numbering = useMock(() => configurationApi.numberSeries());
  const vendors = useMock(() => configurationApi.vendors());
  const notifications = useApi(() => realApi.notificationDeliveries({ limit: 100 }));

  const sections = USE_REAL
    ? SECTIONS.filter((section) => section.id === "notifications")
    : SECTIONS;

  async function retryNotification(id: string) {
    try {
      await realApi.retryNotification(id);
      feedback.submitted(
        "Notification queued for retry.",
        "The outbox publisher will attempt the handoff again.",
      );
      notifications.reload();
    } catch (error) {
      feedback.blocked(
        "Retry failed.",
        error instanceof Error ? error.message : "Try again later.",
      );
    }
  }

  return (
    <ConfigPage
      title="Technical settings"
      description="What HRM connects to, and what it hands over. Rarely changed after go-live."
      sections={sections}
      active={tab}
      onSelect={setTab}
      notice={
        tab === "notifications"
          ? "This view is read-only except for retrying a failed handoff. Every retry remains traceable in the outbox audit history."
          : undefined
      }
    >
      {tab === "integrations" ? (
        <Async state={integrations} rows={4}>
          {(rows) => (
            <ul className="space-y-2">
              {rows.map((i) => (
                <li key={i.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {i.state === "Connected" ? (
                      <Check className="size-4 shrink-0 text-success" aria-hidden />
                    ) : i.state === "Error" ? (
                      <TriangleAlert className="size-4 shrink-0 text-danger" aria-hidden />
                    ) : (
                      <CircleDashed className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    )}
                    <span className="text-sm font-medium">{i.name}</span>
                    <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                      {i.direction}
                    </span>
                    <span
                      className={`text-xs font-medium ${
                        i.state === "Connected"
                          ? "text-success"
                          : i.state === "Error"
                            ? "text-danger"
                            : "text-muted-foreground"
                      }`}
                    >
                      {i.state}
                    </span>
                    {i.lastSync ? (
                      <span className="text-[11px] text-muted-foreground">
                        last sync {i.lastSync}
                      </span>
                    ) : null}
                  </div>
                  <p className="mt-1.5 text-xs text-muted-foreground">{i.note}</p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "notifications" ? (
        <Async state={notifications} rows={5}>
          {(delivery) => (
            <div className="space-y-4" data-testid="notification-delivery-status">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                {[
                  ["Pending", delivery.pending, "text-warning"],
                  ["Publishing", delivery.publishing, "text-info"],
                  ["Published", delivery.published, "text-success"],
                  ["Failed", delivery.failed, "text-danger"],
                  ["SMTP fallback", delivery.fallbackDelivered, "text-muted-foreground"],
                ].map(([label, value, tone]) => (
                  <div key={String(label)} className="rounded-lg border bg-surface p-4">
                    <p className="text-xs text-muted-foreground">{label}</p>
                    <p className={`mt-1 text-2xl font-semibold tabular-nums ${tone}`}>{value}</p>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border bg-surface p-4">
                <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="flex items-center gap-2 text-sm font-semibold">
                      <BellRing className="size-4" aria-hidden />
                      HRM event handoff
                    </h2>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Tracks delivery from HRM to NATS/JetStream. Provider delivery remains visible
                      in Novu.
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    className="gap-1.5"
                    onClick={notifications.reload}
                  >
                    <RefreshCw className="size-3.5" aria-hidden /> Refresh
                  </Button>
                </div>
                <ConfigTable
                  caption="Recent notification handoffs; employee addresses and event payloads are intentionally hidden"
                  minWidth="58rem"
                  headers={[
                    "Event",
                    "Status",
                    "Attempts",
                    "Transport",
                    "Created",
                    "Trace",
                    "Action",
                  ]}
                  rows={delivery.items.map((item) => [
                    <span className="font-mono text-xs">{item.eventType}</span>,
                    <span
                      className={
                        item.status === "failed"
                          ? "text-xs font-medium text-danger"
                          : "text-xs font-medium"
                      }
                    >
                      {item.status}
                    </span>,
                    <span className="tabular text-xs">{item.publishAttempts}</span>,
                    <span className="text-xs">{item.lastTransport ?? "—"}</span>,
                    <span className="tabular text-xs">
                      {new Date(item.createdAt).toLocaleString()}
                    </span>,
                    <span className="font-mono text-[11px]" title={item.correlationId}>
                      {item.publicId.slice(0, 16)}…
                    </span>,
                    item.status === "failed" ? (
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-7 text-xs"
                        onClick={() => retryNotification(item.id)}
                      >
                        Retry
                      </Button>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    ),
                  ])}
                />
                {delivery.items.length === 0 ? (
                  <p className="py-6 text-center text-sm text-muted-foreground">
                    No notification handoffs yet.
                  </p>
                ) : null}
              </div>
            </div>
          )}
        </Async>
      ) : null}

      {tab === "data" ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-lg border bg-surface p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Upload className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              Import
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Every import is validated and previewed before it commits, the same way a bulk change
              is.
            </p>
            <ul className="mt-3 space-y-1.5 text-sm">
              {["Employees", "Attendance", "Leave balances", "Salary components"].map((t) => (
                <li
                  key={t}
                  className="flex items-center justify-between gap-3 border-b py-1.5 last:border-0"
                >
                  <span>{t}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 gap-1.5 px-2 text-xs"
                    onClick={() =>
                      feedback.note(
                        `${t} import template.`,
                        "Files are not generated in this build. The template carries the exact column names the import expects.",
                      )
                    }
                  >
                    <Download className="size-3.5" aria-hidden />
                    Template
                  </Button>
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-lg border bg-surface p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Download className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              Export
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Exports respect your entity and branch access, and every export is recorded.
            </p>
            <ul className="mt-3 space-y-1.5 text-sm">
              {["Full employee data", "Payroll register", "Configuration package", "Audit log"].map(
                (t) => (
                  <li
                    key={t}
                    className="flex items-center justify-between gap-3 border-b py-1.5 last:border-0"
                  >
                    <span>{t}</span>
                    <span className="text-[11px] text-muted-foreground">CSV, JSON</span>
                  </li>
                ),
              )}
            </ul>
            <p className="mt-3 text-xs text-muted-foreground">
              A configuration package can be moved between environments, so a change can be tested
              before it reaches live.
            </p>
          </div>
        </div>
      ) : null}

      {tab === "numbering" ? (
        <Async state={numbering} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Reference formats and the next number in each series"
              minWidth="28rem"
              headers={["Record", "Format", "Next"]}
              rows={rows.map((n) => [
                <span className="font-medium">{n.what}</span>,
                <span className="font-mono text-xs">{n.format}</span>,
                <span className="font-mono text-xs">{n.next}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "vendors" ? (
        <Async state={vendors} rows={4}>
          {(rows) => (
            <>
              <ConfigTable
                caption="HR vendors, what they do and what data they receive"
                minWidth="44rem"
                headers={["Vendor", "Service", "Contract to", "Data shared", "Review"]}
                rows={rows.map((v) => [
                  <span className="font-medium">{v.name}</span>,
                  <span className="text-xs">{v.service}</span>,
                  <span className="tabular text-xs">{v.contractTo}</span>,
                  <span className="text-xs text-muted-foreground">{v.dataShared}</span>,
                  <span className="text-xs">{v.review}</span>,
                ])}
              />
              <p className="mt-3 text-xs text-muted-foreground">
                "Data shared" is the actual field list, not a category. A vendor that only needs a
                name and an amount should never receive a full employee record.
              </p>
            </>
          )}
        </Async>
      ) : null}
    </ConfigPage>
  );
}
