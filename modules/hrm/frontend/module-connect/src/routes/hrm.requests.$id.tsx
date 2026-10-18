import { FormToggle } from "@/components/ui/form-toggle";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Lock, Loader2, SendHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { realApi, useApi } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { useRoleGate } from "@/platform/app-context";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/requests/$id")({
  head: () => ({
    meta: [
      { title: "HR request — Newworldcargo HRM" },
      { name: "description", content: "One thread: conversation, evidence and status together." },
      { property: "og:title", content: "HR request — Newworldcargo HRM" },
      {
        property: "og:description",
        content: "One thread: conversation, evidence and status together.",
      },
    ],
  }),
  component: RequestDetail,
});

const statusLabel: Record<string, string> = {
  open: "Open",
  "in-progress": "In progress",
  "awaiting-employee": "Awaiting employee",
  resolved: "Resolved",
  closed: "Closed",
};

const categoryLabel: Record<string, string> = {
  payroll: "Payroll",
  benefits: "Benefits",
  contract: "Contract",
  "data-change": "Data change",
  "employment-letter": "Employment letter",
  other: "Other",
};

interface ThreadMessage {
  id: string;
  from: string;
  body: string;
  isInternalNote: boolean;
  at: string;
}

function toMessages(raw: unknown): ThreadMessage[] {
  const msgs = Array.isArray(raw) ? raw : [];
  return msgs.map((m, i) => {
    const x = m as Record<string, unknown>;
    return {
      id: String(x.id ?? `msg-${i}`),
      from: String(x.from ?? "system"),
      body: String(x.body ?? ""),
      isInternalNote: Boolean(x.isInternalNote),
      at: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 16).replace("T", " ") : "",
    };
  });
}

function RequestDetail() {
  const { id } = Route.useParams();
  const canAct = useRoleGate()(["hr_ops", "hr_admin"]);
  const state = useApi<Record<string, unknown> | null>(async () => {
    if (!canAct) return realApi.myRequest(id);
    const page = await realApi.experienceRequests();
    const found = (Array.isArray(page.items) ? page.items : []).find(
      (r) => String((r as Record<string, unknown>).id) === id,
    );
    return (found ?? null) as Record<string, unknown> | null;
  }, [id, canAct]);
  const [reply, setReply] = useState("");
  const [noteMode, setNoteMode] = useState(false);
  const [busy, setBusy] = useState(false);

  return (
    <AuthGate>
      <AppShell>
        <Async state={state} rows={3}>
          {(raw) => {
            if (!raw) return <RestrictedState />;
            const requestId = String(raw.id ?? "");
            const workerName = String(raw.workerName ?? "Unknown");
            const category =
              categoryLabel[String(raw.category ?? "")] ?? String(raw.category ?? "");
            const status = statusLabel[String(raw.status ?? "")] ?? String(raw.status ?? "");
            const confidential = String(raw.confidentiality ?? "normal") === "confidential";
            const messages = toMessages(raw.messages);
            const isClosed = ["Resolved", "Closed"].includes(status);

            const send = async (internal: boolean) => {
              if (!reply.trim()) return;
              setBusy(true);
              try {
                if (canAct)
                  await realApi.addRequestMessage(requestId, {
                    body: reply.trim(),
                    isInternalNote: internal,
                  });
                else
                  await realApi.addMyRequestMessage(requestId, {
                    body: reply.trim(),
                    isInternalNote: false,
                  });
                feedback.saved(
                  internal ? "Internal note posted" : "Reply posted to the case thread",
                );
                setReply("");
                state.reload();
              } catch (e) {
                feedback.blocked(
                  "Failed to post the message",
                  e instanceof Error ? e.message : "Please try again.",
                );
              } finally {
                setBusy(false);
              }
            };

            const resolve = async () => {
              setBusy(true);
              try {
                await realApi.resolveRequest(requestId);
                feedback.saved("Request resolved", () => state.reload());
                state.reload();
              } catch (e) {
                feedback.blocked(
                  "Failed to resolve the request",
                  e instanceof Error ? e.message : "Please try again.",
                );
              } finally {
                setBusy(false);
              }
            };

            return (
              <RecordDetail
                reference={requestId}
                title={String(raw.subject ?? "HR request")}
                subtitle={`${category} · raised by ${workerName}`}
                status={status}
                owner={workerName}
                nextAction={
                  isClosed
                    ? "Case closed"
                    : String(raw.status ?? "") === "awaiting-employee"
                      ? "Waiting for the employee to reply"
                      : "Awaiting HR action"
                }
                summary={[
                  { label: "Subject", value: String(raw.subject ?? "") },
                  { label: "Category", value: category },
                  { label: "Employee", value: workerName },
                  { label: "Confidentiality", value: confidential ? "Confidential" : "Normal" },
                  {
                    label: "Raised",
                    value:
                      typeof raw.createdAt === "string"
                        ? String(raw.createdAt).slice(0, 16).replace("T", " ")
                        : "—",
                  },
                ]}
                primaryAction={
                  canAct && !isClosed ? (
                    <Button variant="secondary" onClick={resolve} disabled={busy}>
                      {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                      {String(raw.status ?? "") === "resolved"
                        ? "Close request"
                        : "Resolve request"}
                    </Button>
                  ) : null
                }
              >
                <section aria-label="Case thread" className="rounded-lg border bg-surface p-5">
                  <h2 className="text-sm font-semibold">Thread</h2>
                  <ul className="mt-4 space-y-3">
                    <li className="rounded-md border bg-surface-muted p-3 text-sm">
                      <p className="text-xs uppercase tracking-wide text-muted-foreground">
                        Original request · employee
                      </p>
                      <p className="mt-1 whitespace-pre-wrap">
                        {String(raw.body ?? "") || String(raw.subject ?? "")}
                      </p>
                    </li>
                    {messages.map((m) => (
                      <li
                        key={m.id}
                        className={`rounded-md border p-3 text-sm ${
                          m.isInternalNote
                            ? "border-warning/40 bg-warning-soft"
                            : "border-border bg-surface-muted"
                        }`}
                      >
                        <p className="flex flex-wrap items-center gap-2 text-xs uppercase tracking-wide text-muted-foreground">
                          {m.from === "hr" ? (
                            <span className={m.isInternalNote ? "text-warning" : "text-foreground"}>
                              HR {m.isInternalNote ? "· internal note" : ""}
                            </span>
                          ) : (
                            <span>Employee</span>
                          )}
                          {m.isInternalNote ? <Lock className="size-3" aria-hidden /> : null}
                          <span className="ml-auto">{m.at}</span>
                        </p>
                        <p className="mt-1 whitespace-pre-wrap">{m.body}</p>
                      </li>
                    ))}
                    {messages.length === 0 ? (
                      <li className="rounded-md border border-dashed p-3 text-xs text-muted-foreground">
                        No replies yet — the thread starts here.
                      </li>
                    ) : null}
                  </ul>

                  {!isClosed ? (
                    <div className="mt-5 space-y-3 border-t pt-4">
                      {canAct ? (
                        <label className="flex items-center gap-2 text-sm">
                          <FormToggle
                            type="checkbox"
                            checked={noteMode}
                            onChange={(e) => setNoteMode(e.target.checked)}
                            className="h-4 w-4 accent-primary"
                          />
                          Internal note — only HR can see this
                        </label>
                      ) : null}
                      <Textarea
                        value={reply}
                        onChange={(e) => setReply(e.target.value)}
                        rows={3}
                        placeholder={
                          noteMode
                            ? "Write an internal note for HR colleagues"
                            : "Reply in the case thread"
                        }
                        disabled={busy}
                      />
                      <div className="flex justify-end">
                        <Button onClick={() => send(noteMode)} disabled={busy || !reply.trim()}>
                          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                          Post {noteMode ? "note" : "reply"}
                          <SendHorizontal className="size-4" aria-hidden />
                        </Button>
                      </div>
                    </div>
                  ) : null}
                </section>
              </RecordDetail>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
