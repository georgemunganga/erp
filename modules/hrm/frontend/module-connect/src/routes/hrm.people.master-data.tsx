import { FormSelect } from "@/components/ui/form-select";
import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { ArchiveRestore, FileSpreadsheet, RotateCcw, ShieldCheck, UsersRound } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { type ApiState, realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/people/master-data")({ component: MasterDataPage });
type Row = Record<string, unknown>;
type Tab = "import" | "bulk" | "history" | "reactivate" | "quality";

const today = new Date().toISOString().slice(0, 10);
const importHeaders = [
  "employeeNo",
  "firstName",
  "lastName",
  "middleName",
  "email",
  "phone",
  "nrc",
  "tpin",
  "napsaNumber",
  "nhimaNumber",
  "workerType",
  "orgUnitCode",
  "locationCode",
  "grade",
  "jobTitle",
  "startDate",
];

function parseCsv(text: string): Row[] {
  const records: string[][] = [];
  let record: string[] = [];
  let value = "";
  let quoted = false;
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (character === '"') {
      if (quoted && text[index + 1] === '"') {
        value += '"';
        index += 1;
      } else quoted = !quoted;
    } else if (character === "," && !quoted) {
      record.push(value.trim());
      value = "";
    } else if ((character === "\n" || character === "\r") && !quoted) {
      if (character === "\r" && text[index + 1] === "\n") index += 1;
      record.push(value.trim());
      if (record.some(Boolean)) records.push(record);
      record = [];
      value = "";
    } else value += character;
  }
  record.push(value.trim());
  if (record.some(Boolean)) records.push(record);
  if (records.length < 2)
    throw new Error("The CSV must contain a header and at least one worker row.");
  const normalize = (header: string) => header.replaceAll(/[^a-z0-9]/gi, "").toLowerCase();
  const aliases = new Map(importHeaders.map((header) => [normalize(header), header]));
  aliases.set("employeenumber", "employeeNo");
  aliases.set("napsa", "napsaNumber");
  aliases.set("nhima", "nhimaNumber");
  aliases.set("departmentcode", "orgUnitCode");
  const headers = records[0].map((header) => aliases.get(normalize(header)) ?? header);
  if (!headers.includes("firstName") || !headers.includes("lastName"))
    throw new Error("The CSV requires firstName and lastName columns.");
  return records
    .slice(1)
    .map((cells) =>
      Object.fromEntries(headers.map((header, index) => [header, cells[index] || null])),
    );
}

function downloadTemplate() {
  const body = `${importHeaders.join(",")}\n,Chanda,Banda,,chanda@example.com,+260970000000,123456/78/1,1000000000,NAPSA-001,NHIMA-001,employee,OPS,LHQ,G4,Analyst,2026-08-01\n`;
  const url = URL.createObjectURL(new Blob([body], { type: "text/csv" }));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "worker-import-template.csv";
  anchor.click();
  URL.revokeObjectURL(url);
}

function BatchPreview({ batch, onApply }: { batch: Row; onApply: () => Promise<void> }) {
  const errors = (batch.errors ?? []) as Row[];
  const samples = (batch.samples ?? []) as Row[];
  const [busy, setBusy] = useState(false);
  return (
    <Card data-testid="master-data-preview">
      <CardHeader>
        <CardTitle>Validation preview</CardTitle>
        <CardDescription>
          Nothing has changed yet. Apply is enabled only when every row is valid.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-4">
          {[
            ["Rows", batch.rowCount],
            ["Ready", batch.readyCount],
            ["Unchanged", batch.unchangedCount],
            ["Errors", batch.errorCount],
          ].map(([label, value]) => (
            <div key={String(label)} className="rounded-md border bg-surface-muted p-3">
              <p className="text-xs text-muted-foreground">{String(label)}</p>
              <p className="mt-1 text-xl font-semibold tabular-nums">{String(value ?? 0)}</p>
            </div>
          ))}
        </div>
        {errors.length ? (
          <ul className="space-y-2" aria-label="Validation errors">
            {errors.map((error, index) => (
              <li
                key={`${String(error.row)}-${String(error.field)}-${index}`}
                className="rounded-md border border-danger/40 bg-danger-soft p-2 text-sm text-danger"
              >
                Row {String(error.row)} · {String(error.employeeNo ?? "new worker")} ·{" "}
                {String(error.field)}: {String(error.message)}
              </li>
            ))}
          </ul>
        ) : null}
        {samples.length ? (
          <div className="overflow-x-auto rounded-md border">
            <RecordTable className="w-full min-w-[38rem] text-left text-sm">
              <thead className="border-b bg-surface-muted text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2">Employee</th>
                  <th className="px-3 py-2">Action</th>
                  <th className="px-3 py-2">Before</th>
                  <th className="px-3 py-2">After</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {samples.map((sample, index) => (
                  <tr key={`${String(sample.employeeNo)}-${index}`}>
                    <td className="px-3 py-2 font-mono text-xs">{String(sample.employeeNo)}</td>
                    <td className="px-3 py-2">
                      <StatusBadge status={String(sample.action)} />
                    </td>
                    <td className="px-3 py-2 text-muted-foreground">{String(sample.before)}</td>
                    <td className="px-3 py-2">{String(sample.after)}</td>
                  </tr>
                ))}
              </tbody>
            </RecordTable>
          </div>
        ) : null}
        <Button
          disabled={
            String(batch.status) !== "previewed" ||
            Number(batch.errorCount) > 0 ||
            Number(batch.readyCount) === 0 ||
            busy
          }
          onClick={async () => {
            setBusy(true);
            try {
              await onApply();
            } finally {
              setBusy(false);
            }
          }}
        >
          <ShieldCheck className="size-4" aria-hidden />{" "}
          {busy
            ? "Applying…"
            : String(batch.status) === "previewed"
              ? "Apply validated batch"
              : `Batch ${String(batch.status)}`}
        </Button>
      </CardContent>
    </Card>
  );
}

function MasterDataPage() {
  const [tab, setTab] = useState<Tab>("import");
  const [preview, setPreview] = useState<Row | null>(null);
  const history = useApi(async () => (await realApi.masterDataBatches()).items, []);
  const archived = useApi(async () => {
    const result = await realApi.employees({ includeArchived: true });
    return (result.items as Row[]).filter((worker) => Boolean(worker.isArchived));
  }, []);
  const checks = useApi(async () => (await realApi.dqChecks()) as Row[], []);

  const applied = async () => {
    if (!preview) return;
    const result = await realApi.applyMasterDataBatch(String(preview.id));
    setPreview(result);
    toast.success("Master-data batch applied", {
      description: "Previous values are recoverable for 30 days.",
    });
    history.reload();
    archived.reload();
    checks.reload();
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="People administration"
          title="Master data operations"
          description="Preview, validate, apply and recover worker imports and bulk changes without bypassing employee history."
        />
        <div data-testid="master-data-operations" className="space-y-6">
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Master data operations">
            {(
              [
                ["import", "Worker import"],
                ["bulk", "Bulk update"],
                ["history", "History and recovery"],
                ["reactivate", "Reactivation"],
                ["quality", "Quality dashboard"],
              ] as const
            ).map(([id, label]) => (
              <Button
                key={id}
                role="tab"
                aria-selected={tab === id}
                variant={tab === id ? "default" : "outline"}
                size="sm"
                onClick={() => {
                  setTab(id);
                  setPreview(null);
                }}
              >
                {label}
              </Button>
            ))}
          </div>
          {tab === "import" ? <ImportPanel onPreview={setPreview} /> : null}
          {tab === "bulk" ? <BulkPanel onPreview={setPreview} /> : null}
          {preview && (tab === "import" || tab === "bulk") ? (
            <BatchPreview batch={preview} onApply={applied} />
          ) : null}
          {tab === "history" ? (
            <HistoryPanel
              state={history}
              onChanged={() => {
                history.reload();
                archived.reload();
                checks.reload();
              }}
            />
          ) : null}
          {tab === "reactivate" ? (
            <ReactivationPanel
              state={archived}
              onChanged={() => {
                archived.reload();
                history.reload();
              }}
            />
          ) : null}
          {tab === "quality" ? <QualityPanel state={checks} /> : null}
        </div>
      </AppShell>
    </AuthGate>
  );
}

function ImportPanel({ onPreview }: { onPreview: (batch: Row) => void }) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const preview = async () => {
    if (!file) return;
    setBusy(true);
    try {
      const rows = parseCsv(await file.text());
      onPreview(await realApi.previewWorkerImport(file.name, rows));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Import preview failed");
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card>
      <CardHeader>
        <CardTitle>Import workers from CSV</CardTitle>
        <CardDescription>
          Up to 1,000 rows. Existing employee numbers are updated; new rows are created. Blank
          employee numbers receive the next number.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="rounded-md border bg-surface-muted p-3 text-xs text-muted-foreground">
          Required columns: <code>firstName</code>, <code>lastName</code>. Organisation and location
          values use their configured codes. Duplicate NRC or email values are blocked.
        </div>
        <div>
          <Label htmlFor="worker-import-file">Worker CSV</Label>
          <Input
            id="worker-import-file"
            type="file"
            accept=".csv,text/csv"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={preview} disabled={!file || busy}>
            <FileSpreadsheet className="size-4" aria-hidden />{" "}
            {busy ? "Validating…" : "Preview import"}
          </Button>
          <Button variant="outline" onClick={downloadTemplate}>
            Download template
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

const bulkFields = [
  ["orgUnitCode", "Organisation unit code"],
  ["locationCode", "Location code"],
  ["managerEmployeeNo", "Manager employee number"],
  ["grade", "Grade"],
  ["jobTitle", "Job title"],
  ["status", "Status"],
  ["email", "Email"],
  ["phone", "Phone"],
  ["nrc", "NRC"],
  ["tpin", "TPIN"],
  ["napsaNumber", "NAPSA number"],
  ["nhimaNumber", "NHIMA number"],
] as const;

function BulkPanel({ onPreview }: { onPreview: (batch: Row) => void }) {
  const [employeeNos, setEmployeeNos] = useState("");
  const [field, setField] = useState<(typeof bulkFields)[number][0]>("orgUnitCode");
  const [value, setValue] = useState("");
  const [effectiveDate, setEffectiveDate] = useState(today);
  const [busy, setBusy] = useState(false);
  const rows = useMemo(
    () =>
      employeeNos
        .split(/[\s,;]+/)
        .map((item) => item.trim())
        .filter(Boolean),
    [employeeNos],
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle>Bulk worker update</CardTitle>
        <CardDescription>
          Apply one controlled field change to a selected set of employee numbers. Future dates are
          supported for organisation fields.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <Label htmlFor="bulk-employees">Employee numbers</Label>
          <Textarea
            id="bulk-employees"
            value={employeeNos}
            onChange={(event) => setEmployeeNos(event.target.value)}
            placeholder="EMP-0001&#10;EMP-0002"
          />
        </div>
        <div>
          <Label htmlFor="bulk-field">Field</Label>
          <FormSelect
            id="bulk-field"
            className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
            value={field}
            onChange={(event) => setField(event.target.value as typeof field)}
          >
            {bulkFields.map(([id, label]) => (
              <option key={id} value={id}>
                {label}
              </option>
            ))}
          </FormSelect>
        </div>
        <div>
          <Label htmlFor="bulk-value">New value</Label>
          <Input id="bulk-value" value={value} onChange={(event) => setValue(event.target.value)} />
        </div>
        <div>
          <Label htmlFor="bulk-effective">Effective date</Label>
          <Input
            id="bulk-effective"
            type="date"
            min={today}
            value={effectiveDate}
            onChange={(event) => setEffectiveDate(event.target.value)}
          />
        </div>
        <div className="flex items-end">
          <Button
            disabled={!rows.length || !value || busy}
            onClick={async () => {
              setBusy(true);
              try {
                onPreview(
                  await realApi.previewWorkerBulk(
                    effectiveDate,
                    rows.map((employeeNo) => ({ employeeNo, [field]: value })),
                  ),
                );
              } catch (error) {
                toast.error(error instanceof Error ? error.message : "Bulk preview failed");
              } finally {
                setBusy(false);
              }
            }}
          >
            <UsersRound className="size-4" aria-hidden />{" "}
            {busy ? "Validating…" : `Preview ${rows.length} workers`}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function HistoryPanel({ state, onChanged }: { state: ApiState<Row[]>; onChanged: () => void }) {
  return (
    <Async state={state}>
      {(rows) => (
        <ul className="space-y-3" data-testid="master-data-history">
          {rows.map((batch) => (
            <li key={String(batch.id)} className="rounded-md border bg-surface p-4">
              <div className="flex flex-wrap items-center gap-2">
                <strong>{String(batch.batchType)}</strong>
                <StatusBadge status={String(batch.status)} />
                <span className="text-xs text-muted-foreground">
                  {String(batch.rowCount)} rows · {String(batch.effectiveDate)}
                </span>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                Requested by {String(batch.requestedBySubjectId)} ·{" "}
                {String(batch.createdAt).slice(0, 16).replace("T", " ")}
              </p>
              {batch.canRollback ? (
                <Button
                  className="mt-3"
                  size="sm"
                  variant="outline"
                  onClick={async () => {
                    await realApi.rollbackMasterDataBatch(String(batch.id));
                    toast.success("Batch rolled back");
                    onChanged();
                  }}
                >
                  <RotateCcw className="size-4" aria-hidden /> Roll back batch
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </Async>
  );
}

function ReactivationPanel({
  state,
  onChanged,
}: {
  state: ApiState<Row[]>;
  onChanged: () => void;
}) {
  const [workerId, setWorkerId] = useState("");
  const [reason, setReason] = useState("");
  return (
    <Card>
      <CardHeader>
        <CardTitle>Reactivate an archived worker</CardTitle>
        <CardDescription>
          Reactivation is audited and recoverable. It clears the old end date but retains all
          historical records.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <Async state={state}>
          {(rows) => (
            <div>
              <Label htmlFor="reactivate-worker">Archived worker</Label>
              <FormSelect
                id="reactivate-worker"
                className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                value={workerId}
                onChange={(event) => setWorkerId(event.target.value)}
              >
                <option value="">Select a worker</option>
                {rows.map((worker) => (
                  <option key={String(worker.id)} value={String(worker.id)}>
                    {String(worker.employeeNo)} — {String(worker.fullName)}
                  </option>
                ))}
              </FormSelect>
            </div>
          )}
        </Async>
        <div>
          <Label htmlFor="reactivation-reason">Reason</Label>
          <Textarea
            id="reactivation-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        </div>
        <Button
          disabled={!workerId || !reason.trim()}
          onClick={async () => {
            await realApi.reactivateWorker(workerId, reason.trim());
            toast.success("Worker reactivated");
            setWorkerId("");
            setReason("");
            onChanged();
          }}
        >
          <ArchiveRestore className="size-4" aria-hidden /> Reactivate worker
        </Button>
      </CardContent>
    </Card>
  );
}

function QualityPanel({ state }: { state: ApiState<Row[]> }) {
  return (
    <Async state={state}>
      {(rows) => {
        const grouped = rows.reduce<Record<string, Row[]>>((result, row) => {
          const rule = String(row.rule ?? "other");
          (result[rule] ??= []).push(row);
          return result;
        }, {});
        return (
          <div
            className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"
            data-testid="master-data-quality"
          >
            {Object.entries(grouped).map(([rule, issues]) => (
              <Card key={rule}>
                <CardHeader>
                  <CardTitle className="capitalize">{rule.replaceAll("-", " ")}</CardTitle>
                  <CardDescription>Current worker-master exceptions</CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-3xl font-semibold tabular-nums">{issues.length}</p>
                  <p className="mt-2 text-xs text-muted-foreground">
                    Resolve these records before the next payroll or statutory filing.
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        );
      }}
    </Async>
  );
}
