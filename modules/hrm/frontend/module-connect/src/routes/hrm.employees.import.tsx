import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeft, FileSpreadsheet, ShieldCheck, UsersRound } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/employees/import")({
  head: () => ({
    meta: [
      { title: "Import employees — Kuubu HRM" },
      {
        name: "description",
        content: "Import employees from CSV or Excel with mapping, preview and server validation.",
      },
    ],
  }),
  component: EmployeeImportPage,
});

function EmployeeImportPage() {
  return (
    <AuthGate>
      <AppShell>
        <div className="mx-auto min-w-0 max-w-6xl space-y-6 pb-16">
          <PageHeader
            eyebrow="People / employees"
            title="Import employees"
            description="Bring employees in from CSV or Excel. The file is mapped first, then the server previews every create, update, fill-missing result, skip or error before anything is written."
            meta={
              <Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground">
                <ShieldCheck className="size-3" aria-hidden />
                Server validated
              </Badge>
            }
            primaryAction={
              <Button asChild variant="outline">
                <Link to="/hrm/employees">
                  <ArrowLeft className="mr-1 size-4" aria-hidden />
                  Employees
                </Link>
              </Button>
            }
          />

          <section className="grid gap-3 md:grid-cols-3" aria-label="Employee import safeguards">
            <Safeguard
              icon={FileSpreadsheet}
              title="Map your sheet"
              detail="Use the column names already in your spreadsheet and map them to employee fields."
            />
            <Safeguard
              icon={ShieldCheck}
              title="Preview first"
              detail="Rows are checked by the backend before records are created or updated."
            />
            <Safeguard
              icon={UsersRound}
              title="Protect identity"
              detail="Employee number, email, phone, NRC and TPIN are checked for duplicate identities before anything is written."
            />
          </section>

          <ImportDialog typeKey="workers" presentation="embedded" />
        </div>
      </AppShell>
    </AuthGate>
  );
}

function Safeguard({
  icon: Icon,
  title,
  detail,
}: {
  icon: typeof FileSpreadsheet;
  title: string;
  detail: string;
}) {
  return (
    <Card className="shadow-none">
      <CardContent className="flex gap-3 p-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-secondary text-secondary-foreground">
          <Icon className="size-5" aria-hidden />
        </span>
        <span>
          <span className="block text-sm font-semibold">{title}</span>
          <span className="mt-1 block text-xs leading-5 text-muted-foreground">{detail}</span>
        </span>
      </CardContent>
    </Card>
  );
}
