import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { money } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

export const Route = createFileRoute("/hrm/recruitment/requisitions")({
  head: () => ({
    meta: [
      { title: "Requisitions — Kuubu HRM" },
      {
        name: "description",
        content:
          "Every request to fill a post: replacement or new position, establishment check, budget, approver and due date.",
      },
      { property: "og:title", content: "Requisitions — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Every request to fill a post: replacement or new position, establishment check, budget, approver and due date.",
      },
    ],
  }),
  component: RequisitionsRoute,
});

const entityName = (id: string) => entities.find((e) => e.id === id)?.name ?? "Unknown entity";
const open = ["draft", "submitted", "returned"];

/**
 * "/recruitment/requisitions/new" is nested under this route, so hand the screen
 * over to the child when one is matched instead of rendering the list behind it.
 */
function RequisitionsRoute() {
  const children = useChildMatches();
  return children.length ? <Outlet /> : <RequisitionsList />;
}

interface RequisitionRow {
  id: string;
  requisitionNo: string;
  jobTitle: string;
  reason: string;
  headcount: number;
  grade: string;
  orgUnitName: string;
  locationName?: string;
  hiringManagerName?: string;
  budgetAnnual?: number;
  currency: string;
  businessCase?: string;
  status: string;
  approverName?: string;
  approvedAt?: string;
  returnedReason?: string;
  raisedByName?: string;
  createdAt: string;
  vacancyCount: number;
  replacementWorkerId?: string;
}

function adaptRequisition(v: Record<string, unknown>): RequisitionRow {
  return {
    id: String(v.id ?? ""),
    requisitionNo: String(v.requisitionNo ?? ""),
    jobTitle: String(v.jobTitle ?? ""),
    reason: String(v.reason ?? ""),
    headcount: Number(v.headcount ?? 0),
    grade: String(v.grade ?? "—"),
    orgUnitName: String(v.orgUnitName ?? ""),
    locationName: v.locationName ? String(v.locationName) : undefined,
    hiringManagerName: v.hiringManagerName ? String(v.hiringManagerName) : undefined,
    budgetAnnual: v.budgetAnnual != null ? Number(v.budgetAnnual) : undefined,
    currency: String(v.currency ?? "ZMW"),
    businessCase: v.businessCase ? String(v.businessCase) : undefined,
    status: String(v.status ?? "draft"),
    approverName: v.approverName ? String(v.approverName) : undefined,
    approvedAt: v.approvedAt ? String(v.approvedAt) : undefined,
    returnedReason: v.returnedReason ? String(v.returnedReason) : undefined,
    raisedByName: v.raisedByName ? String(v.raisedByName) : undefined,
    createdAt: String(v.createdAt ?? ""),
    vacancyCount: Number(v.vacancyCount ?? 0),
    replacementWorkerId: v.replacementWorkerId ? String(v.replacementWorkerId) : undefined,
  };
}

function RequisitionsList() {
  const [view, setView] = useState("all");
  const [selected, setSelected] = useState<RequisitionRow | null>(null);
  const [decision, setDecision] = useState<{ mode: "approve" | "return"; row: RequisitionRow } | null>(null);
  const [note, setNote] = useState("");

  const state = useApi(async () => {
    const params: Record<string, unknown> = {};
    if (view === "open") params.status = "draft,submitted,returned";
    else if (view === "approved") params.status = "approved";
    const data = await realApi.requisitions(params);
    const items = (data?.items ?? []) as Record<string, unknown>[];
    const rows = items.map(adaptRequisition);
    if (view === "open") return rows.filter((r) => open.includes(r.status));
    if (view === "approved") return rows.filter((r) => r.status === "approved");
    return rows;
  }, [view]);

  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (USE_REAL) return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, [view]);
  const entityTreeOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const deptTreeOptions = entityUnits.map((e) => ({ value: e.unitName, label: treePathLabel(e.path) }));

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Requisitions"
        description="A requisition is the authority to fill a post. Each row shows why the post is needed, who owns the next decision and when it was raised. Open a requisition to approve, return for rework or review the linked vacancies."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/recruitment/requisitions/new">Raise a requisition</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <>
          <ListPage<RequisitionRow>
            rows={rows}
            savedViews={[
              { id: "all", label: "All requisitions" },
              { id: "open", label: "Awaiting action" },
              { id: "approved", label: "Approved to advertise" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search requisition number, job title or hiring manager"
            searchFields={(r) => `${r.requisitionNo} ${r.jobTitle} ${r.hiringManagerName ?? ""} ${r.orgUnitName}`}
            filters={[
              {
                id: "reason",
                label: "Reason",
                options: ["new", "replacement"],
                match: (r, v) => r.reason === v,
              },
              {
                id: "status",
                label: "Status",
                options: ["draft", "submitted", "approved", "returned", "rejected"],
                match: (r, v) => r.status === v,
              },
              {
                id: "entity",
                label: "Entity & branch",
                options: treeToSelectOptions(treeState.data ?? []).map((o) => o.value),
                treeOptions: entityTreeOptions,
                match: (r, v) =>
                  v.startsWith("entity:")
                    ? entityName(v.slice(7)) !== "Unknown entity"
                    : deptTreeOptions.some((d) => d.value === r.orgUnitName),
              },
            ]}
            bulkActions={[]}
            emptyBody="No requisitions match this view. Clear a filter, or raise a requisition to start a new hire."
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (r) => (
                  <button
                    type="button"
                    onClick={() => setSelected(r)}
                    className="font-mono text-xs text-primary underline-offset-2 hover:underline"
                  >
                    {r.requisitionNo}
                  </button>
                ),
              },
              {
                id: "job",
                header: "Job title",
                cell: (r) => (
                  <span className="block max-w-56 truncate font-medium">{r.jobTitle}</span>
                ),
              },
              {
                id: "reason",
                header: "Reason",
                cell: (r) => (
                  <span className="block max-w-56 text-xs capitalize">
                    {r.reason}
                    {r.replacementWorkerId ? (
                      <span className="block truncate text-muted-foreground">Replacement</span>
                    ) : null}
                  </span>
                ),
              },
              {
                id: "where",
                header: "Department",
                cell: (r) => (
                  <span className="block max-w-56 truncate text-xs">
                    {r.orgUnitName}
                    {r.locationName ? (
                      <span className="block text-muted-foreground">{r.locationName}</span>
                    ) : null}
                  </span>
                ),
              },
              {
                id: "headcount",
                header: "Headcount",
                cell: (r) => (
                  <span className="block text-xs">
                    <span className="tabular font-medium">{r.headcount}</span> at {r.grade}
                    <span className="block text-muted-foreground">{r.vacancyCount} vacancy linked</span>
                  </span>
                ),
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              {
                id: "raised",
                header: "Raised",
                cell: (r) => (
                  <span className="block max-w-48 truncate text-xs">
                    {r.raisedByName ?? "HR"}
                    <span className="block text-muted-foreground">
                      {r.createdAt ? new Date(r.createdAt).toLocaleDateString() : ""}
                    </span>
                  </span>
                ),
              },
              {
                id: "approver",
                header: "Approver",
                defaultVisible: false,
                cell: (r) => <span className="block max-w-56 truncate text-xs">{r.approverName ?? "—"}</span>,
              },
              {
                id: "budget",
                header: "Annual cost",
                defaultVisible: false,
                cell: (r) => (
                  <span className="tabular text-xs">
                    {r.budgetAnnual != null ? money(r.budgetAnnual, r.currency) : "—"}
                  </span>
                ),
              },
              {
                id: "manager",
                header: "Hiring manager",
                defaultVisible: false,
                cell: (r) => <span className="block max-w-56 truncate text-xs">{r.hiringManagerName ?? "—"}</span>,
              },
              {
                id: "case",
                header: "Business case",
                defaultVisible: false,
                cell: (r) => (
                  <span className="block max-w-64 truncate text-xs">{r.businessCase ?? "—"}</span>
                ),
              },
            ]}
          />
          {selected ? (
            <RequisitionDetailDialog
              row={selected}
              onClose={() => { setSelected(null); setDecision(null); setNote(""); }}
              onOpenDecision={(d) => { setDecision({ mode: d, row: selected }); setNote(""); }}
              onRefresh={() => state.reload()}
            />
          ) : null}
          {decision ? (
            <DecisionDialog
              mode={decision.mode}
              requisitionNo={decision.row.requisitionNo}
              note={note}
              onNoteChange={setNote}
              onConfirm={async () => {
                if (decision.mode === "approve") {
                  await realApi.approveRequisition(decision.row.id, { notes: note || undefined });
                } else {
                  await realApi.returnRequisition(decision.row.id, { notes: note });
                }
                setDecision(null);
                setNote("");
                state.reload();
              }}
              onClose={() => { setDecision(null); setNote(""); }}
            />
          ) : null}
          </>
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}

function RequisitionDetailDialog({
  row,
  onClose,
  onOpenDecision,
  onRefresh,
}: {
  row: RequisitionRow;
  onClose: () => void;
  onOpenDecision: (mode: "approve" | "return") => void;
  onRefresh: () => void;
}) {
  const detailState = useApi(async () => {
    const d = await realApi.requisitionDetail(row.id);
    return d as unknown as {
      requisition: Record<string, unknown>;
      events: Array<{ action: string; fromStatus?: string; toStatus?: string; notes?: string; createdAt: string }>;
      vacancies: Array<Record<string, unknown>>;
    };
  }, [row.id]);

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            Requisition {row.requisitionNo}
            <span className="ml-2"><StatusBadge status={row.status} /></span>
          </DialogTitle>
          <DialogDescription>{row.jobTitle} — {row.headcount} post(s) at {row.grade}</DialogDescription>
        </DialogHeader>
        <Async state={detailState}>
          {(detail) => (
            <div className="space-y-4 text-sm">
              <div className="grid grid-cols-2 gap-2 text-xs">
                <div><span className="text-muted-foreground">Reason</span><p className="capitalize">{row.reason}</p></div>
                <div><span className="text-muted-foreground">Department</span><p>{row.orgUnitName}</p></div>
                <div><span className="text-muted-foreground">Hiring manager</span><p>{row.hiringManagerName ?? "—"}</p></div>
                <div><span className="text-muted-foreground">Location</span><p>{row.locationName ?? "—"}</p></div>
                <div><span className="text-muted-foreground">Annual budget</span><p>{row.budgetAnnual != null ? money(row.budgetAnnual, row.currency) : "—"}</p></div>
                <div><span className="text-muted-foreground">Approver</span><p>{row.approverName ?? "—"}</p></div>
                <div><span className="text-muted-foreground">Raised by</span><p>{row.raisedByName ?? "HR"}</p></div>
                <div><span className="text-muted-foreground">Raised on</span><p>{row.createdAt ? new Date(row.createdAt).toLocaleDateString() : "—"}</p></div>
              </div>
              {row.businessCase ? (
                <div><span className="text-muted-foreground text-xs">Business case</span><p className="text-xs">{row.businessCase}</p></div>
              ) : null}
              {row.returnedReason ? (
                <div className="rounded-md border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                  <span className="font-medium">Returned for rework:</span> {row.returnedReason}
                </div>
              ) : null}
              <div>
                <span className="text-muted-foreground text-xs">Timeline</span>
                <ul className="mt-1 space-y-1">
                  {detail.events.map((e, i) => (
                    <li key={i} className="text-xs">
                      <span className="font-medium capitalize">{e.action}</span>
                      {e.fromStatus ? <> from {e.fromStatus}</> : null}
                      {e.toStatus ? <> to {e.toStatus}</> : null}
                      {e.notes ? <> — {e.notes}</> : null}
                      <span className="ml-2 text-muted-foreground">
                        {new Date(e.createdAt).toLocaleString()}
                      </span>
                    </li>
                  ))}
                  {detail.events.length === 0 ? (
                    <li className="text-muted-foreground">No events recorded yet.</li>
                  ) : null}
                </ul>
              </div>
              <div>
                <span className="text-muted-foreground text-xs">Linked vacancies ({detail.vacancies.length})</span>
                <ul className="mt-1 space-y-1">
                  {detail.vacancies.map((v) => (
                    <li key={String(v.id)} className="text-xs">
                      <Link to={`/hrm/recruitment/vacancies`} className="font-medium hover:underline">
                        {String(v.jobTitle ?? "Vacancy")}
                      </Link>
                      <span className="ml-2 text-muted-foreground">
                        {String(v.status ?? "")}
                        {v.closingDate ? ` — closing ${String(v.closingDate)}` : ""}
                      </span>
                    </li>
                  ))}
                  {detail.vacancies.length === 0 ? (
                    <li className="text-muted-foreground">No vacancies linked yet. Publish a vacancy against this requisition to start collecting candidates.</li>
                  ) : null}
                </ul>
              </div>
            </div>
          )}
        </Async>
        <DialogFooter>
          {row.status === "draft" ? (
            <Button
              onClick={async () => {
                await realApi.submitRequisition(row.id);
                onRefresh();
              }}
            >
              Submit for approval
            </Button>
          ) : null}
          {row.status === "submitted" ? (
            <>
              <Button variant="outline" onClick={() => onOpenDecision("return")}>
                Return for rework
              </Button>
              <Button onClick={() => onOpenDecision("approve")}>Approve</Button>
            </>
          ) : null}
          <Button variant="ghost" onClick={onClose}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DecisionDialog({
  mode,
  requisitionNo,
  note,
  onNoteChange,
  onConfirm,
  onClose,
}: {
  mode: "approve" | "return";
  requisitionNo: string;
  note: string;
  onNoteChange: (v: string) => void;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{mode === "approve" ? "Approve requisition" : "Return for rework"}</DialogTitle>
          <DialogDescription>
            {mode === "approve"
              ? `Approve requisition ${requisitionNo} so the vacancy can be advertised.`
              : `Send requisition ${requisitionNo} back to the requester with an explanation of what needs rework.`}
          </DialogDescription>
        </DialogHeader>
        {mode === "return" ? (
          <Textarea
            placeholder="What needs to change before this requisition can be approved?"
            value={note}
            onChange={(e) => onNoteChange(e.target.value)}
          />
        ) : (
          <Input
            placeholder="Optional approval note"
            value={note}
            onChange={(e) => onNoteChange(e.target.value)}
          />
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={mode === "return" && !note.trim()} onClick={onConfirm}>
            {mode === "approve" ? "Approve" : "Return"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
