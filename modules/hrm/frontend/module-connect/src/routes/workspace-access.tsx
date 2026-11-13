import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { KuubuIdentity } from "@/platform/components/KuubuIdentity";
import { CompanyModuleAccess } from "@/modules/core/CompanyModuleAccess";
import { CompanyErpSession } from "@/modules/core/CompanyErpSession";
import {
  createOperationalClient,
  OperationalError,
  type OperationalCompany,
  type OperationalReview,
  type OperationalSession,
} from "@/modules/core/product-operational-api";

export const Route = createFileRoute("/workspace-access")({
  head: () => ({
    meta: [
      { title: "Your companies — Kuubu" },
      { name: "robots", content: "noindex, nofollow" },
      { name: "referrer", content: "no-referrer" },
    ],
  }),
  component: WorkspaceAccess,
});
const api = createOperationalClient();
type Entry =
  | { status: "loading" }
  | { status: "signed-out"; csrf: string }
  | { status: "unavailable"; csrf: string | null }
  | {
      status: "ready";
      session: OperationalSession;
      companies: OperationalCompany[];
      next: string | null;
    };
const roleNames = { owner: "Owner", administrator: "Administrator", member: "Member" };
const moduleNames: Record<string, string> = {
  hrm: "Human resources",
  procurement: "Procurement",
  payroll: "Payroll",
  accounting: "Accounting",
};
function message(error: unknown) {
  if (error instanceof OperationalError) {
    if (error.status === 401) return "Your sign-in has expired. Continue with Kuubu again.";
    if (error.status === 403)
      return "Company access or the subscription has changed. Refresh your companies or contact an administrator.";
    if (error.status === 429) return "Please wait a moment before trying again.";
  }
  return "We couldn’t confirm company access. Please try again.";
}
function WorkspaceAccess() {
  const [entry, setEntry] = useState<Entry>({ status: "loading" });
  const [version, setVersion] = useState(0);
  const [selected, setSelected] = useState<OperationalCompany | null>(null);
  const [review, setReview] = useState<OperationalReview | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [paging, setPaging] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [reviewVersion, setReviewVersion] = useState(0);
  const pending = useRef(false);
  const mounted = useRef(false);
  const pagingController = useRef<AbortController | null>(null);
  const currentReview =
    review && review.company.id === selected?.id && review.company.linkId === selected.linkId
      ? review
      : null;
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      pagingController.current?.abort();
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    pagingController.current?.abort();
    setPaging(false);
    setEntry({ status: "loading" });
    setSelected(null);
    setReview(null);
    setError(null);
    let csrf: string | null = null;
    async function load() {
      try {
        csrf = await api.bootstrap(controller.signal);
        const session = await api.session(controller.signal);
        const page = session.companiesAvailable
          ? await api.companies(null, controller.signal)
          : { rows: [], next: null };
        if (!controller.signal.aborted)
          setEntry({ status: "ready", session, companies: page.rows, next: page.next });
      } catch (failure) {
        if (controller.signal.aborted) return;
        if (csrf && failure instanceof OperationalError && failure.status === 401)
          setEntry({ status: "signed-out", csrf });
        else {
          setEntry({ status: "unavailable", csrf });
          setError(message(failure));
        }
      }
    }
    void load();
    return () => controller.abort();
  }, [version]);
  useEffect(() => {
    const controller = new AbortController();
    setReview(null);
    setReviewError(null);
    setReviewing(Boolean(selected));
    if (selected)
      void api
        .review(selected, controller.signal)
        .then((value) => {
          if (!controller.signal.aborted) setReview(value);
        })
        .catch((failure) => {
          if (!controller.signal.aborted) {
            setReviewError(message(failure));
            if (failure instanceof OperationalError && failure.status === 401)
              setEntry((current) =>
                current.status === "ready"
                  ? { status: "signed-out", csrf: current.session.csrf }
                  : current,
              );
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) setReviewing(false);
        });
    return () => controller.abort();
  }, [selected, reviewVersion]);
  const csrf =
    entry.status === "ready"
      ? entry.session.csrf
      : entry.status === "signed-out" || entry.status === "unavailable"
        ? entry.csrf
        : null;
  async function signIn() {
    if (!csrf || pending.current) return;
    pending.current = true;
    setBusy(true);
    setError(null);
    try {
      const url = await api.start(csrf);
      if (mounted.current) window.location.assign(url);
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function signOut() {
    if (!csrf || pending.current) return;
    pending.current = true;
    setBusy(true);
    setError(null);
    pagingController.current?.abort();
    setSelected(null);
    setReview(null);
    try {
      await api.logout(csrf);
      if (mounted.current) setVersion((value) => value + 1);
    } catch (failure) {
      if (mounted.current) {
        setEntry({ status: "unavailable", csrf });
        setError(message(failure));
      }
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function moreCompanies() {
    if (entry.status !== "ready" || !entry.next || pagingController.current || pending.current)
      return;
    const controller = new AbortController();
    pagingController.current = controller;
    setPaging(true);
    setError(null);
    try {
      const page = await api.companies(entry.next, controller.signal);
      if (!controller.signal.aborted && mounted.current)
        setEntry((current) => {
          if (current.status !== "ready") return current;
          const known = new Set(current.companies.map((row) => row.id + ":" + row.linkId));
          return {
            ...current,
            companies: [
              ...current.companies,
              ...page.rows.filter((row) => !known.has(row.id + ":" + row.linkId)),
            ],
            next: page.next,
          };
        });
    } catch (failure) {
      if (!controller.signal.aborted && mounted.current) setError(message(failure));
    } finally {
      if (pagingController.current === controller) {
        pagingController.current = null;
        if (mounted.current) setPaging(false);
      }
    }
  }
  return (
    <main className="min-h-dvh bg-background text-foreground">
      <div className="mx-auto max-w-6xl px-5 py-8 md:px-8 md:py-12">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <KuubuIdentity />
          {entry.status === "ready" && (
            <Button variant="outline" disabled={busy} onClick={() => void signOut()}>
              Sign out
            </Button>
          )}
        </header>
        <div className="mb-8 mt-10 max-w-2xl">
          <p className="mb-2 text-sm font-medium text-muted-foreground">Your workspace</p>
          <h1 className="text-2xl font-semibold">
            {entry.status === "signed-out" ? "Continue to Kuubu" : "Your companies"}
          </h1>
          <p className="mt-3 text-sm text-muted-foreground">
            Choose a company you belong to. Your company role, subscription and ERP permissions are
            checked separately.
          </p>
        </div>
        {error && (
          <p role="alert" className="mb-5 text-sm text-destructive">
            {error}
          </p>
        )}
        {entry.status === "loading" ? (
          <p role="status">Checking your Kuubu access…</p>
        ) : entry.status === "signed-out" ? (
          <Card className="max-w-xl">
            <CardContent className="space-y-5 p-6">
              <h2 className="text-lg font-semibold">Use your Kuubu account</h2>
              <p className="text-sm text-muted-foreground">
                Sign in and confirm the requested identity checks before choosing a company.
              </p>
              <Button disabled={busy} onClick={() => void signIn()}>
                Continue with Kuubu
              </Button>
              <a href="https://www.kuubuerp.com/" className="block min-h-11 py-3 text-sm underline">
                Back to Kuubu
              </a>
            </CardContent>
          </Card>
        ) : entry.status === "unavailable" ? (
          <div className="max-w-xl space-y-4">
            <h2 className="text-lg font-semibold">Kuubu access is temporarily unavailable</h2>
            <div className="flex flex-wrap gap-3">
              <Button disabled={busy} onClick={() => setVersion((value) => value + 1)}>
                Try again
              </Button>
              {csrf && (
                <Button variant="outline" disabled={busy} onClick={() => void signIn()}>
                  Sign in again
                </Button>
              )}
            </div>
          </div>
        ) : !entry.session.companiesAvailable ? (
          <div className="max-w-xl space-y-4">
            <h2 className="text-lg font-semibold">Company access is not available yet</h2>
            <p className="text-sm text-muted-foreground">
              Your identity is verified. Company access has not been enabled for this service.
            </p>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => setVersion((value) => value + 1)}
            >
              Check again
            </Button>
          </div>
        ) : (
          <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            <section aria-label="Your companies" className="min-w-0 space-y-3">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-lg font-semibold">Choose a company</h2>
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => setVersion((value) => value + 1)}
                >
                  Refresh companies
                </Button>
              </div>
              {entry.companies.length === 0 ? (
                <Card>
                  <CardContent className="space-y-4 p-6">
                    <h3 className="font-semibold">No companies yet</h3>
                    <p className="text-sm text-muted-foreground">
                      Ask a company administrator for an invitation, or create a company with an
                      eligible subscription.
                    </p>
                    <Button asChild>
                      <a href="/setup">Create a company</a>
                    </Button>
                  </CardContent>
                </Card>
              ) : (
                entry.companies.map((row) => (
                  <Card key={row.id + ":" + row.linkId}>
                    <CardContent className="flex flex-col items-stretch justify-between gap-4 p-5 sm:flex-row sm:items-center">
                      <div className="min-w-0 flex-1">
                        <h3 className="break-words font-semibold">{row.name}</h3>
                        <p className="mt-1 text-sm text-muted-foreground">
                          Company role: {roleNames[row.role]}
                        </p>
                      </div>
                      <Button
                        className="w-full sm:w-auto"
                        variant={
                          selected?.id === row.id && selected.linkId === row.linkId
                            ? "default"
                            : "outline"
                        }
                        disabled={busy}
                        aria-pressed={selected?.id === row.id && selected.linkId === row.linkId}
                        aria-label={`Review ${row.name}`}
                        onClick={() => setSelected(row)}
                      >
                        Review company
                      </Button>
                    </CardContent>
                  </Card>
                ))
              )}
              {entry.next && (
                <Button
                  variant="outline"
                  disabled={paging || busy}
                  onClick={() => void moreCompanies()}
                >
                  {paging ? "Loading companies…" : "More companies"}
                </Button>
              )}
            </section>
            <section aria-label="Company access review" aria-live="polite" className="min-w-0">
              <Card>
                <CardContent className="space-y-5 p-6">
                  {!selected ? (
                    <>
                      <h2 className="text-lg font-semibold">Review company access</h2>
                      <p className="text-sm text-muted-foreground">
                        Select a company to check its current subscription and included modules.
                      </p>
                    </>
                  ) : reviewing ? (
                    <p role="status">Checking {selected.name}…</p>
                  ) : reviewError ? (
                    <>
                      <p role="alert" className="text-sm text-destructive">
                        {reviewError}
                      </p>
                      <Button
                        variant="outline"
                        disabled={busy}
                        onClick={() => setReviewVersion((value) => value + 1)}
                      >
                        Retry company check
                      </Button>
                    </>
                  ) : currentReview ? (
                    <>
                      <h2 className="break-words text-lg font-semibold">
                        {currentReview.company.name}
                      </h2>
                      <p className="text-sm">
                        Company role: {roleNames[currentReview.company.role]}
                      </p>
                      <div>
                        <h3 className="mb-3 text-sm font-semibold">Included in the subscription</h3>
                        {currentReview.modules.length ? (
                          <ul className="flex flex-wrap gap-2">
                            {currentReview.modules.map((code) => (
                              <li
                                key={code}
                                className="max-w-full break-words rounded-lg border border-border px-3 py-2 text-sm"
                              >
                                {moduleNames[code] || code}
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="text-sm text-muted-foreground">
                            No modules are included in the current allowance.
                          </p>
                        )}
                      </div>
                      <p className="text-sm text-muted-foreground">
                        Module access must be assigned before you can enter ERP. Company membership
                        alone does not provide module or branch permissions.
                      </p>
                      {currentReview.company.role !== "member" && (
                        <Button asChild variant="outline">
                          <a href="/team">Manage company team</a>
                        </Button>
                      )}
                    </>
                  ) : null}
                </CardContent>
              </Card>
            </section>
          </div>
        )}
        {entry.status === "ready" && currentReview && (
          <CompanyErpSession
            key={currentReview.company.id + ":" + currentReview.company.linkId + ":session"}
            company={currentReview.company}
            modules={currentReview.modules}
            csrf={entry.session.csrf}
            onBusy={setBusy}
            onExpired={() => setEntry({ status: "signed-out", csrf: entry.session.csrf })}
          />
        )}
        {entry.status === "ready" && currentReview?.company.role === "owner" && (
          <CompanyModuleAccess
            key={currentReview.company.id + ":" + currentReview.company.linkId}
            company={currentReview.company}
            modules={currentReview.modules}
            csrf={entry.session.csrf}
            onExpired={() => setEntry({ status: "signed-out", csrf: entry.session.csrf })}
          />
        )}
        <footer className="mt-10 border-t border-border pt-5">
          <a
            href="https://www.kuubuerp.com/account/"
            className="inline-flex min-h-11 items-center text-sm underline"
          >
            My Kuubu account
          </a>
        </footer>
      </div>
    </main>
  );
}
