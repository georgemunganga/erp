import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import {
  Award,
  Check,
  EyeOff,
  HeartHandshake,
  Info,
  Megaphone,
  Minus,
  ShieldCheck,
  TrendingDown,
  TrendingUp,
  TriangleAlert,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { experienceApi } from "@/mock/experience";
import type { Announcement } from "@/mock/experience";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/experience/announcements")({
  head: () => ({
    meta: [
      { title: "Engagement — Kuubu HRM" },
      { name: "description", content: "Announcements, surveys with anonymity thresholds, recognition and wellbeing support." },
      { property: "og:title", content: "Engagement — Kuubu HRM" },
      { property: "og:description", content: "Announcements, surveys with anonymity thresholds, recognition and wellbeing support." },
    ],
  }),
  component: EngagementPage,
});

const kindMeta = {
  Company: { cls: "border-border bg-muted text-muted-foreground" },
  Policy: { cls: "border-info/30 bg-info-soft text-info" },
  Emergency: { cls: "border-danger/30 bg-danger-soft text-danger" },
  Local: { cls: "border-border bg-muted text-muted-foreground" },
} as const;

function AnnouncementCard({ a }: { a: Announcement }) {
  const [ack, setAck] = useState(false);
  const pct = a.audienceSize ? Math.round((a.acknowledged / a.audienceSize) * 100) : 0;

  return (
    <li className="rounded-lg border bg-surface p-5">
      <div className="flex flex-wrap items-start gap-2">
        <Megaphone className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${kindMeta[a.kind].cls}`}>
              {a.kind}
            </span>
            <span className="font-mono text-[11px] text-muted-foreground">{a.id}</span>
          </span>
          <span className="mt-1 block text-sm font-medium">{a.title}</span>
        </span>
      </div>

      <p className="mt-2 text-sm">{a.body}</p>

      <p className="mt-2 text-xs text-muted-foreground">
        {a.audience} · published {a.published}
        {a.expires ? ` · expires ${a.expires}` : ""} · {a.author}
      </p>

      {a.requiresAcknowledgement ? (
        <div className="mt-3 flex flex-wrap items-center gap-3 rounded-md border bg-surface-muted p-3">
          <span className="min-w-0 flex-1 text-xs">
            <span className="font-medium">Acknowledgement required. </span>
            {a.acknowledged} of {a.audienceSize} people have confirmed they have read it ({pct}%).
          </span>
          {ack ? (
            <span className="inline-flex items-center gap-1.5 text-xs font-medium text-success">
              <Check className="size-3.5 shrink-0" aria-hidden />
              You have acknowledged this
            </span>
          ) : (
            <Button size="sm" variant="outline" onClick={() => setAck(true)}>
              Acknowledge
            </Button>
          )}
        </div>
      ) : null}
    </li>
  );
}

function EngagementPage() {
  const announcements = useMock(() => experienceApi.announcements());
  const surveys = useMock(() => experienceApi.surveys());
  const recognitions = useMock(() => experienceApi.recognitions());
  const [tab, setTab] = useState<"news" | "surveys" | "recognition" | "wellbeing">("news");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Employee experience"
        title="Engagement"
        description="Announcements, how the organisation asks for feedback, and how people are recognised."
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Engagement views">
        {([
          ["news", "Announcements"],
          ["surveys", "Surveys"],
          ["recognition", "Recognition"],
          ["wellbeing", "Wellbeing"],
        ] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              tab === id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "news" ? (
        <Async state={announcements} rows={3}>
          {(rows) => (
            <ul className="space-y-4">
              {rows.map((a) => (
                <AnnouncementCard key={a.id} a={a} />
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "surveys" ? (
        <>
          <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
            <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              An anonymous survey is only anonymous if the results cannot be traced back. Scores are
              withheld below 10 responses, and free-text comments below 25 — because how someone
              writes identifies them far more reliably than what they scored.
            </span>
          </p>

          <Async state={surveys} rows={2}>
            {(rows) => (
              <ul className="space-y-4">
                {rows.map((s) => (
                  <li key={s.id} className="rounded-lg border bg-surface p-5">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium">{s.name}</span>
                      <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">{s.kind}</span>
                      <span className="font-mono text-[11px] text-muted-foreground">{s.id}</span>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Closed {s.closed} · {s.responded} of {s.invited} responded
                    </p>

                    <div className="mt-3 space-y-2">
                      <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2.5 text-xs text-warning">
                        <EyeOff className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                        <span>
                          <span className="font-medium">Score suppressed. </span>
                          {s.suppressionNote}
                        </span>
                      </p>
                      <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2.5 text-xs text-warning">
                        <EyeOff className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                        <span>
                          <span className="font-medium">Free text withheld. </span>
                          {s.freeTextNote}
                        </span>
                      </p>
                    </div>

                    {s.themes.length ? (
                      <div className="mt-3">
                        <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                          Themes safe to share
                        </p>
                        <ul className="mt-1 space-y-1.5">
                          {s.themes.map((t) => {
                            const Icon = t.direction === "up" ? TrendingUp : t.direction === "down" ? TrendingDown : Minus;
                            return (
                              <li key={t.theme} className="flex items-start gap-2 text-sm">
                                <Icon
                                  className={`mt-0.5 size-3.5 shrink-0 ${t.direction === "up" ? "text-success" : t.direction === "down" ? "text-warning" : "text-muted-foreground"}`}
                                  aria-hidden
                                />
                                <span>
                                  {t.theme}
                                  <span className="block text-xs text-muted-foreground">
                                    {t.direction === "up" ? "Improving" : t.direction === "down" ? "Worsening" : "No change"} · {t.comment}
                                  </span>
                                </span>
                              </li>
                            );
                          })}
                        </ul>
                        <p className="mt-2 text-[11px] text-muted-foreground">
                          A theme can be acted on without publishing a number. This is how a small
                          organisation still learns something from a survey it cannot report.
                        </p>
                      </div>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </Async>
        </>
      ) : null}

      {tab === "recognition" ? (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-sm font-semibold">Recent recognition</h2>
            <Button size="sm">Recognise a colleague</Button>
          </div>
          <Async state={recognitions} rows={3}>
            {(rows) => (
              <ul className="space-y-3">
                {rows.map((r) => (
                  <li key={r.id} className="rounded-lg border bg-surface p-4">
                    <div className="flex flex-wrap items-start gap-2">
                      <Award className="mt-0.5 size-4 shrink-0 text-accent" aria-hidden />
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm">
                          <span className="font-medium">{r.from}</span> recognised{" "}
                          <span className="font-medium">{r.to}</span>
                        </span>
                        <span className="mt-0.5 block text-[11px] text-muted-foreground">
                          {r.value} · {r.when} · {r.visibility}
                        </span>
                      </span>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{r.message}</p>
                  </li>
                ))}
              </ul>
            )}
          </Async>
          <p className="flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Recognition is not performance data. It does not feed a rating, and its absence is not
            evidence of anything — some good work is invisible by nature.
          </p>
        </>
      ) : null}

      {tab === "wellbeing" ? (
        <>
          <section aria-label="Support available" className="rounded-lg border bg-surface p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <HeartHandshake className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              Support available to you
            </h2>
            <ul className="mt-3 space-y-3 text-sm">
              <li className="rounded-md border p-3">
                <p className="font-medium">Employee assistance programme</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  Free, independent and confidential counselling and practical advice, available 24
                  hours. Provided by an external organisation, not by us.
                </p>
                <Button size="sm" variant="outline" className="mt-2">
                  Contact the provider directly
                </Button>
              </li>
              <li className="rounded-md border p-3">
                <p className="font-medium">Occupational health</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  For work-related health questions and workplace adjustments. Records the outcome
                  only — never your diagnosis.
                </p>
              </li>
              <li className="rounded-md border p-3">
                <p className="font-medium">Talk to someone about workload</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  If the volume or pace of work is the problem, that is a management conversation and
                  a legitimate one to have.
                </p>
              </li>
            </ul>
          </section>

          <section aria-label="Confidentiality" className="rounded-lg border border-success/30 bg-success-soft p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold text-success">
              <ShieldCheck className="size-4 shrink-0" aria-hidden />
              HR cannot see whether you used any of this
            </h2>
            <p className="mt-2 text-sm text-foreground">
              Employee-assistance usage is not reported to us — not per person, and not as a
              department count that could identify a small team. We are told only that the service
              exists and is being used across the organisation. Nobody is asked to explain a
              referral, and nothing about it reaches your employee record, your manager or a pay
              review.
            </p>
            <p className="mt-2 flex gap-2 text-xs text-success">
              <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span>
                If any part of this product ever starts reporting individual wellbeing usage to HR,
                that is a defect, not a feature.
              </span>
            </p>
          </section>
        </>
      ) : null}
    </AppShell>
      </AuthGate>
  );
}
