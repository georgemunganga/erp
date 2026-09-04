import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Info, Pencil, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { employeeProfileApi } from "@/mock/employeeprofile";
import { branchOptions, departmentOptions, gradeOptions, workLocationOptions } from "@/mock/reference";
import { EMPLOYMENT_TYPES } from "@/mock/types";
import { api } from "@/mock/service";
import { ApiError } from "@/platform/api-client";
import { adaptWorkerProfile, adaptWorkers, realApi, useApi } from "@/platform/use-api";
import type { Employee } from "@/mock/types";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { EditPage } from "@/platform/components/EditPage";
import type { EditSection } from "@/platform/components/EditPage";
import { LoadingState, RestrictedState } from "@/platform/components/States";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { SubRecordCard, SubRecords } from "@/platform/components/ProfileFields";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/employees/$id/edit")({
  head: () => ({
    meta: [
      { title: "Edit employee — Mightyfin HRMS" },
      { name: "description", content: "Edit an employee record: personal details, job and grade, where they work, and the pay details payroll relies on." },
      { property: "og:title", content: "Edit employee — Mightyfin HRMS" },
      { property: "og:description", content: "Edit personal details, job, location and pay details on an employee record." },
    ],
  }),
  component: EditEmployee,
});


/** Zambian NRC: six digits, two digits, one digit — 123456/78/9. */
const NRC = /^\d{6}\/\d{2}\/\d$/;
const SALUTATIONS = ["Mr", "Mrs", "Ms", "Dr", "Prof"];
const GENDERS = ["Female", "Male", "Prefer not to say"];
const MARITAL = ["Single", "Married", "Divorced", "Widowed", "Prefer not to say"];
const BANKS = ["Zanaco", "Stanbic", "FNB", "Absa", "Indo Zambia", "Access Bank"];
const PAYMENT_METHODS = ["Bank transfer", "Mobile money", "Cash", "Paid through accounts payable, not payroll"];
const BLOOD_GROUPS = ["A+", "A−", "B+", "B−", "AB+", "AB−", "O+", "O−"];
const RELATIONSHIPS = ["Spouse", "Parent", "Sibling", "Child", "Friend", "Other"];
const SHIFTS = ["Day shift, Monday to Friday", "Rotating shift", "Night shift", "Site roster — 14 on 7 off"];

function paymentMethodKey(value: string) {
  const normalized = value.toLowerCase().replace(/_/g, "-").trim();
  if (normalized === "bank" || normalized === "bank transfer") return "bank";
  if (normalized === "mobile-money" || normalized === "mobile money") return "mobile-money";
  if (normalized === "cash") return "cash";
  if (normalized === "accounts-payable" || normalized.startsWith("paid through accounts payable")) return "accounts-payable";
  return normalized || "bank";
}

function paymentMethodLabel(value: string | undefined) {
  const key = paymentMethodKey(value ?? "");
  if (key === "bank") return "Bank transfer";
  if (key === "mobile-money") return "Mobile money";
  if (key === "cash") return "Cash";
  if (key === "accounts-payable") return "Paid through accounts payable, not payroll";
  return value || "Bank transfer";
}

function EditEmployee() {
  const { id } = Route.useParams();
  const navigate = useNavigate();
  // Real backend: the employee list carries the same shape as the mock
  // employee, so one hook covers both modes (real falls back to mock shape
  // after adaptation when switched on).
  const state = useApi(async () => {
    if (!USE_REAL) return api.employee(id);
    try {
      const direct = await realApi.worker(id);
      return adaptWorkers([direct])[0] ?? null;
    } catch {
      const workers = adaptWorkers(await realApi.employees());
      return workers.find((w) => w.id === id || w.employeeNo === id) ?? null;
    }
  }, [id]);
  const profileState = useApi(
    () => (USE_REAL ? realApi.worker(id).then(adaptWorkerProfile) : employeeProfileApi.profile(id)),
    [id],
  );

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={4}>
        {(employee) => {
          if (!employee) return <RestrictedState />;
          // The form seeds its state once, on mount, so both records must be
          // in hand before it renders — otherwise it opens with blank fields.
          if (profileState.loading) return <LoadingState rows={4} />;
          const pr = profileState.data;

          // Edit mode: a field that the seeded record has never had cannot be
          // required — otherwise editing an existing record is blocked by the
          // same validators used for a brand-new hire. Keep the validators
          // for correctness when a value is entered, but relax "required"
          // on fields that are blank in the current record.
          const sections: EditSection[] = [
            {
              id: "identity",
              title: "Identity",
              description: "As it appears on the NRC. Payroll and the bank both check the legal name against it.",
              fields: [
                { name: "salutation", label: "Salutation", type: "select", options: SALUTATIONS, required: true },
                { name: "fullName", label: "Full legal name", required: true, hint: "Must match the NRC." },
                { name: "preferredName", label: "Preferred name", hint: "Used everywhere except legal documents." },
                { name: "gender", label: "Gender", type: "select", options: GENDERS, required: true },
                { name: "maritalStatus", label: "Marital status", type: "select", options: MARITAL, required: true },
                { name: "nationality", label: "Nationality", required: true },
                { name: "homeTown", label: "Home town" },
                {
                  name: "dateOfBirth",
                  label: "Date of birth",
                  type: "date",
                  required: !!pr?.dateOfBirth,
                  validate: (v) => {
                    if (!v) return null;
                    const age = (Date.now() - new Date(v).getTime()) / 31_557_600_000;
                    if (age < 15) return "Below the minimum working age in Zambia.";
                    if (age > 75) return "Check this date — it gives an age over 75.";
                    return null;
                  },
                },
                {
                  name: "nationalId",
                  label: "NRC number",
                  required: !!employee.nationalId,
                  hint: "Format 123456/78/9.",
                  validate: (v) => (v && !NRC.test(v) ? "An NRC looks like 123456/78/9." : null),
                },
                { name: "passportNo", label: "Passport number" },
                {
                  name: "passportExpiry",
                  label: "Passport expires",
                  type: "date",
                  validate: (v, all) =>
                    v && !all.passportNo ? "Give the passport number as well, or clear this date." : null,
                },
              ],
            },
            {
              id: "contact",
              title: "Contact",
              description: "Where to reach this person, at work and outside it.",
              fields: [
                {
                  name: "email",
                  label: "Work email",
                  required: !!employee.email,
                  validate: (v) => (v && !v.includes("@") ? "Enter a complete email address." : null),
                },
                {
                  name: "personalEmail",
                  label: "Personal email",
                  hint: "Used for payslips after someone leaves, when the work address is closed.",
                  validate: (v) => (v && !v.includes("@") ? "Enter a complete email address." : null),
                },
                {
                  name: "phone",
                  label: "Mobile",
                  required: !!employee.phone,
                  validate: (v) => (v && !v.startsWith("+260") ? "Use the full international format, starting +260." : null),
                },
                { name: "alternatePhone", label: "Alternate phone" },
                { name: "residentialAddress", label: "Residential address", type: "textarea", required: !!pr?.residentialAddress },
                { name: "postalAddress", label: "Postal address", type: "textarea" },
              ],
            },
            {
              id: "kin",
              title: "Next of kin",
              description: "Who is called first if something happens at work. This should never be blank.",
              fields: [
                { name: "emergencyName", label: "Name", required: !!pr?.emergency[0]?.name },
                { name: "emergencyRelationship", label: "Relationship", type: "select", options: RELATIONSHIPS, required: false },
                {
                  name: "emergencyPhone",
                  label: "Phone",
                  required: !!pr?.emergency[0]?.phone,
                  validate: (v, all) =>
                    v && v === all.phone
                      ? "This is the employee's own number. An emergency contact has to be someone else."
                      : null,
                },
              ],
            },
            {
              id: "job",
              title: "Job and grade",
              description: "Grade drives the pay range, so a grade change is a pay change and goes for approval.",
              fields: [
                { name: "jobTitle", label: "Job title", required: true },
                { name: "department", label: "Department", type: "select", options: departmentOptions, required: true },
                { name: "grade", label: "Grade", type: "select", options: gradeOptions, required: !!employee.grade },
                { name: "employmentType", label: "Employment type", type: "select", options: [...EMPLOYMENT_TYPES], required: true },
                {
                  name: "startDate",
                  label: "Employment start date",
                  type: "date",
                  required: true,
                  hint: "Used for payroll proration when the employee starts partway through a pay period.",
                },
                { name: "reportsTo", label: "Reports to", required: !!pr?.reportsTo },
                { name: "costCentre", label: "Cost centre", required: !!pr?.costCentre, hint: "Where this person's cost lands in the ledger." },
                {
                  name: "noticePeriodDays",
                  label: "Notice period (days)",
                  type: "number",
                  required: true,
                  validate: (v) =>
                    v && Number(v) < 30 ? "Zambian law requires at least 30 days for a permanent contract." : null,
                },
                { name: "probationEndsOn", label: "Probation ends", type: "date" },
              ],
            },
            {
              id: "place",
              title: "Where they work",
              description: "Branch decides the calendar and the public holidays that count as non-working days.",
              fields: [
                { name: "branch", label: "Branch", type: "select", options: branchOptions, required: !!employee.branch },
                { name: "location", label: "Work location", type: "select", options: workLocationOptions, required: !!employee.location },
                { name: "shiftPattern", label: "Shift pattern", type: "select", options: SHIFTS, required: !!pr?.shiftPattern },
                { name: "holidayCalendar", label: "Holiday calendar", required: !!pr?.holidayCalendar },
                { name: "leavePolicy", label: "Leave policy", required: !!pr?.leavePolicy },
                { name: "attendanceDeviceId", label: "Attendance device ID" },
              ],
            },
            {
              id: "pay",
              title: "Pay and bank",
              description: "Preferred payment method and payout details for this employee. Payroll reads the primary record directly.",
              fields: [
                { name: "payGroup", label: "Pay group", required: !!pr?.payGroup },
                { name: "paymentMethod", label: "Preferred payment method", type: "select", options: PAYMENT_METHODS, required: true },
                {
                  name: "accountName",
                  label: "Account holder name",
                  hint: "For bank or mobile-money payouts, this should match the employee's registered account name.",
                  validate: (v, all) =>
                    ["bank", "mobile-money"].includes(paymentMethodKey(all.paymentMethod)) && !v
                      ? "Account holder name is required for this payment method."
                      : null,
                },
                {
                  name: "bankName",
                  label: "Bank",
                  type: "select",
                  options: BANKS,
                  validate: (v, all) =>
                    paymentMethodKey(all.paymentMethod) === "bank" && !v
                      ? "Bank is required for bank transfer."
                      : null,
                },
                {
                  name: "bankBranch",
                  label: "Branch / branch code",
                  validate: (v, all) =>
                    paymentMethodKey(all.paymentMethod) === "bank" && !v
                      ? "Branch code is required for bank transfer."
                      : null,
                },
                {
                  name: "bankAccount",
                  label: "Account number",
                  hint: "Verified against the bank before the next payment run.",
                  validate: (v, all) => {
                    if (paymentMethodKey(all.paymentMethod) !== "bank") return null;
                    if (!v) return "Account number is required for bank transfer.";
                    const digits = v.replace(/\D/g, "");
                    return digits.length < 6
                      ? "Enter the account number shown on the bank letter. Account-number length varies by bank."
                      : null;
                  },
                },
                {
                  name: "mobileMoneyNumber",
                  label: "Mobile money number",
                  hint: "Use the full mobile number registered for payout.",
                  validate: (v, all) => {
                    if (paymentMethodKey(all.paymentMethod) !== "mobile-money") return null;
                    if (!v) return "Mobile money number is required for mobile-money payout.";
                    return v.replace(/\s/g, "").length < 9 ? "Enter a complete mobile money number." : null;
                  },
                },
              ],
            },
            {
              id: "statutory",
              title: "Statutory registrations",
              description: "A missing NAPSA or NHIMA number stops this employee being included in a pay run.",
              fields: [
                { name: "tpin", label: "TPIN (ZRA)", required: !!pr?.tpin },
                { name: "napsaNumber", label: "NAPSA number", required: !!pr?.napsaNumber },
                { name: "nhimaNumber", label: "NHIMA number", required: !!pr?.nhimaNumber },
              ],
            },
            {
              id: "support",
              title: "Support and adjustments",
              description: "Recorded only where it changes how someone is supported at work.",
              fields: [
                { name: "bloodGroup", label: "Blood group", type: "select", options: BLOOD_GROUPS },
                { name: "workplaceAdjustments", label: "Workplace adjustments", type: "textarea" },
                { name: "dietaryRequirements", label: "Dietary requirements" },
              ],
            },
            {
              id: "effective",
              title: "When it takes effect",
              description: "A record is a history, not a current state. Every change is dated so a past payslip stays reproducible.",
              fields: [
                {
                  name: "effectiveFrom",
                  label: "Effective from",
                  type: "date",
                  required: true,
                  validate: (v) =>
                    v && v < "2026-08-01"
                      ? "July 2026 payroll is already released, so a change cannot start before 1 August."
                      : null,
                },
                {
                  name: "reason",
                  label: "Reason for the change",
                  type: "textarea",
                  hint: "Whoever approves this, and anyone auditing it later, reads this line.",
                },
              ],
            },
          ];
          const liveFields = new Set([
            "fullName", "preferredName", "nationality", "dateOfBirth", "nationalId",
            "passportNo", "email", "phone", "jobTitle", "grade", "tpin",
            "napsaNumber", "nhimaNumber", "startDate", "paymentMethod", "accountName", "bankName",
            "bankBranch", "bankAccount", "mobileMoneyNumber",
          ]);
          const visibleSections = USE_REAL
            ? sections
                .map((section) => ({
                  ...section,
                  description: section.render
                    ? section.description
                    : "These fields are stored on the live employee record.",
                  fields: section.fields?.filter((field) => liveFields.has(field.name)),
                }))
                .filter((section) => section.render || section.fields?.length)
            : sections;

          // The route id is the mock employee id (`w-1001`) in demo mode but the
          // real worker guid in production — `adaptWorkers` keeps the real id on
          // the `id` field, so fall back to the route id in mock mode.
          const workerId = employee.id ?? id;

          return (
            <EditPage
              title={employee.fullName}
              reference={employee.employeeNo}
              description={USE_REAL ? "Only fields backed by the live employee record are editable here." : "Changes are dated and go into the employee's history. Anything affecting pay is approved before it reaches a run."}
              sections={[
                ...visibleSections,
                ...(USE_REAL
                  ? [
                      {
                        id: "history",
                        title: "Employee history",
                        description: "Education and previous employers. Every change here is written straight to the live record, with one person to undo it.",
                        render: () => (
                          <HistorySection workerId={workerId} />
                        ),
                      },
                    ]
                  : [
                      {
                        id: "history",
                        title: "Employee history",
                        description: "Education, previous employers and moves within this organisation.",
                        render: () => (
                          <p className="text-sm text-muted-foreground">History is recorded in the live build of this screen. Nothing reaches payroll until a change is approved.</p>
                        ),
                      },
                    ]),
              ] as EditSection[]}
              initial={{
                salutation: pr?.salutation ?? "Mr",
                fullName: employee.fullName,
                preferredName: employee.preferredName ?? "",
                gender: pr?.gender ?? "Prefer not to say",
                maritalStatus: pr?.maritalStatus ?? "Single",
                nationality: pr?.nationality ?? "Zambian",
                homeTown: pr?.homeTown ?? "",
                dateOfBirth: pr?.dateOfBirth ?? "",
                nationalId: employee.nationalId,
                passportNo: pr?.passportNo ?? "",
                passportExpiry: pr?.passportExpiry ?? "",

                email: employee.email ?? "",
                personalEmail: pr?.personalEmail ?? "",
                phone: employee.phone ?? "",
                alternatePhone: pr?.alternatePhone ?? "",
                residentialAddress: pr?.residentialAddress ?? "",
                postalAddress: pr?.postalAddress ?? "",

                emergencyName: pr?.emergency[0]?.name ?? "",
                emergencyRelationship: pr?.emergency[0]?.relationship ?? "Spouse",
                emergencyPhone: pr?.emergency[0]?.phone ?? "",

                jobTitle: employee.jobTitle,
                department: employee.department,
                grade: employee.grade,
                employmentType: employee.employmentType,
                startDate: employee.startDate ?? "",
                reportsTo: pr?.reportsTo ?? "",
                costCentre: pr?.costCentre ?? "",
                noticePeriodDays: String(pr?.noticePeriodDays ?? 30),
                probationEndsOn: pr?.probationEndsOn ?? "",

                branch: employee.branch,
                location: employee.location,
                shiftPattern: pr?.shiftPattern ?? SHIFTS[0],
                holidayCalendar: pr?.holidayCalendar ?? "",
                leavePolicy: pr?.leavePolicy ?? "",
                attendanceDeviceId: pr?.attendanceDeviceId ?? "",

                payGroup: pr?.payGroup ?? "",
                paymentMethod: paymentMethodLabel(pr?.paymentMethod),
                accountName: pr?.accountName ?? employee.fullName,
                bankName: pr?.bankName ?? "",
                bankBranch: pr?.bankBranch ?? "",
                bankAccount: pr?.bankAccount ?? employee.bankAccount ?? "",
                mobileMoneyNumber: pr?.mobileMoneyNumber ?? "",

                tpin: pr?.tpin ?? "",
                napsaNumber: pr?.napsaNumber ?? "",
                nhimaNumber: pr?.nhimaNumber ?? "",

                bloodGroup: pr?.bloodGroup ?? "",
                workplaceAdjustments: pr?.workplaceAdjustments ?? "",
                dietaryRequirements: pr?.dietaryRequirements ?? "",

                effectiveFrom: "2026-09-01",
                reason: "",
              }}
              saveLabel="Save the change"
              footerNote={USE_REAL ? "Saved changes are written to the live HRM employee record and audited by the API." : "Nothing reaches payroll until the change is approved."}
              extraChanges={[]}
              onCancel={() => navigate({ to: "/hrm/employees/$id", params: { id } })}
              onSave={async (values, changed) => {
                if (USE_REAL) {
                  const body: Record<string, unknown> = {};
                  if (changed.includes("fullName")) {
                    const parts = values.fullName.trim().split(/\s+/);
                    body.firstName = parts[0] ?? "";
                    body.middleName = parts.slice(1, parts.length - 1).join(" ") || null;
                    body.lastName = parts[parts.length - 1] ?? "";
                  }
                  if (changed.includes("email")) body.email = values.email || null;
                  if (changed.includes("phone")) body.phone = values.phone || null;
                  if (changed.includes("preferredName")) body.preferredName = values.preferredName || null;
                  if (changed.includes("nationalId")) body.nrc = values.nationalId || null;
                  if (changed.includes("dateOfBirth")) body.dateOfBirth = values.dateOfBirth || null;
                  if (changed.includes("passportNo")) body.passportNo = values.passportNo || null;
                  if (changed.includes("nationality")) body.nationality = values.nationality || null;
                  if (changed.includes("tpin")) body.tpin = values.tpin || null;
                  if (changed.includes("napsaNumber")) body.napsaNumber = values.napsaNumber || null;
                  if (changed.includes("nhimaNumber")) body.nhimaNumber = values.nhimaNumber || null;
                  if (changed.includes("jobTitle")) body.jobTitle = values.jobTitle || null;
                  if (changed.includes("grade")) body.grade = values.grade || null;
                  if (changed.includes("startDate")) body.startDate = values.startDate || null;
                  if (changed.includes("employmentType"))
                    body.workerType =
                      values.employmentType === "Contractor"
                        ? "contractor"
                        : values.employmentType === "Intern"
                          ? "intern"
                          : "employee";
                  const paymentChanged = changed.some((field) =>
                    ["paymentMethod", "accountName", "bankName", "bankBranch", "bankAccount", "mobileMoneyNumber"].includes(field),
                  );
                  try {
                    if (Object.keys(body).length) {
                      await realApi.updateWorker(id, body);
                    }
                    if (paymentChanged) {
                      const method = paymentMethodKey(values.paymentMethod);
                      const paymentBody = {
                        paymentMethod: method,
                        accountName: values.accountName || employee.fullName,
                        bankName: method === "bank" ? values.bankName : method === "mobile-money" ? "Mobile money" : method === "cash" ? "Cash" : "Accounts payable",
                        branchCode: method === "bank" ? values.bankBranch : "N/A",
                        accountNumber: method === "bank" ? values.bankAccount : method === "mobile-money" ? values.mobileMoneyNumber : "N/A",
                        mobileMoneyNumber: method === "mobile-money" ? values.mobileMoneyNumber : null,
                        isPrimary: true,
                      };
                      if (pr?.bankDetailId) {
                        await realApi.updateBankDetails(id, pr.bankDetailId, paymentBody);
                      } else {
                        await realApi.addBankDetails(id, paymentBody);
                      }
                    }
                    feedback.saved(`${employee.fullName} updated in the live HRM record.`);
                    navigate({ to: "/hrm/employees/$id", params: { id } });
                    return;
                  } catch (err) {
                    const msg = err instanceof ApiError ? err.message : String(err);
                    feedback.blocked("The change could not be saved.", msg);
                    return;
                  }
                }
                const paySensitive = changed.filter((c) =>
                  [
                    "grade",
                    "bankAccount",
                    "bankName",
                    "paymentMethod",
                    "employmentType",
                    "fullName",
                    "payGroup",
                    "tpin",
                    "napsaNumber",
                    "nhimaNumber",
                  ].includes(c),
                );
                if (paySensitive.length) {
                  feedback.submitted(
                    `${changed.length} change${changed.length === 1 ? "" : "s"} sent for approval.`,
                    `${paySensitive.length} of them ${
                      paySensitive.length === 1 ? "affects" : "affect"
                    } pay, so a second person signs off before payroll sees it. Effective ${values.effectiveFrom}.`,
                  );
                } else {
                  feedback.saved(
                    `${employee.fullName} updated, effective ${values.effectiveFrom}.`,
                    () => feedback.note("Change reverted."),
                  );
                }
                navigate({ to: "/hrm/employees/$id", params: { id } });
              }}
            />
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}

/**
 * M33: live CRUD for the three worker-history collections.
 *
 * The rule these sections encode: a history record is append-only in spirit,
 * so adding is the loudest action, editing quietly fixes a typo, and deleting
 * is the only thing that needs a second thought — the dialog states the
 * consequence ("gone from payroll audits too") instead of asking "are you sure?".
 * Everything saves straight to the live record; the one visible Undo reverses
 * the last add.
 */
type HistoryRow = Record<string, unknown>;

type HistoryKind = "education" | "external-work-history" | "internal-work-history";

const HISTORY_HINT: Record<HistoryKind, string> = {
  education: "Give the institution and qualification first.",
  "external-work-history": "Give the company and role first.",
  "internal-work-history": "Give the department and role first.",
};

/** Inline add/edit form shown per history section. */
function HistoryForm({
  kind,
  row,
  onSubmit,
  onDiscard,
}: {
  kind: HistoryKind;
  row: HistoryRow;
  onSubmit: () => void;
  onDiscard: () => void;
}) {
  // The loudest fields a record cannot exist without. Everything else is
  // optional detail the server fills with blanks.
  const required =
    kind === "education"
      ? ["institution", "qualification"]
      : kind === "external-work-history"
        ? ["company", "role"]
        : ["orgUnitName", "role"];
  // The form is uncontrolled, so requiredness is derived from the live DOM
  // whenever any input changes. A mount-time tick picks up values that were
  // placed in the DOM before React rendered (e.g. programmatic fills).
  const [, setTick] = useState(0);
  useEffect(() => {
    setTick((t) => t + 1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const missingRequired = required.some(
    (name) => !String(document.getElementById(historyFieldId(kind, name))?.value ?? row[name] ?? "").trim(),
  );
  // DOM ids are stable and unique across all three history forms — save()
  // reads the live DOM values (manual typing and programmatic fills both land
  // in the DOM, even when React state never synchronises).
  const fieldId = (name: string) => historyFieldId(kind, name);
  // Uncontrolled inputs read from refs at submit time. A controlled `value`
  // prop lets React re-renders win over programmatic/native edits — switching
  // to `defaultValue` guarantees what the user types (or automation fills) is
  // exactly what gets saved.
  return (
    <div className="mt-2 rounded-md border bg-surface-muted p-3">
      {historyFields(kind).map((f) => (
        <div key={f.name} className="mt-2 first:mt-0">
          <label htmlFor={fieldId(f.name)} className="text-xs text-muted-foreground">
            {f.label}
          </label>
          <Input
            id={fieldId(f.name)}
            type={f.type ?? "text"}
            defaultValue={row.id ? String(row[f.name] ?? "") : undefined}
            onChange={() => setTick((t) => t + 1)}
          />
        </div>
      ))}
      <div className="mt-3 flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onDiscard}>
          Cancel
        </Button>
        <Button
          size="sm"
          onClick={onSubmit}
          disabled={missingRequired}
          title={missingRequired ? HISTORY_HINT[kind] : undefined}
        >
          {row.id ? "Update" : "Add"}
        </Button>
      </div>
    </div>
  );
}

/** Stable DOM ids for a history form field — unique across all three kinds. */
function historyFieldId(kind: HistoryKind, name: string) {
  return kind === "education"
    ? name === "grade"
      ? "educationGrade"
      : `education-${name}`
    : kind === "external-work-history"
      ? `ext-${name}`
      : `int-${name}`;
}

/** The fields a worker-history record owns — one table per kind. */
function historyFields(kind: HistoryKind) {
  if (kind === "education") {
    return [
      { name: "institution", label: "Institution", type: "text" },
      { name: "qualification", label: "Qualification", type: "text" },
      { name: "fieldOfStudy", label: "Field of study", type: "text" },
      { name: "grade", label: "Grade", type: "text" },
      { name: "startYear", label: "Start year", type: "text" },
      { name: "endYear", label: "End year", type: "text" },
    ];
  }
  if (kind === "external-work-history") {
    return [
      { name: "company", label: "Company", type: "text" },
      { name: "role", label: "Role", type: "text" },
      { name: "startDate", label: "Start date", type: "text", hint: "YYYY-MM-DD" },
      { name: "endDate", label: "End date", type: "text", hint: "YYYY-MM-DD" },
      { name: "responsibilities", label: "Responsibilities", type: "text" },
    ];
  }
  return [
    { name: "orgUnitName", label: "Department / branch", type: "text" },
    { name: "role", label: "Role", type: "text" },
    { name: "grade", label: "Grade", type: "text" },
    { name: "startDate", label: "Start date", type: "text", hint: "YYYY-MM-DD" },
    { name: "endDate", label: "End date", type: "text", hint: "YYYY-MM-DD" },
    { name: "reason", label: "Reason for the move", type: "text" },
  ];
}

function HistorySection({ workerId }: { workerId: string }) {
  const [education, setEducation] = useState<HistoryRow[]>([]);
  const [external, setExternal] = useState<HistoryRow[]>([]);
  const [internal, setInternal] = useState<HistoryRow[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ kind: HistoryKind; row: HistoryRow } | null>(null);
  const [adding, setAdding] = useState<HistoryKind | null>(null);
  const [deleting, setDeleting] = useState<{ kind: HistoryKind; row: HistoryRow } | null>(null);
  // A stable row that survives re-renders while the inline form is typed into.
  const formRowRef = useRef<Record<HistoryKind, HistoryRow>>({
    education: {},
    "external-work-history": {},
    "internal-work-history": {},
  });

  async function refresh() {
    const [ed, ext, inn] = await Promise.all([
      realApi.education(workerId).catch(() => []),
      realApi.externalWorkHistory(workerId).catch(() => []),
      realApi.internalWorkHistory(workerId).catch(() => []),
    ]);
    setEducation(Array.isArray(ed) ? ed : []);
    setExternal(Array.isArray(ext) ? ext : []);
    setInternal(Array.isArray(inn) ? inn : []);
  }

  useEffect(() => {
    let alive = true;
    Promise.all([
      realApi.education(workerId).catch(() => []),
      realApi.externalWorkHistory(workerId).catch(() => []),
      realApi.internalWorkHistory(workerId).catch(() => []),
    ])
      .then(([ed, ext, inn]) => {
        if (!alive) return;
        setEducation(Array.isArray(ed) ? ed : []);
        setExternal(Array.isArray(ext) ? ext : []);
        setInternal(Array.isArray(inn) ? inn : []);
      })
      .catch((err) => {
        if (alive) setLoadError(err instanceof ApiError ? err.message : String(err));
      });
    return () => {
      alive = false;
    };
  }, [workerId]);

  async function save(kind: HistoryKind, row: HistoryRow) {
    // Re-read the live DOM values so manual typing and programmatic fills
    // are both captured — React-controlled state is not used for history
    // forms (uncontrolled inputs, defaultValue only).
    for (const f of historyFields(kind)) {
      const el = document.getElementById(historyFieldId(kind, f.name));
      if (el && "value" in el) row[f.name] = String((el as HTMLInputElement).value);
    }
    const body: Record<string, unknown> = {};
    for (const f of historyFields(kind)) {
      const value = (row[f.name] ?? "").toString().trim();
      if (value) {
        if (f.name === "startYear" || f.name === "endYear") {
          const year = Number(value);
          if (!Number.isFinite(year)) continue;
          body[f.name] = year;
        } else body[f.name] = value;
      } else body[f.name] = null;
    }
    try {
      if (row.id) {
        if (kind === "education") await realApi.updateEducation(workerId, String(row.id), body);
        else if (kind === "external-work-history")
          await realApi.updateExternalWorkHistory(workerId, String(row.id), body);
        else await realApi.updateInternalWorkHistory(workerId, String(row.id), body);
        await refresh();
        feedback.saved("History record updated on the live HRM record.");
      } else {
        const created =
          kind === "education"
            ? await realApi.addEducation(workerId, body)
            : kind === "external-work-history"
              ? await realApi.addExternalWorkHistory(workerId, body)
              : await realApi.addInternalWorkHistory(workerId, body);
        row.id = String((created as Record<string, unknown>).id ?? "");
        await refresh();
        feedback.saved(
          "History record added to the live HRM record.",
          async () => {
            if (!row.id) return;
            try {
              if (kind === "education") await realApi.removeEducation(workerId, row.id);
              else if (kind === "external-work-history")
                await realApi.removeExternalWorkHistory(workerId, row.id);
              else await realApi.removeInternalWorkHistory(workerId, row.id);
              setEducation((s) => s.filter((r) => r.id !== row.id));
              setExternal((s) => s.filter((r) => r.id !== row.id));
              setInternal((s) => s.filter((r) => r.id !== row.id));
              feedback.note("The record was removed again.");
            } catch {
              feedback.blocked("The undo failed — the record is still saved.");
            }
          },
        );
      }
      setEditing(null);
      setAdding(null);
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : String(err);
      await refresh();
      feedback.blocked("The history record could not be saved.", msg);
    }
  }

  async function remove(kind: HistoryKind, row: HistoryRow) {
    try {
      if (kind === "education") await realApi.removeEducation(workerId, String(row.id));
      else if (kind === "external-work-history")
        await realApi.removeExternalWorkHistory(workerId, String(row.id));
      else await realApi.removeInternalWorkHistory(workerId, String(row.id));
      await refresh();
      feedback.removed("History record removed from the live HRM record.");
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : String(err);
      feedback.blocked("The record could not be removed.", msg);
    }
    setDeleting(null);
  }

  function yearRange(row: HistoryRow) {
    const s = row.startYear && Number(row.startYear) ? String(row.startYear) : "";
    const e = row.endYear && Number(row.endYear) ? String(row.endYear) : "";
    return s || e ? `${s}–${e}`.replace(/^–|–$/, "") : "";
  }

  return (
    <div className="space-y-6">
      {loadError ? (
        <p role="alert" className="rounded-md border border-danger/30 bg-danger-soft p-3 text-sm text-danger">
          <AlertTriangle className="mr-1.5 inline size-3.5" aria-hidden />
          {loadError}
        </p>
      ) : null}

      {/* Education */}
      <div>
        <div className="flex items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold">Education</h3>
          <Button
            size="sm"
            variant="outline"
            className="gap-1"
            disabled={editing !== null || adding !== null}
            onClick={() => {
              formRowRef.current.education = {};
              setAdding("education");
              setEditing(null);
            }}
          >
            <Plus className="size-3.5" aria-hidden /> Add
          </Button>
        </div>
        {adding === "education" || (editing?.kind === "education") ? (
          <HistoryForm
            kind="education"
            row={editing?.kind === "education" ? editing.row : formRowRef.current.education}
            onSubmit={() => save("education", editing?.kind === "education" ? editing.row : formRowRef.current.education)}
            onDiscard={() => {
              setAdding(null);
              setEditing(null);
            }}
          />
        ) : (
          <SubRecords
            items={education}
            empty="No qualifications recorded yet."
            render={(ed) => (
              <SubRecordCard
                title={String(ed.qualification ?? "Qualification not recorded")}
                meta={yearRange(ed)}
              >
                {ed.institution ? String(ed.institution) : "Institution not recorded"}
                {ed.fieldOfStudy ? ` · ${ed.fieldOfStudy}` : ""}
                {ed.grade ? ` · ${ed.grade}` : ""}
                <span className="ml-3">
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 text-info hover:underline"
                    onClick={() => {
                      setEditing({ kind: "education", row: { ...ed } });
                      setAdding(null);
                    }}
                  >
                    <Pencil className="size-3" aria-hidden /> Edit
                  </button>
                  <button
                    type="button"
                    className="ml-3 inline-flex items-center gap-1 text-danger hover:underline"
                    onClick={() => setDeleting({ kind: "education", row: ed })}
                  >
                    <Trash2 className="size-3" aria-hidden /> Remove
                  </button>
                </span>
              </SubRecordCard>
            )}
          />
        )}
      </div>

      {/* External work history */}
      <div>
        <div className="flex items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold">Previous employers</h3>
          <Button
            size="sm"
            variant="outline"
            className="gap-1"
            disabled={editing !== null || adding !== null}
            onClick={() => {
              formRowRef.current["external-work-history"] = {};
              setAdding("external-work-history");
              setEditing(null);
            }}
          >
            <Plus className="size-3.5" aria-hidden /> Add
          </Button>
        </div>
        {adding === "external-work-history" || editing?.kind === "external-work-history" ? (
          <HistoryForm
            kind="external-work-history"
            row={editing?.kind === "external-work-history" ? editing.row : formRowRef.current["external-work-history"]}
            onSubmit={() =>
              save(
                "external-work-history",
                editing?.kind === "external-work-history"
                  ? editing.row
                  : formRowRef.current["external-work-history"],
              )
            }
            onDiscard={() => {
              setAdding(null);
              setEditing(null);
            }}
          />
        ) : (
          <SubRecords
            items={external}
            empty="No previous employers recorded yet."
            render={(ex) => (
              <SubRecordCard
                title={`${String(ex.role ?? "Role not recorded")} — ${String(ex.company ?? "Company not recorded")}`}
                meta={
                  ex.startDate || ex.endDate
                    ? `${ex.startDate ?? "—"} to ${ex.endDate ?? "present"}`
                    : undefined
                }
              >
                {ex.responsibilities ? String(ex.responsibilities) : "No responsibilities recorded"}
                <span className="ml-3">
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 text-info hover:underline"
                    onClick={() => {
                      setEditing({ kind: "external-work-history", row: { ...ex } });
                      setAdding(null);
                    }}
                  >
                    <Pencil className="size-3" aria-hidden /> Edit
                  </button>
                  <button
                    type="button"
                    className="ml-3 inline-flex items-center gap-1 text-danger hover:underline"
                    onClick={() => setDeleting({ kind: "external-work-history", row: ex })}
                  >
                    <Trash2 className="size-3" aria-hidden /> Remove
                  </button>
                </span>
              </SubRecordCard>
            )}
          />
        )}
      </div>

      {/* Internal work history */}
      <div>
        <div className="flex items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold">Moves within this organisation</h3>
          <Button
            size="sm"
            variant="outline"
            className="gap-1"
            disabled={editing !== null || adding !== null}
            onClick={() => {
              formRowRef.current["internal-work-history"] = {};
              setAdding("internal-work-history");
              setEditing(null);
            }}
          >
            <Plus className="size-3.5" aria-hidden /> Add
          </Button>
        </div>
        {adding === "internal-work-history" || editing?.kind === "internal-work-history" ? (
          <HistoryForm
            kind="internal-work-history"
            row={editing?.kind === "internal-work-history" ? editing.row : formRowRef.current["internal-work-history"]}
            onSubmit={() =>
              save(
                "internal-work-history",
                editing?.kind === "internal-work-history"
                  ? editing.row
                  : formRowRef.current["internal-work-history"],
              )
            }
            onDiscard={() => {
              setAdding(null);
              setEditing(null);
            }}
          />
        ) : (
          <SubRecords
            items={internal}
            empty="No internal moves recorded yet."
            render={(inn) => (
              <SubRecordCard
                title={`${String(inn.role ?? "Role not recorded")} — ${String(inn.orgUnitName ?? "Department not recorded")}`}
                meta={
                  inn.startDate || inn.endDate
                    ? `${inn.startDate ?? "—"} to ${inn.endDate ?? "present"}`
                    : undefined
                }
              >
                {inn.grade ? `Grade ${inn.grade}` : "Grade not recorded"}
                {inn.reason ? ` · ${inn.reason}` : ""}
                <span className="ml-3">
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 text-info hover:underline"
                    onClick={() => {
                      setEditing({ kind: "internal-work-history", row: { ...inn } });
                      setAdding(null);
                    }}
                  >
                    <Pencil className="size-3" aria-hidden /> Edit
                  </button>
                  <button
                    type="button"
                    className="ml-3 inline-flex items-center gap-1 text-danger hover:underline"
                    onClick={() => setDeleting({ kind: "internal-work-history", row: inn })}
                  >
                    <Trash2 className="size-3" aria-hidden /> Remove
                  </button>
                </span>
              </SubRecordCard>
            )}
          />
        )}
      </div>

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(o) => !o && setDeleting(null)}
        title="Remove this history record?"
        consequence="It disappears from the employee's record, including any audit trail that showed it."
        confirmLabel="Remove it"
        destructive
        onConfirm={() => {
          if (deleting) remove(deleting.kind, deleting.row);
        }}
      />
    </div>
  );
}
