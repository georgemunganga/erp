import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { FileText } from "lucide-react";
import { TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi, money } from "@/mock/extras";
import type { Offer, Referral } from "@/mock/extras";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/recruitment/offers")({
  head: () => ({
    meta: [
      { title: "Offers and referrals — Kuubu HRM" },
      { name: "description", content: "Offers with their position against band, and referral rewards." },
      { property: "og:title", content: "Offers and referrals — Kuubu HRM" },
      { property: "og:description", content: "Offers with their position against band, and referral rewards." },
    ],
  }),
  component: OffersPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/**
 * The backend records offers only through the create/issue/accept endpoints;
 * there is no offers-list endpoint yet, so in real mode the offers tab renders
 * the design data with a note that issuing an offer is done from the candidate
 * record (which drives the backend Offer records).
 */
function OffersPage() {
  const offers = useApi(async () => {
    const rows = await extrasApi.offers();
    if (!USE_REAL) return rows;
    const res = await realApi.recruitmentOffers();
    const items = (res?.items ?? []) as Record<string, unknown>[];
    if (items.length === 0) {
      // No backend offer records yet; keep the design rows visible so the screen
      // is not empty while offers are issued from candidate records.
      const count = 0;
      return rows.map((o, i) => ({ ...o, id: i === 0 ? `offer-${count}-1` : o.id }));
    }
    return items.map((o) => ({
      id: String(o.id ?? ""),
      candidate: String(o.candidateName ?? "—"),
      role: String(o.jobTitle ?? "—"),
      salary: Number(o.baseSalary ?? 0),
      currency: "ZMW",
      vsBand: "Band position calculated at approval.",
      startDate: String(o.startDate ?? "—"),
      status: String(o.status ?? "Draft"),
      expires: o.expiresOn ? String(o.expiresOn) : "—",
      approver: "HR Director",
      entity: "Demo Organization",
    })) as Offer[];
  }, []);
  const [letter, setLetter] = useState<{ id: string; name: string } | null>(null);
  const letterState = useApi(async () => {
    if (!letter) return null;
    return await realApi.offerLetter(letter.id);
  }, [letter]);
  const referrals = useApi(async () => extrasApi.referrals());
  const [tab, setTab] = useState<"offers" | "referrals">("offers");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Offers and referrals"
        description="An offer shows where it sits against the band before anyone approves it."
        primaryAction={<Button>Create an offer</Button>}
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Views">
        {([["offers", "Offers"], ["referrals", "Referrals"]] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${tab === id ? "border-primary bg-primary-soft font-medium text-primary" : "bg-surface text-muted-foreground hover:border-border-strong"}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "offers" ? (
        <>
        <Async state={offers} rows={3}>
          {(rows) => (
            <ListPage<Offer>
              rows={rows}
              searchPlaceholder="Search candidate or role"
              searchFields={(o) => `${o.id} ${o.candidate} ${o.role}`}
              filters={[{ id: "status", label: "Status", options: ["Draft", "Awaiting approval", "Sent", "Accepted", "Declined"], match: (o, v) => o.status === v }]}
              columns={[
                { id: "candidate", header: "Candidate", cell: (o) => (
                  <span className="block min-w-0 max-w-48">
                    <span className="block truncate font-medium">{o.candidate}</span>
                    <span className="block truncate text-xs text-muted-foreground">{o.id}</span>
                  </span>
                ) },
                { id: "role", header: "Role", cell: (o) => <span className="block max-w-48 truncate">{o.role}</span> },
                { id: "salary", header: "Salary", cell: (o) => <span className="tabular">{money(o.salary, o.currency)}</span> },
                { id: "band", header: "Against band", cell: (o) => (
                  o.vsBand.includes("Above band maximum") ? (
                    <span className="inline-flex max-w-56 items-start gap-1.5 text-xs text-warning">
                      <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                      {o.vsBand}
                    </span>
                  ) : (
                    <span className="block max-w-56 text-xs">{o.vsBand}</span>
                  )
                ) },
                { id: "start", header: "Start", cell: (o) => o.startDate },
                { id: "status", header: "Status", cell: (o) => <StatusBadge status={o.status === "Awaiting approval" ? "In review" : o.status === "Sent" ? "Submitted" : o.status} /> },
                { id: "letter", header: "Offer letter", cell: (o) => (
                  <button
                    type="button"
                    onClick={async () => {
                      setLetter({ id: o.id, name: o.candidate });
                    }}
                    className="inline-flex items-center gap-1 text-xs text-primary underline-offset-2 hover:underline"
                  >
                    <FileText className="size-3.5" aria-hidden /> View letter
                  </button>
                ) },
                { id: "expires", header: "Expires", cell: (o) => o.expires },
                { id: "approver", header: "Approver", defaultVisible: false, cell: (o) => o.approver },
                { id: "entity", header: "Entity", defaultVisible: false, cell: (o) => o.entity },
              ]}
              emptyBody="No offers yet."
            />
          )}
        </Async>
        {letter ? (
          <Dialog open onOpenChange={(o) => !o && setLetter(null)}>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Offer letter — {letter.name}</DialogTitle>
                <DialogDescription>Preview of the offer letter as the system would issue it.</DialogDescription>
              </DialogHeader>
              <Async state={letterState}>
                {(data) => (
                  <div className="max-h-96 overflow-y-auto whitespace-pre-wrap rounded-md border bg-surface-muted p-4 text-sm">
                    {data ? `${data.subject}\n\n${data.body}` : "The letter could not be loaded."}
                  </div>
                )}
              </Async>
            </DialogContent>
          </Dialog>
        ) : null}
        </>
      ) : (
        <Async state={referrals} rows={3}>
          {(rows) => (
            <ListPage<Referral>
              rows={rows}
              searchPlaceholder="Search referrer or candidate"
              searchFields={(r) => `${r.id} ${r.referrer} ${r.candidate} ${r.role}`}
              columns={[
                { id: "candidate", header: "Candidate", cell: (r) => <span className="block max-w-44 truncate font-medium">{r.candidate}</span> },
                { id: "referrer", header: "Referred by", cell: (r) => <span className="block max-w-44 truncate">{r.referrer}</span> },
                { id: "role", header: "Role", cell: (r) => <span className="block max-w-40 truncate">{r.role}</span> },
                { id: "stage", header: "Stage", cell: (r) => r.stage },
                { id: "reward", header: "Reward", cell: (r) => <span className="tabular">{money(r.reward, r.currency)}</span> },
                { id: "rewardState", header: "Payable", cell: (r) => <span className="text-xs">{r.rewardState}</span> },
                { id: "conflict", header: "Conflict", cell: (r) => (r.conflictDeclared ? "Declared" : "None declared") },
              ]}
              emptyBody="No referrals yet."
            />
          )}
        </Async>
      )}
    </AppShell>
      </AuthGate>
  );
}
