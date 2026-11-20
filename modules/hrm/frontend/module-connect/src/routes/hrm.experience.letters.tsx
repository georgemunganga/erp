import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Check, Clock, Download, FileText, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { employees, entities } from "@/mock/data";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { toast } from "sonner";

export const Route = createFileRoute("/hrm/experience/letters")({
  head: () => ({
    meta: [
      { title: "Letters — Kuubu HRM" },
      {
        name: "description",
        content:
          "Request an employment letter and see exactly what it will say before it is issued.",
      },
      { property: "og:title", content: "Letters — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Request an employment letter and see exactly what it will say before it is issued.",
      },
    ],
  }),
  component: LettersPage,
});

interface LetterType {
  id: string;
  name: string;
  purpose: string;
  /** Exactly what will be disclosed — shown before the employee commits. */
  discloses: string[];
  needsApproval: boolean;
  turnaround: string;
}

const letterTypes: LetterType[] = [
  {
    id: "employment",
    name: "Employment confirmation",
    purpose: "Confirms that you work here, your job title and your start date.",
    discloses: ["Full name", "Job title", "Start date", "Employment type"],
    needsApproval: false,
    turnaround: "Usually within 1 working day",
  },
  {
    id: "salary",
    name: "Salary confirmation",
    purpose: "Confirms employment plus your current salary. Often needed by banks and landlords.",
    discloses: ["Full name", "Job title", "Start date", "Gross annual salary", "Pay frequency"],
    needsApproval: true,
    turnaround: "Usually within 2 working days",
  },
  {
    id: "visa",
    name: "Visa support letter",
    purpose: "Supports a visa or travel permit application.",
    discloses: [
      "Full name",
      "Job title",
      "Start date",
      "Salary",
      "Travel dates",
      "Passport number",
    ],
    needsApproval: true,
    turnaround: "Usually within 3 working days",
  },
  {
    id: "service",
    name: "Service certificate",
    purpose: "Confirms your period of service. Issued on or after your last working day.",
    discloses: ["Full name", "Job title", "Start date", "End date", "Period of service"],
    needsApproval: false,
    turnaround: "Issued after your last working day",
  },
];

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
const letterStatus: Record<string, string> = {
  generated: "Approved",
  "pending-approval": "In review",
  pending: "In review",
  issued: "Approved",
  draft: "Draft",
  approved: "Approved",
  rejected: "Rejected",
};

interface IssuedLetter {
  id: string;
  type: string;
  addressee: string;
  requested: string;
  status: string;
  verification: string;
}

function adaptLetters(rows: unknown[]): IssuedLetter[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      id: String(r.id ?? ""),
      type: String(r.letterType ?? "Letter"),
      addressee: String(r.addressee ?? ""),
      requested: typeof r.createdAt === "string" ? String(r.createdAt).slice(0, 10) : "",
      status: letterStatus[String(r.status ?? "")] ?? String(r.status ?? "In review"),
      verification: String(r.verificationCode ?? ""),
    } satisfies IssuedLetter;
  });
}

const staticLetters: IssuedLetter[] = [
  {
    id: "LT-2026-0441",
    type: "Employment confirmation",
    addressee: "Rabobank — mortgage department",
    requested: "2026-07-18",
    status: "Approved",
    verification: "MF-4K7Q-22HD",
  },
  {
    id: "LT-2026-0402",
    type: "Salary confirmation",
    addressee: "Stichting Woningnet",
    requested: "2026-05-04",
    status: "Approved",
    verification: "MF-9P1X-08LM",
  },
];

function RequestFlow({ onDone }: { onDone: (ref: string) => void }) {
  const [typeId, setTypeId] = useState(letterTypes[0].id);
  const [addressee, setAddressee] = useState("");
  const type = letterTypes.find((t) => t.id === typeId)!;
  const me = employees[0];
  const employerName = entities.find((entity) => entity.id === me.entityId)?.name ?? "the employing legal entity";

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "What do you need the letter for?",
      purpose:
        "Each letter says something different. Picking the right one avoids a second request.",
      render: () => (
        <div className="max-w-xl space-y-3">
          {letterTypes.map((t) => (
            <label
              key={t.id}
              className={`flex cursor-pointer gap-3 rounded-lg border p-3 transition-colors ${
                typeId === t.id ? "border-primary bg-primary-soft" : "hover:bg-surface-muted"
              }`}
            >
              <input
                type="radio"
                name="letter-type"
                className="mt-1"
                checked={typeId === t.id}
                onChange={() => setTypeId(t.id)}
              />
              <span className="min-w-0">
                <span className="block text-sm font-medium">{t.name}</span>
                <span className="block text-xs text-muted-foreground">{t.purpose}</span>
              </span>
            </label>
          ))}
        </div>
      ),
    },
    {
      id: "disclosure",
      title: "What this letter will say",
      purpose:
        "Check what is disclosed before it goes anywhere. Nothing else about you is included.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <ul className="space-y-1.5">
            {type.discloses.map((d) => (
              <li key={d} className="flex items-center gap-2 text-sm">
                <Check className="size-4 shrink-0 text-success" aria-hidden />
                {d}
              </li>
            ))}
          </ul>
          <p className="rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
            Your bank details, national identifier, absence record and performance history are never
            included in a letter.
          </p>
        </div>
      ),
    },
    {
      id: "addressee",
      title: "Who is it addressed to?",
      purpose: "The letter names the recipient, which is what makes it verifiable by them.",
      render: () => (
        <div className="max-w-md">
          <Label htmlFor="addressee">Addressee</Label>
          <Input
            id="addressee"
            className="mt-1"
            value={addressee}
            onChange={(e) => setAddressee(e.target.value)}
            placeholder="e.g. Rabobank — mortgage department"
          />
          <p className="mt-1 text-xs text-muted-foreground">
            Use "To whom it may concern" if you do not have a specific recipient.
          </p>
          <div className="mt-4">
            <Label htmlFor="delivery">Delivery</Label>
            <Select defaultValue="download">
              <SelectTrigger id="delivery" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="download">Download it myself</SelectItem>
                <SelectItem value="email">Email it to me</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "A preview of the letter as it will be issued.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <div className="rounded-lg border bg-surface-muted p-4 text-sm leading-relaxed">
            <p className="font-medium">{addressee || "To whom it may concern"}</p>
            <p className="mt-3">
              This confirms that <span className="font-medium">{me.fullName}</span> ({me.employeeNo}
              ) is employed by {employerName} as{" "}
              <span className="font-medium">{me.jobTitle}</span>, on a{" "}
              {me.employmentType.toLowerCase()} basis, since {me.startDate}.
            </p>
            {type.discloses.includes("Gross annual salary") ? (
              <p className="mt-2">Gross annual salary: K57,600.00, paid monthly.</p>
            ) : null}
            <p className="mt-3 text-xs text-muted-foreground">
              Issued by HR operations. Verifiable using the code printed on the letter.
            </p>
          </div>
          <p className="text-xs text-muted-foreground">
            {type.needsApproval
              ? "This letter type needs HR approval before it is issued."
              : "This letter type is issued automatically — no approval needed."}{" "}
            {type.turnaround}.
          </p>
        </div>
      ),
    },
  ];

  return (
    <GuidedFlow
      flowId="letter-request"
      steps={steps}
      submitLabel="Request letter"
      onSubmit={async () => {
        if (USE_REAL) {
          const r = await realApi.createMyLetter({
            letterType:
              typeId === "salary"
                ? "salary-confirmation"
                : typeId === "visa"
                  ? "visa"
                  : typeId === "service"
                    ? "service-certificate"
                    : "employment-confirmation",
            addressee: addressee || "To whom it may concern",
            purpose: type.name,
          });
          onDone(String((r as { id?: unknown }).id ?? ""));
          return;
        }
        onDone(`MOCK-${Date.now().toString(36)}`);
      }}
    />
  );
}

function LettersPage() {
  const [requesting, setRequesting] = useState(false);
  const [ref, setRef] = useState<string | null>(null);
  const issuedState = useApi(
    async (): Promise<IssuedLetter[]> =>
      USE_REAL ? adaptLetters((await realApi.myLetters()).items) : staticLetters,
    [],
  );

  if (ref) {
    return (
      <AuthGate>
        <AppShell>
          <PageHeader eyebrow="Letters" title="Request submitted" />
          <NextSteps
            reference={`LT-${ref}`}
            title="Letter requested"
            steps={[
              "HR operations will issue it within the stated turnaround.",
              "You will be notified here when it is ready to download.",
              "Each issued letter carries a verification code the recipient can check.",
            ]}
            actions={
              <Button
                onClick={() => {
                  setRef(null);
                  setRequesting(false);
                }}
              >
                Back to letters
              </Button>
            }
          />
        </AppShell>
      </AuthGate>
    );
  }

  if (requesting) {
    return (
      <AppShell>
        <PageHeader
          eyebrow="Letters"
          title="Request a letter"
          description="Four short steps. You will see exactly what the letter discloses before submitting."
          primaryAction={
            <Button variant="ghost" onClick={() => setRequesting(false)}>
              Cancel
            </Button>
          }
        />
        <RequestFlow onDone={setRef} />
      </AppShell>
    );
  }

  return (
    <AppShell>
      <PageHeader
        eyebrow="Employee experience"
        title="Letters and certificates"
        description="Request an employment letter without opening a case. You always see what it discloses first."
        primaryAction={<Button onClick={() => setRequesting(true)}>Request a letter</Button>}
      />

      <section aria-label="Available letters">
        <h2 className="text-sm font-semibold">What you can request</h2>
        <ul className="mt-3 grid gap-3 sm:grid-cols-2">
          {letterTypes.map((t) => (
            <li key={t.id} className="rounded-lg border bg-surface p-4">
              <h3 className="flex items-center gap-2 text-sm font-medium">
                <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                {t.name}
              </h3>
              <p className="mt-1 text-xs text-muted-foreground">{t.purpose}</p>
              <p className="mt-2 flex items-center gap-1.5 text-[11px] text-muted-foreground">
                <Clock className="size-3.5 shrink-0" aria-hidden />
                {t.turnaround}
                {t.needsApproval ? " · needs approval" : " · issued automatically"}
              </p>
            </li>
          ))}
        </ul>
      </section>

      <section aria-label="Previously issued" className="pt-2">
        <h2 className="text-sm font-semibold">Previously issued</h2>
        <Async state={issuedState}>
          {(rows) => (
            <ul className="mt-3 divide-y rounded-lg border bg-surface">
              {rows.map((l) => (
                <li key={l.id} className="flex flex-wrap items-center gap-3 p-4">
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs text-muted-foreground">{l.id}</span>
                      <span className="text-sm font-medium">{l.type}</span>
                      <StatusBadge status={l.status} />
                    </span>
                    <span className="mt-0.5 block text-xs text-muted-foreground">
                      For {l.addressee} · requested {l.requested}
                    </span>
                    <span className="mt-1 flex items-center gap-1.5 text-[11px] text-muted-foreground">
                      <ShieldCheck className="size-3.5 shrink-0 text-success" aria-hidden />
                      Verification code {l.verification}
                    </span>
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    className="gap-2"
                    disabled={l.status === "In review"}
                    onClick={async () => {
                      try {
                        await realApi.downloadMyLetter(l.id, `${l.type}-${l.id}.txt`);
                      } catch (error) {
                        toast.error(
                          error instanceof Error ? error.message : "Letter download failed",
                        );
                      }
                    }}
                  >
                    <Download className="size-4" aria-hidden />
                    Download
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </Async>
        <p className="mt-3 text-xs text-muted-foreground">
          Need something not listed here?{" "}
          <Link to="/hrm/requests/new" className="text-primary underline underline-offset-2">
            Raise an HR request
          </Link>
          .
        </p>
      </section>
    </AppShell>
  );
}
