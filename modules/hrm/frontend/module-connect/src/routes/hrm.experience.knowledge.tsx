import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import {
  BookOpen,
  CircleSlash,
  FileClock,
  Info,
  Quote,
  Search,
  Sparkles,
  ThumbsDown,
  ThumbsUp,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { experienceApi } from "@/mock/experience";
import type { Article } from "@/mock/experience";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/experience/knowledge")({
  head: () => ({
    meta: [
      { title: "HR knowledge — Kuubu HRM" },
      { name: "description", content: "Approved HR articles, and an assistant that answers only from them and cites what it used." },
      { property: "og:title", content: "HR knowledge — Kuubu HRM" },
      { property: "og:description", content: "Approved HR articles, and an assistant that answers only from them and cites what it used." },
    ],
  }),
  component: KnowledgePage,
});

const stateMeta = {
  Approved: { icon: BookOpen, cls: "border-success/30 bg-success-soft text-success", word: "Approved" },
  "In review": { icon: FileClock, cls: "border-warning/40 bg-warning-soft text-warning", word: "In review — not published" },
  Expired: { icon: CircleSlash, cls: "border-border bg-muted text-muted-foreground", word: "Expired — do not rely on" },
} as const;

function ArticleCard({ a }: { a: Article }) {
  const { icon: Icon, cls, word } = stateMeta[a.state];
  return (
    <li className="rounded-lg border bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <span className="min-w-0">
          <span className="block text-sm font-medium">{a.title}</span>
          <span className="mt-0.5 block text-xs text-muted-foreground">
            {a.category} · {a.version} · updated {a.updated}
          </span>
        </span>
        <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] font-medium ${cls}`}>
          <Icon className="size-3 shrink-0" aria-hidden />
          {word}
        </span>
      </div>

      <p className="mt-2 text-sm text-muted-foreground">{a.summary}</p>

      <dl className="mt-3 grid gap-x-6 gap-y-1 text-[11px] sm:grid-cols-2">
        <div className="flex gap-1.5">
          <dt className="text-muted-foreground">Applies to:</dt>
          <dd>{a.appliesTo}</dd>
        </div>
        <div className="flex gap-1.5">
          <dt className="text-muted-foreground">Owner:</dt>
          <dd>{a.owner}</dd>
        </div>
        <div className="flex gap-1.5">
          <dt className="text-muted-foreground">Languages:</dt>
          <dd>{a.languages.join(", ")}</dd>
        </div>
        <div className="flex gap-1.5">
          <dt className="text-muted-foreground">Review due:</dt>
          <dd>{a.reviewDue}</dd>
        </div>
      </dl>

      {a.state === "Approved" ? (
        <p className="mt-3 flex items-center gap-3 text-[11px] text-muted-foreground">
          <span>{a.views30d} views in 30 days</span>
          <span className="inline-flex items-center gap-1">
            <ThumbsUp className="size-3 shrink-0" aria-hidden />
            {a.helpful}
          </span>
          <span className="inline-flex items-center gap-1">
            <ThumbsDown className="size-3 shrink-0" aria-hidden />
            {a.notHelpful}
          </span>
        </p>
      ) : null}
    </li>
  );
}

function KnowledgePage() {
  const articles = useMock(() => experienceApi.articles());
  const exchanges = useMock(() => experienceApi.exchanges());
  const [q, setQ] = useState("");
  const [asked, setAsked] = useState(0);

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Employee experience"
        title="HR knowledge"
        description="Answers come from approved articles only. Where there is no approved source, you are told so and offered a person — not a guess."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/requests/new">Ask HR directly</Link>
          </Button>
        }
      />

      <section aria-label="Search and assistant" className="rounded-lg border bg-surface p-5">
        <div className="relative max-w-xl">
          <Search className="pointer-events-none absolute left-2.5 top-2.5 size-4 text-muted-foreground" aria-hidden />
          <Input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Ask a question, or search the articles"
            aria-label="Ask a question or search articles"
            className="pl-8"
          />
        </div>

        <Async state={exchanges} rows={2}>
          {(rows) => {
            const ex = rows[asked % rows.length];
            return (
              <div className="mt-4 space-y-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
                    <Sparkles className="size-3.5" aria-hidden />
                    Example exchange
                  </span>
                  <Button size="sm" variant="ghost" onClick={() => setAsked((n) => n + 1)}>
                    Show another
                  </Button>
                </div>

                <div className="rounded-md border bg-surface-muted p-3">
                  <p className="text-sm font-medium">{ex.question}</p>

                  {ex.answer ? (
                    <>
                      <p className="mt-2 text-sm">{ex.answer}</p>
                      <div className="mt-3">
                        <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                          Answered from
                        </p>
                        <ul className="mt-1 space-y-1">
                          {ex.citations.map((c) => (
                            <li key={c.articleId} className="flex items-start gap-1.5 text-xs">
                              <Quote className="mt-0.5 size-3 shrink-0 text-muted-foreground" aria-hidden />
                              <span>
                                <span className="font-mono text-[11px] text-muted-foreground">{c.articleId}</span>{" "}
                                {c.title}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    </>
                  ) : (
                    <div className="mt-2 rounded-md border border-warning/40 bg-warning-soft p-3">
                      <p className="text-sm font-medium text-warning">
                        I do not have an approved answer for this
                      </p>
                      <p className="mt-1 text-sm text-foreground">
                        There is no published article covering it yet. Rather than guess, this goes to
                        a person who can answer properly.
                      </p>
                      <Button size="sm" className="mt-3" asChild>
                        <Link to="/hrm/requests/new">Raise an HR request</Link>
                      </Button>
                    </div>
                  )}

                  <p className="mt-3 flex gap-2 border-t pt-2 text-[11px] text-muted-foreground">
                    <Info className="mt-0.5 size-3 shrink-0" aria-hidden />
                    {ex.note}
                  </p>
                </div>
              </div>
            );
          }}
        </Async>

        <p className="mt-4 flex gap-2 text-xs text-muted-foreground">
          <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          What you ask here is not shared with your manager and is not added to your employee record.
          Questions are used only to find gaps in the articles.
        </p>
      </section>

      <section aria-label="Articles">
        <h2 className="text-sm font-semibold">Articles</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          Each article states who owns it, which populations it applies to, and when it is next
          reviewed. An article past its review date is marked expired rather than quietly left up.
        </p>
        <Async state={articles} rows={4}>
          {(rows) => {
            const filtered = q
              ? rows.filter((a) =>
                  `${a.title} ${a.category} ${a.summary}`.toLowerCase().includes(q.toLowerCase()),
                )
              : rows;
            return (
              <>
                <p aria-live="polite" className="mt-2 text-xs text-muted-foreground">
                  {filtered.length} of {rows.length} articles
                </p>
                <ul className="mt-3 space-y-3">
                  {filtered.map((a) => (
                    <ArticleCard key={a.id} a={a} />
                  ))}
                </ul>
              </>
            );
          }}
        </Async>
      </section>
    </AppShell>
      </AuthGate>
  );
}
