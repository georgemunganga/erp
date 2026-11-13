import { createFileRoute, Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { ArrowRight, FileSpreadsheet, History, ShieldCheck, Upload } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { ExportButton } from "@/platform/components/ImportExport/ExportButton";
import { isTimesheetOnly, useAuth } from "@/platform/auth";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/attendance/import")({
  head: () => ({
    meta: [
      { title: "Import attendance — Kuubu HRM" },
      { name: "description", content: "Import attendance from the shared spreadsheet workflow and review the results." },
    ],
  }),
  component: AttendanceImportPage,
});

function AttendanceImportPage() {
  const { user } = useAuth();
  if (isTimesheetOnly(user?.roles ?? [])) return <AuthGate><AppShell>
    <PageHeader eyebrow="Timesheets" title="Import attendance" description="Upload employee clock-in and clock-out records. Imported overtime goes to an administrator for approval." />
    <ImportDialog typeKey="attendance" presentation="embedded" />
  </AppShell></AuthGate>;
  return <FullAttendanceImportPage />;
}

function FullAttendanceImportPage() {
  const history = useApi(realApi.timeOperationsHistory, []);
  const imports = history.data?.imports ?? [];
  const audits = history.data?.timeAudits ?? [];
  const [overtimeText, setOvertimeText] = useState("employeeNo,workDate,overtimeHours,overtimeMultiplier,reason,status\nEMP-0001,26-08-2026,2,1.5,Approved overtime,pending");
  const [importing, setImporting] = useState(false);
  const [overtimeResult, setOvertimeResult] = useState<Record<string, unknown> | null>(null);

  const overtimeRows = useMemo(() => parseOvertimeRows(overtimeText), [overtimeText]);

  async function submitOvertimeImport() {
    setImporting(true);
    setOvertimeResult(null);
    try {
      const result = await realApi.importOvertime({
        fileName: "manual-overtime-import.csv",
        markApproved: false,
        rows: overtimeRows.rows,
      });
      setOvertimeResult(result);
      history.reload();
    } catch (error) {
      setOvertimeResult({ status: "failed", errors: [error instanceof Error ? error.message : "Import failed"] });
    } finally {
      setImporting(false);
    }
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave / attendance"
          title="Import attendance"
          description="Bring clock-in and clock-out records into the HRM, validate them, and send derived overtime to review."
          meta={<Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground"><FileSpreadsheet className="size-3" aria-hidden /> Shared Import/Export workflow</Badge>}
          primaryAction={
            <div className="flex flex-wrap gap-2">
              <ExportButton typeKey="attendance" fileName="attendance" />
            </div>
          }
        />

        <div className="space-y-6" data-testid="attendance-import-page">
          <section className="grid gap-3 md:grid-cols-3" aria-label="Import steps">
            <Step number="1" title="Choose your file" detail="Use CSV or Excel from your attendance device or working sheet." active />
            <Step number="2" title="Check the preview" detail="Map columns and let the server identify errors before writing." />
            <Step number="3" title="Review overtime" detail="Imported hours are reconciled before they can enter payroll." />
          </section>

          <Card className="border-primary/20 bg-primary-soft/20 shadow-none">
            <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground"><Upload className="size-5" aria-hidden /></span>
                <div>
                  <p className="font-semibold">Attendance uses the shared import tool</p>
                  <p className="mt-1 max-w-2xl text-sm leading-5 text-muted-foreground">Upload CSV or Excel, map the real column titles, manually edit rows if needed, preview server validation, then confirm only accepted rows. Nothing is written before the preview step.</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
            <div className="space-y-6">
              <ImportDialog typeKey="attendance" presentation="embedded" onDone={() => history.reload()} />

              <Separator />

              <Card className="shadow-none" data-testid="overtime-import-card">
                <CardHeader className="pb-4">
                  <CardTitle className="flex items-center gap-2 text-base"><ShieldCheck className="size-4" aria-hidden />Import overtime hours only</CardTitle>
                  <CardDescription>Use this when HR has approved overtime hours instead of full clock-in and clock-out logs. Rows stay pending for review unless the backend request explicitly marks them approved.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Textarea
                    className="min-h-40 font-mono text-xs"
                    value={overtimeText}
                    onChange={(event) => setOvertimeText(event.target.value)}
                    aria-label="Overtime CSV rows"
                  />
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <p className="text-xs text-muted-foreground">
                      {overtimeRows.rows.length} valid row{overtimeRows.rows.length === 1 ? "" : "s"} ready. Dates can use DD-MM-YYYY or YYYY-MM-DD.
                    </p>
                    <Button onClick={submitOvertimeImport} disabled={importing || overtimeRows.rows.length === 0}>
                      {importing ? "Importing..." : "Import overtime"}
                    </Button>
                  </div>
                  {overtimeRows.errors.length > 0 && <div className="rounded-lg border border-danger/30 bg-danger-soft p-3 text-xs text-danger">{overtimeRows.errors.slice(0, 4).join(" ")}</div>}
                  {overtimeResult && <div className="rounded-lg border bg-muted/40 p-3 text-xs text-muted-foreground" data-testid="overtime-import-result">
                    Status: {String(overtimeResult.status ?? "completed")} · Imported: {String(overtimeResult.importedCount ?? 0)} · Updated: {String(overtimeResult.updatedCount ?? 0)} · Rejected: {String(overtimeResult.rejectedCount ?? 0)}
                  </div>}
                </CardContent>
              </Card>

              <Card className="shadow-none">
                <CardHeader className="pb-4"><CardTitle className="flex items-center gap-2 text-base"><History className="size-4" aria-hidden />Recent attendance imports</CardTitle><CardDescription>Persisted import activity for your organisation scope.</CardDescription></CardHeader>
                <CardContent>
                  {history.loading ? <p className="text-sm text-muted-foreground">Loading import history...</p> : imports.length === 0 ? <div className="rounded-xl border border-dashed p-8 text-center"><p className="font-medium">No attendance imports yet</p><p className="mt-1 text-sm text-muted-foreground">Your confirmed imports will appear here with accepted and rejected row counts.</p></div> : <div className="divide-y rounded-xl border">{imports.slice(0, 8).map((item) => <div key={String(item.batchId ?? item.id)} className="flex flex-wrap items-center justify-between gap-3 p-4"><div><p className="font-medium">{String(item.fileName ?? "Attendance import")}</p><p className="mt-1 text-xs text-muted-foreground">Batch {String(item.batchId ?? item.id ?? "-")}</p></div><div className="flex items-center gap-2 text-xs"><Badge variant="outline" className="border-success/30 bg-success-soft text-success-foreground">{String(item.importedCount ?? item.created ?? 0)} imported</Badge><Badge variant="outline" className="border-danger/30 bg-danger-soft text-danger">{String(item.rejectedCount ?? item.errors ?? 0)} rejected</Badge></div></div>)}</div>}
                </CardContent>
              </Card>
            </div>

            <Card className="h-fit shadow-none"><CardHeader className="pb-4"><CardTitle className="text-base">Next step</CardTitle><CardDescription>Attendance import does not approve overtime automatically.</CardDescription></CardHeader><CardContent><p className="text-sm leading-5 text-muted-foreground">After a successful import, open the review queue to check scheduled hours, worked hours, overtime multipliers, and decision notes before payroll.</p><Button asChild className="mt-4 w-full gap-2"><Link to="/hrm/time/operations">Review overtime <ArrowRight className="size-4" aria-hidden /></Link></Button></CardContent></Card>
          </div>

          <Card className="shadow-none">
            <CardHeader className="pb-4"><CardTitle className="text-base">Time audit evidence</CardTitle><CardDescription>Recent attendance and overtime changes recorded in the append-only audit trail.</CardDescription></CardHeader>
            <CardContent>
              {history.loading ? <p className="text-sm text-muted-foreground">Loading audit trail...</p> : audits.length === 0 ? <p className="rounded-xl border border-dashed p-6 text-sm text-muted-foreground">No time audit entries yet.</p> : <div className="divide-y rounded-xl border">{audits.slice(0, 10).map((entry) => <div key={String(entry.id)} className="grid gap-2 p-4 text-sm md:grid-cols-[180px_minmax(0,1fr)_220px]"><p className="font-medium">{String(entry.action ?? "change")}</p><p className="truncate text-muted-foreground">{String(entry.entityType ?? "time")} · {String(entry.entityId ?? "")}</p><p className="text-xs text-muted-foreground md:text-right">{String(entry.actorSubjectId ?? "system")}</p></div>)}</div>}
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}

function Step({ number, title, detail, active = false }: { number: string; title: string; detail: string; active?: boolean }) {
  return <div className={`rounded-xl border p-4 ${active ? "border-primary/40 bg-primary-soft/30" : "bg-card"}`}><div className="flex items-center gap-2"><span className={`flex size-7 items-center justify-center rounded-full text-xs font-semibold ${active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}>{number}</span><p className="text-sm font-semibold">{title}</p></div><p className="mt-2 text-xs leading-5 text-muted-foreground">{detail}</p></div>;
}

function parseOvertimeRows(text: string): { rows: Array<Record<string, unknown>>; errors: string[] } {
  const lines = text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length <= 1) return { rows: [], errors: [] };
  const headers = lines[0].split(",").map((h) => h.trim());
  const errors: string[] = [];
  const rows = lines.slice(1).map((line, index) => {
    const values = line.split(",").map((v) => v.trim());
    const raw: Record<string, string> = {};
    headers.forEach((header, i) => { raw[header] = values[i] ?? ""; });
    const employeeNo = raw.employeeNo;
    const workDate = normalizeDate(raw.workDate);
    const overtimeHours = Number(raw.overtimeHours);
    const overtimeMultiplier = raw.overtimeMultiplier ? Number(raw.overtimeMultiplier) : undefined;
    if (!employeeNo || !workDate || !Number.isFinite(overtimeHours) || overtimeHours <= 0) {
      errors.push(`Row ${index + 2} needs employeeNo, workDate, and positive overtimeHours.`);
    }
    return {
      employeeNo,
      workDate,
      overtimeHours,
      overtimeMultiplier,
      reason: raw.reason || undefined,
      status: raw.status || undefined,
    };
  }).filter((row) => row.employeeNo && row.workDate && Number(row.overtimeHours) > 0);
  return { rows, errors };
}

function normalizeDate(value: string | undefined) {
  if (!value) return "";
  const ddmmyyyy = /^(\d{2})-(\d{2})-(\d{4})$/.exec(value.trim());
  if (!ddmmyyyy) return value.trim();
  return `${ddmmyyyy[3]}-${ddmmyyyy[2]}-${ddmmyyyy[1]}`;
}
