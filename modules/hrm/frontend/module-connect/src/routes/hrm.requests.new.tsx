import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { BookOpen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { realApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { feedback } from "@/platform/feedback";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/requests/new")({
  head: () => ({
    meta: [
      { title: "Raise an HR request — Kuubu HRM" },
      {
        name: "description",
        content: "Guided case submission with purpose, details, evidence and next steps.",
      },
      { property: "og:title", content: "Raise an HR request — Kuubu HRM" },
      {
        property: "og:description",
        content: "Guided case submission with purpose, details, evidence and next steps.",
      },
    ],
  }),
  component: NewRequest,
});

const categories = [
  "Employment letter",
  "Personal data change",
  "Payroll query",
  "Contract query",
  "Something else",
] as const;

const categoryCode: Record<string, string> = {
  "Employment letter": "employment-letter",
  "Personal data change": "data-change",
  "Payroll query": "payroll",
  "Contract query": "contract",
  "Something else": "other",
};

const suggestedAnswers: Record<string, string> = {
  "Employment letter":
    "Most employment/salary confirmation letters are issued within 2 working days — you may not need to wait for a case.",
  "Personal data change":
    "Bank and address changes need one piece of supporting evidence (e.g. a bank letter) and take effect from the next pay run.",
  "Payroll query":
    "Payslip explanations are available directly on each payslip — check there before raising a case.",
  "Contract query": "Contract copies are available in Documents on your profile.",
  "Something else": "",
};

function NewRequest() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [category, setCategory] = useState<string>(categories[0]);
  const [subject, setSubject] = useState("");
  const [detail, setDetail] = useState("");
  const [confidentiality, setConfidentiality] = useState("standard");
  const [contact, setContact] = useState("app");
  const [submitting, setSubmitting] = useState(false);

  const suggestion = useMemo(() => suggestedAnswers[category], [category]);

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "Describe what you need",
      purpose: "Picking the right category routes this to the person who can actually help.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <div>
            <Label htmlFor="category">Category</Label>
            <Select value={category} onValueChange={setCategory}>
              <SelectTrigger id="category" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {categories.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {suggestion ? (
            <div className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
              <BookOpen className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>{suggestion} You can still continue and raise a case below.</span>
            </div>
          ) : null}
          <div>
            <Label htmlFor="subject">Subject</Label>
            <Input
              id="subject"
              className="mt-1"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="Short summary"
            />
          </div>
        </div>
      ),
    },
    {
      id: "details",
      title: "Essential details",
      purpose: "Only what's relevant to this category — nothing you don't need to answer.",
      render: () => (
        <div className="max-w-lg">
          <Label htmlFor="detail">Details</Label>
          <Textarea
            id="detail"
            className="mt-1"
            rows={5}
            value={detail}
            onChange={(e) => setDetail(e.target.value)}
            placeholder="What do you need, and by when?"
          />
          <div className="mt-4 rounded-md border border-dashed p-4 text-center text-xs text-muted-foreground">
            Attach supporting evidence if you have it — for now describe the evidence in the
            details; file upload is on the roadmap.
          </div>
        </div>
      ),
    },
    {
      id: "confidentiality",
      title: "Confidentiality and contact",
      purpose: "Who can see this, and how you'd like to be reached.",
      render: () => (
        <div className="max-w-lg space-y-5">
          <fieldset>
            <legend className="text-sm font-medium">Confidentiality</legend>
            <RadioGroup
              value={confidentiality}
              onValueChange={setConfidentiality}
              className="mt-2 space-y-2"
            >
              <label className="flex items-center gap-2 text-sm">
                <RadioGroupItem value="standard" /> Standard — visible to HR operations and your
                manager where relevant
              </label>
              <label className="flex items-center gap-2 text-sm">
                <RadioGroupItem value="restricted" /> Restricted — visible to HR operations only
              </label>
            </RadioGroup>
          </fieldset>
          <fieldset>
            <legend className="text-sm font-medium">Preferred contact</legend>
            <RadioGroup value={contact} onValueChange={setContact} className="mt-2 space-y-2">
              <label className="flex items-center gap-2 text-sm">
                <RadioGroupItem value="app" /> In this app only
              </label>
              <label className="flex items-center gap-2 text-sm">
                <RadioGroupItem value="email" /> Email me updates too
              </label>
            </RadioGroup>
          </fieldset>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose:
        "Check the facts. Submitting creates one case thread you can track from HR requests.",
      render: () => (
        <dl className="grid max-w-lg gap-3 sm:grid-cols-2">
          {[
            ["Category", category],
            ["Subject", subject || "Not given"],
            ["Confidentiality", confidentiality === "restricted" ? "Restricted" : "Standard"],
            ["Contact", contact === "email" ? "In-app + email" : "In-app only"],
          ].map(([k, v]) => (
            <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
              <dt className="text-xs text-muted-foreground">{k}</dt>
              <dd className="text-sm font-medium">{v}</dd>
            </div>
          ))}
        </dl>
      ),
    },
  ];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="HR requests"
          title="Raise an HR request"
          description="Four short steps. Review the request before submitting it to HR."
        />
        <GuidedFlow
          flowId="request-new"
          steps={steps}
          submitLabel="Submit request"
          onSubmit={async () => {
            if (!subject.trim()) {
              feedback.blocked(
                "Subject is missing",
                "Add a subject so HR knows what this is about.",
              );
              return;
            }
            setSubmitting(true);
            try {
              const created = (await realApi.createMyRequest({
                category: categoryCode[category] ?? "other",
                subject: subject.trim(),
                body: detail.trim() || subject.trim(),
                confidentiality: confidentiality === "restricted" ? "confidential" : "normal",
              })) as { id?: unknown };
              const id = typeof created?.id === "string" ? created.id : null;
              setRef(id ?? "thread");
              feedback.submitted(
                "Your HR request has been submitted",
                "HR operations will respond in the case thread; you can check status from HR requests.",
              );
            } catch (e) {
              feedback.blocked(
                "Failed to submit the request",
                e instanceof Error ? e.message : "Please try again in a moment.",
              );
              setRef(null);
            } finally {
              setSubmitting(false);
            }
          }}
          submitted={
            ref ? (
              <NextSteps
                reference={`HR-${ref}`}
                title="Request submitted"
                steps={[
                  "HR operations will respond within the standard service target for this category.",
                  "You'll see every update and can reply from the same case thread — no separate email chain.",
                  "You can check status any time from HR requests.",
                ]}
                actions={
                  <>
                    <Button onClick={() => navigate({ to: "/hrm/requests" })}>
                      View my requests
                    </Button>
                    <Button variant="outline" asChild>
                      <Link to="/hrm">Back to Home</Link>
                    </Button>
                  </>
                }
              />
            ) : undefined
          }
        />
      </AppShell>
    </AuthGate>
  );
}
