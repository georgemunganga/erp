import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Database, FileSpreadsheet, RefreshCw, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ExportButton } from "@/platform/components/ImportExport/ExportButton";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/data/import-export")({
  head: () => ({
    meta: [
      { title: "Import and export — Kuubu HRM" },
      { name: "description", content: "Use one shared, schema-driven import and export workflow across HRM data." },
    ],
  }),
  component: ImportExportPage,
});

function ImportExportPage() {
  const state = useApi(realApi.importSchemas, []);
  const [reloadKey, setReloadKey] = useState(0);
  const schemas = state.data ?? [];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Configuration / data movement"
          title="Import and export"
          description="Move HRM data using one consistent workflow. Select a data type, map your file, preview server validation, and confirm only the rows you want to write."
          meta={<Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground"><ShieldCheck className="size-3" aria-hidden /> Server-validated data movement</Badge>}
        />
        <div className="space-y-6" data-testid="import-export-page">
          <section className="grid gap-3 md:grid-cols-3" aria-label="Shared import and export safeguards">
            <Safeguard title="One importer" detail="Every registered data type uses the same map, preview, and apply flow." />
            <Safeguard title="Nothing writes early" detail="The server validates rows before you confirm accepted changes." />
            <Safeguard title="Exports round-trip" detail="CSV and Excel downloads use the same canonical fields as imports." />
          </section>

          {state.loading ? <div className="rounded-xl border border-dashed p-10 text-center text-sm text-muted-foreground">Loading the live import schemas…</div> : state.error ? <div className="flex items-center justify-between gap-4 rounded-xl border border-danger/30 bg-danger-soft/30 p-4 text-sm" role="alert"><span>The live import/export registry could not be loaded: {state.error}</span><Button variant="outline" size="sm" onClick={() => state.reload()} className="gap-2"><RefreshCw className="size-4" aria-hidden />Try again</Button></div> : schemas.length === 0 ? <div className="rounded-xl border border-dashed p-10 text-center"><p className="font-medium">No live data types are registered</p><p className="mt-1 text-sm text-muted-foreground">Nothing can be imported or exported until a server schema is available.</p></div> : <div className="grid gap-4 md:grid-cols-2">{schemas.map((schema) => <DataTypeCard key={`${schema.typeKey}-${reloadKey}`} schema={schema} onDone={() => setReloadKey((value) => value + 1)} />)}</div>}

          <Card className="border-primary/20 bg-primary-soft/20 shadow-none"><CardContent className="flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between"><div><p className="font-semibold">Need a task-specific entry point?</p><p className="mt-1 text-sm text-muted-foreground">Attendance has its own import page so the result can hand off directly to overtime review.</p></div><Button asChild variant="outline" className="gap-2"><Link to="/hrm/time/attendance/import">Open attendance import <ArrowRight className="size-4" aria-hidden /></Link></Button></CardContent></Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}

function DataTypeCard({ schema, onDone }: { schema: { typeKey: string; displayName: string; fields: Array<{ key: string; label: string; required: boolean }> }; onDone: () => void }) {
  const required = schema.fields.filter((field) => field.required).length;
  return <Card className="shadow-none"><CardHeader className="pb-4"><div className="flex items-start justify-between gap-3"><div className="flex items-start gap-3"><span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary text-secondary-foreground"><Database className="size-5" aria-hidden /></span><div><CardTitle className="text-base">{schema.displayName}</CardTitle><CardDescription className="mt-1">{schema.fields.length} fields · {required} required · live server schema</CardDescription></div></div><Badge variant="outline" className="font-mono text-[10px]">{schema.typeKey}</Badge></div></CardHeader><CardContent className="flex flex-wrap gap-2"><ImportDialog typeKey={schema.typeKey} onDone={onDone} /><ExportButton typeKey={schema.typeKey} fileName={schema.typeKey} /><span className="basis-full text-xs text-muted-foreground">Use Import for CSV/Excel mapping and preview. Use Export to download the current server dataset.</span></CardContent></Card>;
}

function Safeguard({ title, detail }: { title: string; detail: string }) {
  return <div className="rounded-xl border bg-card p-4"><div className="flex items-center gap-2"><FileSpreadsheet className="size-4 text-primary-foreground" aria-hidden /><p className="text-sm font-semibold">{title}</p></div><p className="mt-2 text-xs leading-5 text-muted-foreground">{detail}</p></div>;
}
