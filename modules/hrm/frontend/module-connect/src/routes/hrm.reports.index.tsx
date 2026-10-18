import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import * as XLSX from "xlsx";
import {
  BarChart3,
  BadgeCheck,
  Download,
  FileText,
  Landmark,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import { Bar, BarChart, CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { useRoleGate } from "@/platform/app-context";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/reports/")({
  head: () => ({
    meta: [
      { title: "Management reports — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "Trusted workforce, payroll, time, recruitment and statutory management reporting.",
      },
    ],
  }),
  component: ReportsPage,
});

interface Dimension {
  id: string;
  code: string;
  name: string;
}
interface ReportFilters {
  fromDate: string;
  toDate: string;
  legalEntities: Dimension[];
  orgUnits: Dimension[];
  locations: Dimension[];
}
interface Kpi {
  code: string;
  label: string;
  value: number;
  unit: string;
  definition: string;
  source: string;
}
interface Trend {
  period: string;
  headcount: number;
  hires: number;
  leavers: number;
  grossPay: number;
  employerCost: number;
}
interface Department {
  orgUnitId?: string;
  department: string;
  headcount: number;
  payrollWorkers: number;
  grossPay: number;
  deductions: number;
  netPay: number;
  employerContributions: number;
  employerCost: number;
}
interface LeaveRow {
  leaveType: string;
  requests: number;
  approvedDays: number;
  pendingDays: number;
}
interface AttendanceRow {
  status: string;
  records: number;
  scheduledHours: number;
  workedHours: number;
  overtimeHours: number;
}
interface RecruitmentRow {
  stage: string;
  candidates: number;
  percentage: number;
}
interface MovementRow {
  movementType: string;
  movements: number;
}
interface Statutory {
  paye: number;
  napsaEmployee: number;
  napsaEmployer: number;
  nhimaEmployee: number;
  nhimaEmployer: number;
  total: number;
}
interface Catalogue {
  code: string;
  name: string;
  category: string;
  description: string;
  owner: string;
  certified: boolean;
  payrollRestricted: boolean;
  source: string;
}
type ExportFormat = "csv" | "xlsx" | "pdf";
type AuthorityFormat = "spreadsheet" | "pdf";
type FilingCode = (typeof statutoryFilings)[number]["code"];
type LayoutColumn = { id: string; header: string; field: string; include: boolean };
type StatutoryPreview = {
  exportType: string;
  periodLabel: string;
  currency: string;
  templateColumns: string[];
  rows: Array<Record<string, string>>;
};

interface Dashboard {
  generatedAt: string;
  dataThrough: string;
  filters: ReportFilters;
  kpis: Kpi[];
  trend: Trend[];
  departments: Department[];
  leave: LeaveRow[];
  attendance: AttendanceRow[];
  recruitment: RecruitmentRow[];
  movements: MovementRow[];
  statutoryLiability: Statutory;
  catalogue: Catalogue[];
  reconciliationNotes: string[];
}

const workforceChart: ChartConfig = {
  headcount: { label: "Headcount", color: "hsl(var(--primary))" },
  hires: { label: "Hires", color: "hsl(var(--success))" },
  leavers: { label: "Leavers", color: "hsl(var(--danger))" },
};
const costChart: ChartConfig = {
  grossPay: { label: "Gross pay", color: "hsl(var(--primary))" },
  employerCost: { label: "Employer cost", color: "hsl(var(--warning))" },
};
const statutoryFilings = [
  { code: "paye-return", label: "ZRA PAYE return", icon: Landmark },
  { code: "zra", label: "ZRA schedule", icon: FileText },
  { code: "napsa", label: "NAPSA remittance", icon: FileText },
  { code: "nhima", label: "NHIMA remittance", icon: FileText },
] as const;

const authorityFields = [
  ["blank", "Blank column"],
  ["companyAccount", "Company account"],
  ["year", "Year"],
  ["period", "Period"],
  ["month", "Month"],
  ["employeeNo", "Employee number"],
  ["ssn", "NAPSA SSN"],
  ["napsaNumber", "NAPSA number"],
  ["nhimaNumber", "NHIMA number"],
  ["nrc", "NRC"],
  ["tpin", "TPIN"],
  ["tpinNrc", "TPIN or NRC"],
  ["identityType", "Identity type"],
  ["surname", "Surname"],
  ["firstName", "First name"],
  ["fullName", "Full name"],
  ["employmentNature", "Employment nature"],
  ["dateOfBirth", "Date of birth"],
  ["gross", "Gross"],
  ["grossEmoluments", "Gross emoluments"],
  ["chargeableEmoluments", "Chargeable emoluments"],
  ["netPay", "Net pay"],
  ["paye", "PAYE"],
  ["taxDeducted", "Tax deducted"],
  ["totalTaxCredit", "Total tax credit"],
  ["taxAdjusted", "Tax adjusted"],
  ["napsaEmployee", "NAPSA employee"],
  ["napsaEmployer", "NAPSA employer"],
  ["napsaTotal", "NAPSA total"],
  ["nhimaEmployee", "NHIMA employee"],
  ["nhimaEmployer", "NHIMA employer"],
  ["nhimaTotal", "NHIMA total"],
  ["status", "Status"],
] as const;

const defaultAuthorityLayout: Record<FilingCode, Array<[string, string]>> = {
  "paye-return": [
    ["identityType", "identityType"],
    ["tpinNrc", "tpinNrc"],
    ["fullName", "fullName"],
    ["employmentNature", "employmentNature"],
    ["grossEmoluments", "grossEmoluments"],
    ["chargeableEmoluments", "chargeableEmoluments"],
    ["totalTaxCredit", "totalTaxCredit"],
    ["taxDeducted", "taxDeducted"],
    ["taxAdjusted", "taxAdjusted"],
  ],
  zra: [
    ["Employee No", "employeeNo"],
    ["Employee Name", "fullName"],
    ["TPIN", "tpin"],
    ["Gross Pay", "gross"],
    ["PAYE", "paye"],
    ["Net Pay", "netPay"],
  ],
  napsa: [
    ["Company Account", "companyAccount"],
    ["Year", "year"],
    ["Period", "period"],
    ["SSN", "napsaNumber"],
    ["NRC", "nrc"],
    ["Surname", "surname"],
    ["First Name", "firstName"],
    ["", "blank"],
    ["Date of Birth", "dateOfBirth"],
    ["Gross", "gross"],
    ["Employee Contribution", "napsaEmployee"],
    ["Employer Contribution", "napsaEmployer"],
    ["Status", "status"],
  ],
  nhima: [
    ["Year", "year"],
    ["Month", "month"],
    ["NHIMA", "nhimaNumber"],
    ["NRC", "nrc"],
    ["Surname", "surname"],
    ["First Name", "firstName"],
    ["Date of Birth", "dateOfBirth"],
    ["Gross", "gross"],
    ["Employee Contribution", "nhimaEmployee"],
    ["Employer Contribution", "nhimaEmployer"],
  ],
};

function isoDate(value: Date) {
  return value.toISOString().slice(0, 10);
}
function initialDates() {
  const now = new Date();
  return { fromDate: `${now.getUTCFullYear()}-01-01`, toDate: isoDate(now) };
}
function currency(value: number) {
  return value.toLocaleString("en-ZM", {
    style: "currency",
    currency: "ZMW",
    minimumFractionDigits: 2,
  });
}
function number(value: number, digits = 1) {
  return value.toLocaleString("en-ZM", { maximumFractionDigits: digits });
}
function makeLayout(code: FilingCode, preview?: StatutoryPreview): LayoutColumn[] {
  const base = preview?.templateColumns?.length
    ? preview.templateColumns.map((header, index) => {
        const fallback = defaultAuthorityLayout[code][index]?.[1] ?? "blank";
        return [header, fallback] as [string, string];
      })
    : defaultAuthorityLayout[code];
  return base.map(([header, field], index) => ({
    id: `${code}-${index}`,
    header,
    field,
    include: true,
  }));
}
function exportRows(layout: LayoutColumn[], rows: Array<Record<string, string>>) {
  const active = layout.filter((column) => column.include);
  return {
    headers: active.map((column) => column.header),
    rows: rows.map((row) => active.map((column) => row[column.field] ?? "")),
  };
}
function csvCell(value: string) {
  return /[",\n\r]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}
function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
function downloadAuthoritySpreadsheet(code: FilingCode, label: string, period: string, layout: LayoutColumn[], sourceRows: Array<Record<string, string>>) {
  const { headers, rows } = exportRows(layout, sourceRows);
  const safePeriod = period.replaceAll(/\s+/g, "-").toLowerCase();
  if (code === "paye-return") {
    const sheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, "PayeEmployeeDetails");
    XLSX.writeFile(book, `${label}-${safePeriod}.xlsx`);
    return;
  }
  const csv = [headers, ...rows].map((row) => row.map((cell) => csvCell(String(cell))).join(",")).join("\r\n");
  downloadBlob(new Blob([`${csv}\r\n`], { type: "text/csv;charset=utf-8" }), `${label}-${safePeriod}.csv`);
}
function openAuthorityPdf(label: string, period: string, layout: LayoutColumn[], sourceRows: Array<Record<string, string>>) {
  const { headers, rows } = exportRows(layout, sourceRows);
  const table = [
    `<thead><tr>${headers.map((h) => `<th>${h || "&nbsp;"}</th>`).join("")}</tr></thead>`,
    `<tbody>${rows
      .map((row) => `<tr>${row.map((cell) => `<td>${String(cell).replaceAll("&", "&amp;").replaceAll("<", "&lt;")}</td>`).join("")}</tr>`)
      .join("")}</tbody>`,
  ].join("");
  const doc = window.open("", "_blank", "noopener,noreferrer,width=1200,height=800");
  if (!doc) return;
  doc.document.write(`<!doctype html><html><head><title>${label} ${period}</title><style>
    body{font-family:Arial,sans-serif;margin:24px;color:#111827} h1{font-size:18px;margin:0 0 4px}
    p{margin:0 0 16px;color:#4b5563;font-size:12px} table{border-collapse:collapse;width:100%;font-size:11px}
    th,td{border:1px solid #d1d5db;padding:6px;text-align:left;white-space:nowrap} th{background:#f3f4f6}
    @media print{@page{size:landscape;margin:12mm} button{display:none}}
  </style></head><body><button onclick="window.print()">Print or save PDF</button><h1>${label}</h1><p>${period}</p><table>${table}</table></body></html>`);
  doc.document.close();
}
function kpiValue(kpi: Kpi) {
  if (kpi.unit === "ZMW") return currency(kpi.value);
  if (kpi.unit === "percent") return `${number(kpi.value, 2)}%`;
  if (kpi.unit === "hours") return `${number(kpi.value)} h`;
  if (kpi.unit === "days") return `${number(kpi.value)} days`;
  return number(kpi.value, 0);
}

function ReportsPage() {
  const defaults = useMemo(initialDates, []);
  const [draft, setDraft] = useState({
    ...defaults,
    legalEntityId: "all",
    orgUnitId: "all",
    locationId: "all",
  });
  const [filters, setFilters] = useState(draft);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [exportFormat, setExportFormat] = useState<ExportFormat>("pdf");
  const canPayroll = useRoleGate()(["payroll", "hr_admin"]);
  const params = useMemo(
    () => ({
      fromDate: filters.fromDate,
      toDate: filters.toDate,
      ...(filters.legalEntityId !== "all" ? { legalEntityId: filters.legalEntityId } : {}),
      ...(filters.orgUnitId !== "all" ? { orgUnitId: filters.orgUnitId } : {}),
      ...(filters.locationId !== "all" ? { locationId: filters.locationId } : {}),
    }),
    [filters],
  );
  const report = useApi(
    () => realApi.managementReports(params) as Promise<Dashboard>,
    [JSON.stringify(params)],
  );
  const data = report.data;

  async function download(item: Catalogue) {
    setDownloading(item.code);
    try {
      await realApi.downloadManagementReport(item.code, params, exportFormat);
    } finally {
      setDownloading(null);
    }
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Reports"
          title="Workforce and payroll intelligence"
          description="One trusted view of people, cost, time, recruitment and statutory liabilities — reconciled to operational HRM records."
          primaryAction={
            <Button variant="outline" onClick={report.reload}>
              <RefreshCw className="mr-2 size-4" />
              Refresh
            </Button>
          }
        />

        <section
          data-testid="management-reporting"
          aria-label="Reporting filters"
          className="rounded-xl border bg-surface p-4"
        >
          <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
            <div>
              <Label htmlFor="report-from">From</Label>
              <input
                id="report-from"
                type="date"
                value={draft.fromDate}
                onChange={(e) => setDraft((x) => ({ ...x, fromDate: e.target.value }))}
                className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm"
              />
            </div>
            <div>
              <Label htmlFor="report-to">To</Label>
              <input
                id="report-to"
                type="date"
                value={draft.toDate}
                onChange={(e) => setDraft((x) => ({ ...x, toDate: e.target.value }))}
                className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm"
              />
            </div>
            <FilterSelect
              label="Legal entity"
              value={draft.legalEntityId}
              options={data?.filters.legalEntities ?? []}
              onChange={(value) =>
                setDraft((x) => ({
                  ...x,
                  legalEntityId: value,
                  orgUnitId: "all",
                  locationId: "all",
                }))
              }
            />
            <FilterSelect
              label="Department"
              value={draft.orgUnitId}
              options={data?.filters.orgUnits ?? []}
              onChange={(value) => setDraft((x) => ({ ...x, orgUnitId: value }))}
            />
            <FilterSelect
              label="Location"
              value={draft.locationId}
              options={data?.filters.locations ?? []}
              onChange={(value) => setDraft((x) => ({ ...x, locationId: value }))}
            />
            <div className="flex items-end">
              <Button className="w-full" onClick={() => setFilters(draft)}>
                Apply filters
              </Button>
            </div>
          </div>
          {data ? (
            <p className="mt-3 text-xs text-muted-foreground">
              Data through {data.dataThrough} · refreshed{" "}
              {new Date(data.generatedAt).toLocaleString("en-ZM")}
            </p>
          ) : null}
        </section>

        {report.loading ? (
          <DashboardSkeleton />
        ) : report.error ? (
          <StateMessage title="Reports unavailable" detail={report.error} />
        ) : report.degraded ? (
          <StateMessage
            title="Reporting service unavailable"
            detail="The live HRM API did not respond. No fallback figures are shown because management reports must remain source-backed."
          />
        ) : data ? (
          <>
            <section
              aria-label="Key indicators"
              className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"
            >
              {data.kpis.map((kpi) => (
                <article
                  key={kpi.code}
                  className="rounded-xl border bg-surface p-4"
                  title={`${kpi.definition} Source: ${kpi.source}`}
                >
                  <p className="text-xs font-medium text-muted-foreground">{kpi.label}</p>
                  <p className="mt-1 text-2xl font-semibold tabular-nums">{kpiValue(kpi)}</p>
                  <p className="mt-2 line-clamp-2 text-[11px] text-muted-foreground">
                    {kpi.definition}
                  </p>
                </article>
              ))}
            </section>

            <section aria-label="Trends" className="grid gap-4 xl:grid-cols-2">
              <ChartCard
                title="Workforce movement"
                subtitle="Closing headcount, hires and leavers by month"
              >
                <ChartContainer config={workforceChart} className="h-72 w-full aspect-auto">
                  <LineChart data={data.trend} margin={{ left: 0, right: 12 }}>
                    <CartesianGrid vertical={false} />
                    <XAxis dataKey="period" tickLine={false} axisLine={false} />
                    <YAxis allowDecimals={false} tickLine={false} axisLine={false} />
                    <ChartTooltip content={<ChartTooltipContent />} />
                    <Line
                      type="monotone"
                      dataKey="headcount"
                      stroke="var(--color-headcount)"
                      strokeWidth={2}
                    />
                    <Line
                      type="monotone"
                      dataKey="hires"
                      stroke="var(--color-hires)"
                      strokeWidth={2}
                    />
                    <Line
                      type="monotone"
                      dataKey="leavers"
                      stroke="var(--color-leavers)"
                      strokeWidth={2}
                    />
                  </LineChart>
                </ChartContainer>
              </ChartCard>
              <ChartCard
                title="Payroll cost trend"
                subtitle="Released gross pay and total employer cost"
              >
                <ChartContainer config={costChart} className="h-72 w-full aspect-auto">
                  <BarChart data={data.trend} margin={{ left: 4, right: 12 }}>
                    <CartesianGrid vertical={false} />
                    <XAxis dataKey="period" tickLine={false} axisLine={false} />
                    <YAxis
                      tickFormatter={(v) => `${number(Number(v) / 1000, 0)}k`}
                      tickLine={false}
                      axisLine={false}
                    />
                    <ChartTooltip
                      content={
                        <ChartTooltipContent formatter={(value) => currency(Number(value))} />
                      }
                    />
                    <Bar dataKey="grossPay" fill="var(--color-grossPay)" radius={[4, 4, 0, 0]} />
                    <Bar
                      dataKey="employerCost"
                      fill="var(--color-employerCost)"
                      radius={[4, 4, 0, 0]}
                    />
                  </BarChart>
                </ChartContainer>
              </ChartCard>
            </section>

            <section aria-label="Department payroll" className="rounded-xl border bg-surface">
              <SectionHeading
                title="Cost and headcount by department"
                subtitle="Payroll dimensions use the assignment effective at each pay-period end."
              />
              <div className="overflow-x-auto">
                <RecordTable variant="compact" className="w-full text-left text-sm">
                  <thead className="border-y bg-muted/40 text-xs text-muted-foreground">
                    <tr>
                      <th className="px-4 py-2">Department</th>
                      <th className="px-4 py-2 text-right">Headcount</th>
                      <th className="px-4 py-2 text-right">Gross</th>
                      <th className="px-4 py-2 text-right">Deductions</th>
                      <th className="px-4 py-2 text-right">Net</th>
                      <th className="px-4 py-2 text-right">Employer cost</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.departments.map((row) => (
                      <tr key={row.orgUnitId ?? row.department} className="border-b last:border-0">
                        <td className="px-4 py-3 font-medium">{row.department}</td>
                        <td className="px-4 py-3 text-right tabular-nums">{row.headcount}</td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {currency(row.grossPay)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {currency(row.deductions)}
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums">
                          {currency(row.netPay)}
                        </td>
                        <td className="px-4 py-3 text-right font-medium tabular-nums">
                          {currency(row.employerCost)}
                        </td>
                      </tr>
                    ))}
                    {data.departments.length === 0 ? (
                      <tr>
                        <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                          No released payroll or active headcount in this window.
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </RecordTable>
              </div>
            </section>

            <section aria-label="Operational reports" className="grid gap-4 xl:grid-cols-3">
              <CompactTable
                title="Leave"
                headers={["Type", "Approved", "Pending"]}
                rows={data.leave.map((x) => [
                  x.leaveType,
                  `${number(x.approvedDays)} d`,
                  `${number(x.pendingDays)} d`,
                ])}
              />
              <CompactTable
                title="Attendance"
                headers={["Status", "Records", "Overtime"]}
                rows={data.attendance.map((x) => [
                  x.status,
                  number(x.records, 0),
                  `${number(x.overtimeHours)} h`,
                ])}
              />
              <CompactTable
                title="Recruitment funnel"
                headers={["Stage", "Candidates", "Mix"]}
                rows={data.recruitment.map((x) => [
                  x.stage,
                  number(x.candidates, 0),
                  `${number(x.percentage)}%`,
                ])}
              />
            </section>

            <section aria-label="Statutory liability" className="rounded-xl border bg-surface p-5">
              <div className="flex items-start gap-3">
                <ShieldCheck className="mt-0.5 size-5 text-primary" />
                <div>
                  <h2 className="font-semibold">Statutory liability control</h2>
                  <p className="text-xs text-muted-foreground">
                    Immutable released line components; employee and employer shares remain
                    separate.
                  </p>
                </div>
              </div>
              <div className="mt-4 grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
                {(
                  [
                    ["PAYE", data.statutoryLiability.paye],
                    ["NAPSA employee", data.statutoryLiability.napsaEmployee],
                    ["NAPSA employer", data.statutoryLiability.napsaEmployer],
                    ["NHIMA employee", data.statutoryLiability.nhimaEmployee],
                    ["NHIMA employer", data.statutoryLiability.nhimaEmployer],
                    ["Total liability", data.statutoryLiability.total],
                  ] as const
                ).map(([label, value]) => (
                  <div key={label} className="rounded-lg bg-muted/40 p-3">
                    <p className="text-[11px] text-muted-foreground">{label}</p>
                    <p className="mt-1 font-semibold tabular-nums">{currency(value)}</p>
                  </div>
                ))}
              </div>
            </section>

            <StatutoryFilings canPayroll={canPayroll} />

            <section aria-label="Report catalogue" className="space-y-3">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                  <h2 className="text-lg font-semibold">Certified exports</h2>
                  <p className="text-sm text-muted-foreground">
                    Choose a delivery format for the selected reporting window. PDFs are print-ready;
                    Excel remains editable; CSV is suited to system import.
                  </p>
                </div>
                <div className="w-full sm:w-44">
                  <Label htmlFor="report-export-format">Export format</Label>
                  <Select value={exportFormat} onValueChange={(value) => setExportFormat(value as ExportFormat)}>
                    <SelectTrigger id="report-export-format" className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="pdf">PDF · print-ready</SelectItem>
                      <SelectItem value="xlsx">Excel · editable</SelectItem>
                      <SelectItem value="csv">CSV · data</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                {data.catalogue.map((item) => {
                  const blocked = item.payrollRestricted && !canPayroll;
                  return (
                    <article
                      key={item.code}
                      className="flex gap-3 rounded-xl border bg-surface p-4"
                    >
                      <div className="mt-0.5 rounded-lg bg-primary/10 p-2 text-primary">
                        <BarChart3 className="size-4" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          <h3 className="text-sm font-semibold">{item.name}</h3>
                          {item.certified ? (
                            <BadgeCheck
                              className="size-4 text-success"
                              aria-label="Certified definition"
                            />
                          ) : null}
                        </div>
                        <p className="mt-1 text-xs text-muted-foreground">{item.description}</p>
                        <p className="mt-2 text-[11px] text-muted-foreground">
                          {item.owner} · {item.source}
                        </p>
                      </div>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={blocked || downloading === item.code}
                        title={
                          blocked ? "Payroll or HR Admin access required" : `Download ${item.name}`
                        }
                        onClick={() => download(item)}
                      >
                        <Download className="mr-1.5 size-3.5" />
                        {downloading === item.code ? "Preparing…" : exportFormat.toUpperCase()}
                      </Button>
                    </article>
                  );
                })}
              </div>
            </section>

            <section
              aria-label="Reconciliation controls"
              className="rounded-xl border border-primary/20 bg-primary/5 p-4"
            >
              <h2 className="text-sm font-semibold">Reconciliation controls</h2>
              <ul className="mt-2 space-y-1 text-xs text-muted-foreground">
                {data.reconciliationNotes.map((note) => (
                  <li key={note}>• {note}</li>
                ))}
              </ul>
            </section>
          </>
        ) : null}
      </AppShell>
    </AuthGate>
  );
}

function StatutoryFilings({ canPayroll }: { canPayroll: boolean }) {
  const [periodId, setPeriodId] = useState("");
  const [downloading, setDownloading] = useState<string | null>(null);
  const [selectedFiling, setSelectedFiling] = useState<(typeof statutoryFilings)[number] | null>(null);
  const runs = useApi(() => realApi.payrollRuns(), []);
  const groups = useApi(() => realApi.payrollPayGroups(), []);
  const groupRows = useMemo(() => (groups.data ?? []) as Array<Record<string, unknown>>, [groups.data]);
  const groupId = String(groupRows[0]?.id ?? "");
  const periods = useApi(
    () => (groupId ? realApi.payrollPayGroupPeriods(groupId) : Promise.resolve([])),
    [groupId],
  );
  const periodRows = useMemo(
    () => (periods.data ?? []) as Array<Record<string, unknown>>,
    [periods.data],
  );
  const released = useMemo(() => {
    const payload = runs.data as { items?: unknown[] } | unknown[] | null;
    const rows = Array.isArray(payload)
      ? payload
      : payload && typeof payload === "object" && Array.isArray(payload.items)
        ? payload.items
        : [];
    const releasedLabels = new Set(
      rows
        .map((row) => row as Record<string, unknown>)
        .filter((row) => row.status === "released" || row.status === "closed")
        .map((row) => String(row.periodLabel ?? ""))
        .filter(Boolean),
    );
    const seen = new Set<string>();
    return periodRows
      .map((row) => ({
        id: String(row.id ?? ""),
        periodLabel: String(row.periodLabel ?? row.id ?? ""),
      }))
      .filter(
        (row) =>
          row.id &&
          releasedLabels.has(row.periodLabel) &&
          !seen.has(row.id) &&
          Boolean(seen.add(row.id)),
      );
  }, [periodRows, runs.data]);
  useEffect(() => {
    if (!periodId && released.length) setPeriodId(String(released[0].id));
  }, [periodId, released]);

  return (
    <section aria-label="Authority filing files" className="rounded-xl border bg-surface p-5">
      <h2 className="font-semibold">Authority filing files</h2>
      <p className="text-xs text-muted-foreground">
        Period-specific ZRA, NAPSA and NHIMA files remain tied to a single released payroll period.
      </p>
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <Select value={periodId} onValueChange={setPeriodId}>
          <SelectTrigger aria-label="Released payroll period" className="w-52">
            <SelectValue placeholder="Select released period" />
          </SelectTrigger>
          <SelectContent>
            {released.map((row) => (
              <SelectItem key={String(row.id)} value={String(row.id)}>
                {String(row.periodLabel ?? row.id)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {statutoryFilings.map((filing) => {
          const Icon = filing.icon;
          return (
            <Button
              key={filing.code}
              size="sm"
              variant="outline"
              disabled={!periodId || !canPayroll || downloading === filing.code}
              onClick={() => setSelectedFiling(filing)}
            >
              <Icon className="mr-1.5 size-3.5" />
              {downloading === filing.code ? "Preparing…" : filing.label}
            </Button>
          );
        })}
      </div>
      {selectedFiling ? (
        <AuthorityFilingDialog
          filing={selectedFiling}
          periodId={periodId}
          open={Boolean(selectedFiling)}
          onOpenChange={(open) => {
            if (!open) setSelectedFiling(null);
          }}
          onBusyChange={setDownloading}
        />
      ) : null}
      {!canPayroll ? (
        <p className="mt-2 text-xs text-warning">
          Payroll or HR Admin access is required for authority files.
        </p>
      ) : null}
      {released.length === 0 && !periods.loading ? (
        <p className="mt-2 text-xs text-muted-foreground">
          No released payroll periods are available.
        </p>
      ) : null}
    </section>
  );
}

function AuthorityFilingDialog({
  filing,
  periodId,
  open,
  onOpenChange,
  onBusyChange,
}: {
  filing: (typeof statutoryFilings)[number];
  periodId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onBusyChange: (code: string | null) => void;
}) {
  const [format, setFormat] = useState<AuthorityFormat>("spreadsheet");
  const [layout, setLayout] = useState<LayoutColumn[]>(() => makeLayout(filing.code));
  const preview = useApi(
    () =>
      open && periodId
        ? (realApi.statutoryPreview(filing.code, periodId) as Promise<StatutoryPreview>)
        : Promise.resolve({
            exportType: filing.code,
            periodLabel: "",
            currency: "ZMW",
            templateColumns: [],
            rows: [],
          }),
    [open, periodId, filing.code],
  );
  useEffect(() => {
    if (preview.data) setLayout(makeLayout(filing.code, preview.data));
  }, [filing.code, preview.data]);

  const table = useMemo(
    () => exportRows(layout, preview.data?.rows ?? []),
    [layout, preview.data?.rows],
  );
  const labelSlug = filing.label.toLowerCase().replaceAll(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

  function updateColumn(id: string, patch: Partial<LayoutColumn>) {
    setLayout((current) => current.map((column) => (column.id === id ? { ...column, ...patch } : column)));
  }

  async function exportConfirmed() {
    if (!preview.data) return;
    onBusyChange(filing.code);
    try {
      if (format === "pdf") openAuthorityPdf(filing.label, preview.data.periodLabel, layout, preview.data.rows);
      else downloadAuthoritySpreadsheet(filing.code, labelSlug, preview.data.periodLabel, layout, preview.data.rows);
      onOpenChange(false);
    } finally {
      onBusyChange(null);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{filing.label}</DialogTitle>
          <DialogDescription>
            Confirm the authority layout before export. The defaults follow the latest template file
            we have, but you can change headers or mapped fields if the portal changes.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 lg:grid-cols-[220px_1fr]">
          <div className="space-y-3 rounded-lg border bg-muted/20 p-3">
            <Label>Export format</Label>
            <Select value={format} onValueChange={(value) => setFormat(value as AuthorityFormat)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="spreadsheet">
                  {filing.code === "paye-return" ? "Excel template" : "CSV spreadsheet"}
                </SelectItem>
                <SelectItem value="pdf">PDF preview</SelectItem>
              </SelectContent>
            </Select>
            <div className="rounded-md border bg-background p-3 text-xs text-muted-foreground">
              <div className="font-medium text-foreground">Rows ready</div>
              <div className="mt-1">{preview.data?.rows.length ?? 0} payroll lines</div>
              <div className="mt-3 font-medium text-foreground">Period</div>
              <div className="mt-1">{preview.data?.periodLabel || "Loading..."}</div>
            </div>
          </div>

          <div className="space-y-4">
            {preview.loading ? (
              <div className="rounded-lg border bg-surface p-6 text-sm text-muted-foreground">
                Loading released payroll data...
              </div>
            ) : preview.error ? (
              <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
                {preview.error}
              </div>
            ) : (
              <>
                <div className="overflow-x-auto rounded-lg border">
                  <RecordTable variant="compact" className="min-w-[980px] text-left text-xs">
                    <thead className="bg-muted/60">
                      <tr>
                        <th className="px-3 py-2">Use</th>
                        <th className="px-3 py-2">Export column</th>
                        <th className="px-3 py-2">Payroll field</th>
                      </tr>
                    </thead>
                    <tbody>
                      {layout.map((column) => (
                        <tr key={column.id} className="border-t">
                          <td className="px-3 py-2">
                            <Checkbox
                              checked={column.include}
                              onCheckedChange={(checked) => updateColumn(column.id, { include: Boolean(checked) })}
                              aria-label={`Include ${column.header || "blank column"}`}
                            />
                          </td>
                          <td className="px-3 py-2">
                            <Input
                              value={column.header}
                              onChange={(event) => updateColumn(column.id, { header: event.target.value })}
                              aria-label="Export column name"
                            />
                          </td>
                          <td className="px-3 py-2">
                            <Select value={column.field} onValueChange={(field) => updateColumn(column.id, { field })}>
                              <SelectTrigger>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {authorityFields.map(([value, label]) => (
                                  <SelectItem key={value} value={value}>
                                    {label}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </RecordTable>
                </div>

                <div>
                  <div className="mb-2 text-sm font-medium">Data preview</div>
                  <div className="overflow-x-auto rounded-lg border">
                    <RecordTable variant="compact" className="min-w-[1100px] text-left text-xs">
                      <thead className="bg-muted/60">
                        <tr>{table.headers.map((header, index) => <th key={`${header}-${index}`} className="px-3 py-2">{header || "Blank"}</th>)}</tr>
                      </thead>
                      <tbody>
                        {table.rows.slice(0, 10).map((row, rowIndex) => (
                          <tr key={rowIndex} className="border-t">
                            {row.map((cell, cellIndex) => (
                              <td key={`${rowIndex}-${cellIndex}`} className="px-3 py-2 whitespace-nowrap">
                                {cell}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </RecordTable>
                  </div>
                  {(preview.data?.rows.length ?? 0) > 10 ? (
                    <p className="mt-2 text-xs text-muted-foreground">
                      Showing the first 10 rows. The export includes all rows.
                    </p>
                  ) : null}
                </div>
              </>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button disabled={preview.loading || Boolean(preview.error) || !preview.data?.rows.length} onClick={exportConfirmed}>
            <Download className="mr-2 size-4" />
            Export {format === "pdf" ? "PDF" : "spreadsheet"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: Dimension[];
  onChange: (value: string) => void;
}) {
  const id = `report-${label.toLowerCase().replaceAll(" ", "-")}`;
  const allLabel = label === "Legal entity" ? "All legal entities" : `All ${label.toLowerCase()}s`;
  return (
    <div>
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id} className="mt-1">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{allLabel}</SelectItem>
          {options.map((x) => (
            <SelectItem key={x.id} value={x.id}>
              {x.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
function ChartCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-surface p-5">
      <h2 className="font-semibold">{title}</h2>
      <p className="mb-4 text-xs text-muted-foreground">{subtitle}</p>
      {children}
    </section>
  );
}
function SectionHeading({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="p-5">
      <h2 className="font-semibold">{title}</h2>
      <p className="text-xs text-muted-foreground">{subtitle}</p>
    </div>
  );
}
function CompactTable({
  title,
  headers,
  rows,
}: {
  title: string;
  headers: string[];
  rows: string[][];
}) {
  return (
    <section className="rounded-xl border bg-surface">
      <SectionHeading title={title} subtitle="Selected reporting window" />
      <div className="overflow-x-auto">
        <RecordTable variant="compact" className="w-full text-left text-xs">
          <thead className="border-y bg-muted/40 text-muted-foreground">
            <tr>
              {headers.map((x, i) => (
                <th key={x} className={`px-4 py-2 ${i ? "text-right" : ""}`}>
                  {x}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.join("-")} className="border-b last:border-0">
                {row.map((x, i) => (
                  <td
                    key={`${i}-${x}`}
                    className={`px-4 py-2.5 ${i ? "text-right tabular-nums" : "font-medium capitalize"}`}
                  >
                    {x}
                  </td>
                ))}
              </tr>
            ))}
            {rows.length === 0 ? (
              <tr>
                <td
                  colSpan={headers.length}
                  className="px-4 py-6 text-center text-muted-foreground"
                >
                  No records in this window.
                </td>
              </tr>
            ) : null}
          </tbody>
        </RecordTable>
      </div>
    </section>
  );
}
function DashboardSkeleton() {
  return (
    <section aria-label="Loading reports" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <div key={i} className="h-28 animate-pulse rounded-xl border bg-muted/40" />
      ))}
    </section>
  );
}
function StateMessage({ title, detail }: { title: string; detail: string }) {
  return (
    <section className="rounded-xl border border-danger/30 bg-danger/5 p-6">
      <h2 className="font-semibold">{title}</h2>
      <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
    </section>
  );
}
