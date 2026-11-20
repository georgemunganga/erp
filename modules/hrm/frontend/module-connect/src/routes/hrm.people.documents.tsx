import { createFileRoute } from "@tanstack/react-router";
import { useRef, useState } from "react";
import {
  Download,
  FilePlus,
  FileSignature,
  Gavel,
  Lock,
  PenLine,
  ShieldAlert,
  TriangleAlert,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { employees } from "@/mock/data";
import { documentsApi } from "@/mock/documents";
import type { Classification, EmployeeDocument } from "@/mock/documents";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/documents")({
  head: () => ({
    meta: [
      { title: "Documents — Kuubu HRM" },
      { name: "description", content: "The digital employee file: classification, versions, signatures, expiry and retention." },
      { property: "og:title", content: "Documents — Kuubu HRM" },
      { property: "og:description", content: "The digital employee file: classification, versions, signatures, expiry and retention." },
    ],
  }),
  component: DocumentsPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Known demo worker so the documents page can show the real file. */
const REAL_WORKER_ID = "019ffa91-5917-7617-96fb-8f3d11849049";
const REAL_WORKER_NAME = "Smoke M3Worker";

const name = (id: string) => employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

/** Maps backend worker-document DTOs (`{ items, ... }` under `/hrm/documents/worker/{id}`)
 *  to the UI `EmployeeDocument` shape used by the rest of the page. */
function adaptDocuments(raw: unknown): EmployeeDocument[] {
  const items = (Array.isArray(raw)
    ? raw
    : (raw as { items?: unknown[] })?.items ?? []) as Array<{
    id?: string;
    workerId?: string;
    category?: string;
    title?: string;
    fileName?: string;
    contentType?: string;
    sizeBytes?: number;
    classification?: string;
    expiryDate?: string | null;
  }>;
  return items.map((d, i) => ({
    id: d.id ?? `doc-${i}`,
    employeeId: REAL_WORKER_ID, // overwritten by the page with the selected worker
    name: d.title || d.fileName || "Untitled document",
    category: d.category ?? "General",
    classification: ((d.classification === "restricted"
      ? "Restricted"
      : d.classification === "confidential"
        ? "Confidential"
        : "General") as Classification),
    visibleTo: "HR operations and the employee",
    version: 1,
    issued: "",
    expires: d.expiryDate && d.expiryDate !== "0001-01-01T00:00:00+00:00" ? d.expiryDate.slice(0, 10) : undefined,
    signature: "Not required",
    retention: "Retained per schedule",
    sizeKb: Math.round((d.sizeBytes ?? 0) / 1024),
  }));
}

const TODAY = new Date("2026-07-29");
const daysUntil = (iso: string) =>
  Math.round((new Date(iso).getTime() - TODAY.getTime()) / 86_400_000);

/** Classification is never colour-only — it always carries an icon and a word. */
function ClassificationTag({ value }: { value: Classification }) {
  const map = {
    General: { icon: FileSignature, cls: "border-border bg-muted text-muted-foreground" },
    Confidential: { icon: Lock, cls: "border-info/30 bg-info-soft text-info" },
    Restricted: { icon: ShieldAlert, cls: "border-danger/30 bg-danger-soft text-danger" },
  } as const;
  const { icon: Icon, cls } = map[value];
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium ${cls}`}>
      <Icon className="size-3.5 shrink-0" aria-hidden />
      {value}
    </span>
  );
}

function SignatureCell({ d }: { d: EmployeeDocument }) {
  if (!d.signature || d.signature === "Not required") {
    return <span className="text-xs text-muted-foreground">Not required</span>;
  }
  if (d.signature === "Signed") {
    return <span className="text-xs">Signed {d.signedOn}</span>;
  }
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-warning">
      <PenLine className="size-3.5 shrink-0" aria-hidden />
      Awaiting signature
    </span>
  );
}

function ExpiryCell({ d }: { d: EmployeeDocument }) {
  if (!d.expires) return <span className="text-xs text-muted-foreground">No expiry</span>;
  const days = daysUntil(d.expires);
  if (days < 0) {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-danger">
        <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
        Expired {d.expires}
      </span>
    );
  }
  if (days <= 60) {
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-warning">
        <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
        {d.expires} · {days} days
      </span>
    );
  }
  return <span className="text-xs">{d.expires}</span>;
}

function DocumentsPage() {
  const workers = useApi(() => (USE_REAL ? realApi.employees({ pageSize: 200 }) : Promise.resolve(null)), []);
  const employeeOptions: Array<{ id: string; fullName: string }> = USE_REAL
    ? Array.from((workers.data as { items?: Array<{ id: string; fullName: string }> } | null)?.items ?? []).map((w) => ({
        id: String(w.id ?? ""),
        fullName: String(w.fullName ?? "Unknown"),
      }))
    : [];
  const defaultWorkerId = employeeOptions.length > 0 ? employeeOptions[0].id : REAL_WORKER_ID;
  const [selectedWorkerId, setSelectedWorkerId] = useState<string | null>(null);
  const workerId = (selectedWorkerId ?? defaultWorkerId) as string;
  const workerName = employeeOptions.find((w) => w.id === workerId)?.fullName ?? REAL_WORKER_NAME;
  const mockDocs = useMock(() => documentsApi.all());
  const realDocs = useApi(
    () =>
      realApi
        .workerDocuments(workerId)
        .then((raw) =>
          adaptDocuments(raw).map((d) => ({ ...d, employeeId: workerId, employeeName: workerName })),
        ),
    [workerId, workerName],
  );
  const docs = USE_REAL ? realDocs : mockDocs;
  const templates = useMock(() => documentsApi.templates());
  const [view, setView] = useState("all");
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement | null>(null);

  /** Categories the backend accepts for worker documents (see document schema). */
  const docCategories = [
    { value: "contract", label: "Contract" },
    { value: "id", label: "Identity document" },
    { value: "qualification", label: "Qualification" },
    { value: "medical", label: "Medical" },
    { value: "certificate", label: "Certificate" },
    { value: "letter", label: "Letter" },
    { value: "evidence", label: "Evidence" },
  ];

  /** Guess the category from the file name, falling back to the first valid one. */
  const guessCategory = (fileName: string): string => {
    const n = fileName.toLowerCase();
    if (n.includes("contract")) return "contract";
    if (n.includes("nrc") || n.includes("id") || n.includes("passport")) return "id";
    if (n.includes("degree") || n.includes("cert")) return "qualification";
    if (n.includes("medical") || n.includes("health")) return "medical";
    if (n.includes("letter")) return "letter";
    if (n.includes("evidence")) return "evidence";
    return docCategories[0].value;
  };

  /** Upload a file to the real backend for the selected worker, then reload. */
  const onUpload = async (file: File) => {
    if (!USE_REAL || !file) return;
    const category = guessCategory(file.name);
    setUploading(true);
    try {
      await realApi.uploadDocument(workerId, file, category, file.name);
      docs.reload();
      feedback.saved(`${file.name} added to ${workerName}'s file as ${category}.`);
      feedback.note("It is retained under the document schedule — nothing here is silently deleted.");
    } catch (err) {
      feedback.blocked("The file could not be uploaded.", String(err));
    } finally {
      setUploading(false);
      if (fileInput.current) fileInput.current.value = "";
    }
  };

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="People"
        title="Documents and employee files"
        description="Who may open a document is decided by its classification, not by who it is about. Nothing here is deleted on request — it is retained, held or lawfully disposed of on a schedule."
        primaryAction={
          USE_REAL ? (
            <>
              <input
                ref={fileInput}
                type="file"
                className="hidden"
                aria-label="Choose a document to upload"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) void onUpload(f);
                }}
              />
              <Button
                variant="outline"
                onClick={() => fileInput.current?.click()}
                disabled={uploading}
              >
                <FilePlus className="size-4" aria-hidden />
                {uploading ? "Uploading…" : "Upload document"}
              </Button>
              <Button
                onClick={() =>
                  feedback.note(
                    "Choose a template below to generate a document.",
                    "Templates are managed in Configuration, under Process design.",
                  )
                }
              >
                Generate from template
              </Button>
            </>
          ) : (
            <Button
              onClick={() =>
                feedback.note(
                  "Choose a template below to generate a document.",
                  "Templates are managed in Configuration, under Process design.",
                )
              }
            >
              Generate from template
            </Button>
          )
        }
      />

      {/* HR-filed documents belong to one employee at a time — pick the file owner first. */}
      {USE_REAL && (
        <div className="mt-4 flex items-center gap-3">
          <label htmlFor="documents-worker-select" className="text-sm font-medium">
            Employee file
          </label>
          <Select
            value={workerId}
            onValueChange={(next) => {
              setSelectedWorkerId(next);
              docs.reload();
            }}
          >
            <SelectTrigger id="documents-worker-select" className="w-64">
              <SelectValue placeholder="Select an employee" />
            </SelectTrigger>
            <SelectContent className="max-h-72 overflow-auto">
              {employeeOptions.length > 0 ? (
                employeeOptions.map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.fullName}
                  </SelectItem>
                ))
              ) : (
                <SelectItem value={REAL_WORKER_ID}>{REAL_WORKER_NAME} (demo)</SelectItem>
              )}
            </SelectContent>
          </Select>
          {employeeOptions.length === 0 && (
            <p className="text-xs text-muted-foreground">
              No workers on the roster yet — import or add employees first, then their files appear here.
            </p>
          )}
        </div>
      )}

      <Async state={docs} rows={5}>
        {(rows) => {
          const expiring = rows.filter((d) => d.expires && daysUntil(d.expires) <= 60);
          const unsigned = rows.filter((d) => d.signature === "Awaiting signature");
          const held = rows.filter((d) => d.legalHold);

          return (
            <>
              {expiring.length || unsigned.length || held.length ? (
                <section aria-label="Needs attention" className="grid gap-3 sm:grid-cols-3">
                  <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                    <p className="flex items-center gap-1.5 text-xs font-medium text-warning">
                      <TriangleAlert className="size-3.5" aria-hidden />
                      Expiring or expired
                    </p>
                    <p className="mt-1 text-2xl font-semibold">{expiring.length}</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      A lapsed licence can stop someone working — it feeds the fitness-to-work check.
                    </p>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <p className="flex items-center gap-1.5 text-xs font-medium">
                      <PenLine className="size-3.5" aria-hidden />
                      Awaiting signature
                    </p>
                    <p className="mt-1 text-2xl font-semibold">{unsigned.length}</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Unsigned documents are not yet in force.
                    </p>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <p className="flex items-center gap-1.5 text-xs font-medium">
                      <Gavel className="size-3.5" aria-hidden />
                      Under legal hold
                    </p>
                    <p className="mt-1 text-2xl font-semibold">{held.length}</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Retained past schedule and not deletable until the hold lifts.
                    </p>
                  </div>
                </section>
              ) : null}

              <ListPage<EmployeeDocument>
                rows={rows.filter((d) =>
                  view === "expiring"
                    ? Boolean(d.expires && daysUntil(d.expires) <= 60)
                    : view === "unsigned"
                      ? d.signature === "Awaiting signature"
                      : view === "restricted"
                        ? d.classification === "Restricted"
                        : true,
                )}
                savedViews={[
                  { id: "all", label: "All documents" },
                  { id: "expiring", label: "Expiring or expired" },
                  { id: "unsigned", label: "Awaiting signature" },
                  { id: "restricted", label: "Restricted only" },
                ]}
                activeView={view}
                onViewChange={setView}
                searchPlaceholder="Search document, employee or category"
                searchFields={(d) => `${d.id} ${d.name} ${d.category} ${USE_REAL ? (d.employeeName ?? workerName) : name(d.employeeId)}`}
                filters={[
                  {
                    id: "classification",
                    label: "Classification",
                    options: ["General", "Confidential", "Restricted"],
                    match: (d, v) => d.classification === v,
                  },
                  {
                    id: "category",
                    label: "Category",
                    options: ["Contract", "Employment change", "Policy acknowledgement", "Qualification", "Licence", "Occupational health"],
                    match: (d, v) => d.category === v,
                  },
                ]}
                bulkActions={[{ label: "Export selection (audited)", onSelect: () => undefined }]}
                columns={[
                                    { id: "name",
                    header: "Document",
                    cell: (d) => (
                      <span className="block min-w-0 max-w-72">
                        <span className="block truncate text-sm font-medium">{d.name}</span>
                        <span className="block truncate text-xs text-muted-foreground">
                          {d.id} · v{d.version}
                          {d.supersedes ? " · supersedes an earlier version" : ""}
                        </span>
                      </span>
                    ),
                  },
                  { id: "employee", header: "Employee", cell: (d) => <span className="block max-w-48 truncate">{USE_REAL ? (d.employeeName ?? workerName) : name(d.employeeId)}</span> },
                  { id: "category", header: "Category", cell: (d) => d.category },
                  { id: "classification", header: "Classification", cell: (d) => <ClassificationTag value={d.classification} /> },
                  { id: "signature", header: "Signature", cell: (d) => <SignatureCell d={d} /> },
                  { id: "expires", header: "Expires", cell: (d) => <ExpiryCell d={d} /> },
                  {
                    id: "open",
                    header: "",
                    cell: (d) =>
                      d.classification === "Restricted" ? (
                        <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                          <Lock className="size-3.5 shrink-0" aria-hidden />
                          Restricted
                        </span>
                      ) : (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 gap-1.5 px-2 text-xs"
                          onClick={async () => {
                            if (USE_REAL) {
                              try {
                                const { url, fileName } = await realApi.downloadDocument(d.id, d.name);
                                const a = document.createElement("a");
                                a.href = url;
                                a.download = fileName;
                                a.click();
                                return;
                              } catch (err) {
                                feedback.blocked("Could not open the document.", String(err));
                                return;
                              }
                            }
                            feedback.note("Document preview is not available in this build.");
                          }}
                        >
                          <Download className="size-3.5" aria-hidden />
                          Open
                        </Button>
                      ),
                  },
                  { id: "visibleTo", header: "Who can see it", defaultVisible: false, cell: (d) => <span className="block max-w-72 text-xs">{d.visibleTo}</span> },
                  { id: "retention", header: "Retention", defaultVisible: false, cell: (d) => <span className="text-xs">{d.retention}</span> },
                  {
                    id: "lastAccess",
                    header: "Last opened",
                    defaultVisible: false,
                    cell: (d) =>
                      d.lastAccess ? (
                        <span className="block max-w-56 text-xs">
                          {d.lastAccess.by} · {d.lastAccess.at}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">Not opened</span>
                      ),
                  },
                ]}
                emptyBody="No documents match the current view."
              />

              {held.length ? (
                <section aria-label="Legal holds" className="rounded-lg border border-warning/40 bg-warning-soft p-5">
                  <h2 className="flex items-center gap-2 text-sm font-semibold text-warning">
                    <Gavel className="size-4" aria-hidden />
                    Documents under legal hold
                  </h2>
                  <ul className="mt-3 space-y-2 text-sm">
                    {held.map((d) => (
                      <li key={d.id}>
                        <span className="font-medium">{d.name}</span>{" "}
                        <span className="text-muted-foreground">— {name(d.employeeId)}</span>
                        <p className="mt-0.5 text-xs text-foreground">{d.legalHold}</p>
                      </li>
                    ))}
                  </ul>
                </section>
              ) : null}
            </>
          );
        }}
      </Async>

      <section aria-label="Templates" className="rounded-lg border bg-surface p-5">
        <h2 className="text-sm font-semibold">Templates</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          Documents are generated from a versioned template with merge fields — not written by hand
          each time, so wording stays consistent and auditable.
        </p>
        <Async state={templates} rows={2}>
          {(rows) => (
            <ul className="mt-3 divide-y">
              {rows.map((t) => (
                <li key={t.id} className="flex flex-wrap items-center gap-3 py-3">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{t.name}</span>
                    <span className="block truncate text-xs text-muted-foreground">
                      {t.category} · {t.mergeFields.length} merge fields · updated {t.lastUpdated}
                    </span>
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {t.requiresSignature ? "Signature required" : "No signature"}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      feedback.note(
                        "Template selected.",
                        "Fields are filled from the employee record, so nothing is retyped.",
                      )
                    }
                  >
                    Use
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>
    </AppShell>
      </AuthGate>
  );
}
