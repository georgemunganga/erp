import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { ArrowRight, Check, Building2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { KuubuIdentity } from "@/platform/components/KuubuIdentity";
import {
  createProductSetupClient,
  productSetupStart,
  ProductSetupError,
  type SetupCompany,
  type SetupPayer,
  type SetupSession,
} from "@/modules/core/product-setup-api";
import {
  readSetupAttempt,
  saveSetupAttempt,
  clearSetupAttempt,
  type SetupAttempt,
  type StoredSetupAttempt,
} from "@/modules/core/product-setup-attempt";

export const Route = createFileRoute("/setup")({
  head: () => ({
    meta: [
      { title: "Create your company — Kuubu" },
      { name: "description", content: "Set up your company with your Kuubu account." },
      { name: "referrer", content: "no-referrer" },
      { name: "robots", content: "noindex, nofollow" },
    ],
  }),
  component: CompanySetup,
});

const api = createProductSetupClient();
type Step = "account" | "details" | "review";
function message(error: unknown): string {
  if (error instanceof ProductSetupError) {
    if (error.status === 401)
      return "Your setup authorization has expired. Continue with Kuubu again to resume.";
    if (error.status === 403)
      return "Your account or subscription cannot authorize this request. Check your billing access before trying again.";
    if (error.code === "CapacityExhausted")
      return "This subscription has no company places available. Review your allowance before trying again.";
    if (error.status === 409)
      return "This request conflicts with an earlier company request. Contact support before starting another.";
  }
  return "We couldn’t confirm the result. Try again using the same reviewed request.";
}

function CompanySetup() {
  const [session, setSession] = useState<SetupSession | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadVersion, setLoadVersion] = useState(0);
  const [payers, setPayers] = useState<SetupPayer[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [paging, setPaging] = useState(false);
  const [customerId, setCustomerId] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [step, setStep] = useState<Step>("account");
  const [confirmed, setConfirmed] = useState(false);
  const [stored, setStored] = useState<StoredSetupAttempt>({ status: "empty" });
  const [attempt, setAttempt] = useState<SetupAttempt | null>(null);
  const [result, setResult] = useState<SetupCompany | null>(null);
  const [busy, setBusy] = useState(false);
  const [expired, setExpired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const sending = useRef(false);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    const current = () => !controller.signal.aborted;
    setLoading(true);
    setError(null);
    setConfirmed(false);
    setExpired(false);
    setSession(null);
    setPayers([]);
    setCursor(null);
    let saved: StoredSetupAttempt;
    try {
      saved = readSetupAttempt(window.sessionStorage);
    } catch {
      saved = { status: "unavailable" };
    }
    setStored(saved);
    (async () => {
      try {
        const proof = await api.session(controller.signal);
        if (!current()) return;
        setSession(proof);
        if (saved.status === "ready" && saved.attempt.subject === proof.subject) {
          setAttempt(saved.attempt);
          setCustomerId(saved.attempt.command.customerId);
          setCompanyName(saved.attempt.command.companyName);
          setStep("review");
        } else {
          setAttempt(null);
          setCustomerId("");
          setCompanyName("");
          setStep("account");
        }
        const page = await api.payers(undefined, controller.signal);
        if (current()) {
          setPayers(page.customers);
          setCursor(page.nextCursor);
        }
      } catch (failure) {
        if (current()) {
          setError(message(failure));
          if (failure instanceof ProductSetupError && failure.status === 401) {
            setSession(null);
            setExpired(true);
          }
        }
      } finally {
        if (current()) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [loadVersion]);

  useEffect(() => {
    if (!session) return;
    const delay = Date.parse(session.expiresAt) - Date.now();
    if (delay <= 0) {
      setExpired(true);
      return;
    }
    const timer = window.setTimeout(() => setExpired(true), delay);
    return () => window.clearTimeout(timer);
  }, [session]);
  useEffect(() => {
    if (!loading) heading.current?.focus({ preventScroll: true });
  }, [step, loading, result]);

  const selected = payers.find((payer) => payer.customerId === customerId);
  const identityMismatch =
    stored.status === "ready" && session !== null && stored.attempt.subject !== session.subject;
  const storageBlocked = stored.status === "invalid" || stored.status === "unavailable";
  const name = companyName.trim().normalize("NFC");
  const validName = name.length > 0 && name.length <= 200 && !/\p{Cc}/u.test(name);
  const allowed = !!session && !expired && !storageBlocked && !identityMismatch;

  async function loadMore() {
    if (!cursor || paging || busy) return;
    setPaging(true);
    setError(null);
    try {
      const page = await api.payers(cursor);
      if (mounted.current) {
        setPayers((previous) => [
          ...previous,
          ...page.customers.filter(
            (row) => !previous.some((old) => old.customerId === row.customerId),
          ),
        ]);
        setCursor(page.nextCursor);
      }
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      if (mounted.current) setPaging(false);
    }
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session || !allowed || !selected || !validName || !confirmed || sending.current) return;
    sending.current = true;
    setBusy(true);
    setError(null);
    try {
      const reviewed: SetupAttempt = attempt ?? {
        version: 1,
        subject: session.subject,
        command: {
          customerId,
          companyName: name,
          idempotencyKey: crypto.randomUUID().replaceAll("-", ""),
        },
      };
      // Retain before sending; a lost response/reload must never generate another key.
      try {
        saveSetupAttempt(window.sessionStorage, reviewed);
      } catch {
        setStored({ status: "unavailable" });
        setError(
          "Your browser couldn’t retain the reviewed request. Nothing was sent. Enable storage before trying again.",
        );
        return;
      }
      setAttempt(reviewed);
      setStored({ status: "ready", attempt: reviewed });
      const created = await api.create(session, reviewed.command);
      if (mounted.current) {
        setResult(created);
        setConfirmed(false);
      }
    } catch (failure) {
      if (mounted.current) {
        setError(message(failure));
        if (failure instanceof ProductSetupError && failure.status === 401) setExpired(true);
      }
    } finally {
      sending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function finish() {
    if (!session || busy) return;
    setBusy(true);
    setError(null);
    try {
      try {
        await api.cancel(session);
      } catch (failure) {
        if (!(failure instanceof ProductSetupError && failure.status === 401)) throw failure;
      }
      if (result) clearSetupAttempt(window.sessionStorage);
      window.location.assign("https://www.kuubuerp.com/");
    } catch {
      setError("We couldn’t close setup. Please try again; your company request remains saved.");
      setBusy(false);
    }
  }
  const title = result
    ? "Your company is saved."
    : loading
      ? "Getting things ready…"
      : !session || expired
        ? "A home for your company."
        : identityMismatch
          ? "Resume with the original account."
          : storageBlocked
            ? "Keep your request safe."
            : step === "account"
              ? "Choose your billing account."
              : step === "details"
                ? "Tell us about your company."
                : attempt
                  ? "Check your previous request."
                  : "Ready to create your company?";
  const activeStep = result ? 3 : step === "account" ? 0 : step === "details" ? 1 : 2;

  return (
    <div className="kuubu-product-shell min-h-dvh bg-background text-foreground">
      <header className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-7 md:px-10">
        <KuubuIdentity />
        <a className="text-sm underline underline-offset-4" href="mailto:hello@greenwebb.tech">
          Get help
        </a>
      </header>
      <main className="mx-auto grid max-w-6xl gap-10 px-5 pb-12 pt-6 md:px-10 lg:grid-cols-[.85fr_1.15fr] lg:gap-20 lg:pt-12">
        <section className="space-y-6 lg:pt-8" aria-labelledby="setup-intro">
          <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground">
            Your Kuubu workspace
          </p>
          <h1
            id="setup-intro"
            className="max-w-md text-4xl font-semibold leading-tight tracking-tight md:text-5xl"
          >
            A clearer space
            <br />
            for your business
            <br />
            <span className="text-primary">to grow.</span>
          </h1>
          <p className="max-w-sm text-base leading-relaxed text-muted-foreground">
            Start with your company. Bring your people, work and records together as your business
            grows.
          </p>
          <div className="flex items-start gap-3 border-t border-border pt-6 text-sm leading-relaxed text-muted-foreground">
            <Building2 className="mt-1 size-5 shrink-0 text-primary" aria-hidden="true" />
            <p className="max-w-xs">
              Each company has its own workspace and permissions, with one Kuubu account to bring
              them together.
            </p>
          </div>
        </section>
        <section aria-labelledby="setup-heading" className="min-w-0">
          <ol
            aria-label="Company setup progress"
            className="mb-6 flex gap-4 text-sm text-muted-foreground"
          >
            {["Account", "Details", "Review"].map((label, index) => (
              <li
                key={label}
                aria-current={!result && activeStep === index ? "step" : undefined}
                className={`flex items-center gap-2 ${index <= activeStep ? "font-medium text-primary" : ""}`}
              >
                <span className="inline-flex size-7 items-center justify-center rounded-full border border-current text-xs">
                  {index < activeStep ? <Check className="size-4" aria-hidden="true" /> : index + 1}
                </span>
                <span>{label}</span>
              </li>
            ))}
          </ol>
          <Card className="rounded-xl border-border bg-card shadow-sm">
            <CardContent className="space-y-6 p-6 md:p-8">
              <div>
                <h2
                  ref={heading}
                  tabIndex={-1}
                  id="setup-heading"
                  className="text-2xl font-semibold tracking-tight outline-none"
                >
                  {title}
                </h2>
                <p className="mt-3 text-sm leading-relaxed text-muted-foreground">
                  {result
                    ? "Your company has been recorded in Kuubu. Workspace access is managed separately."
                    : step === "details"
                      ? "Choose the name you want displayed in your company workspace."
                      : step === "review"
                        ? "Check the details and confirm before sending your company request."
                        : "Choose the account that will cover your company’s Kuubu subscription."}
                </p>
              </div>
              {error ? (
                <p
                  role="alert"
                  className="rounded-lg border border-danger/30 bg-danger-soft p-3 text-sm leading-relaxed text-danger"
                >
                  {error}
                </p>
              ) : null}
              {loading ? (
                <p role="status" className="text-sm text-muted-foreground">
                  Checking your setup access…
                </p>
              ) : result ? (
                <>
                  <div className="rounded-lg border border-border bg-surface p-5">
                    <p className="text-sm text-muted-foreground">Company</p>
                    <p className="mt-1 break-words font-semibold">{companyName}</p>
                    <p className="mt-2 text-sm text-muted-foreground">
                      {result.status === "Existing"
                        ? "Your earlier request was confirmed. No additional company was created."
                        : "Saved successfully."}
                    </p>
                  </div>
                  <Button className="w-full" disabled={busy} onClick={() => void finish()}>
                    {busy ? "Closing setup…" : "Return to Kuubu"}
                  </Button>
                </>
              ) : !session || expired ? (
                <>
                  <p className="text-sm leading-relaxed text-muted-foreground">
                    {stored.status === "ready"
                      ? "Your previous company request is retained. Continue with the same Kuubu account to review and safely retry it."
                      : "Continue with your Kuubu account and authorize company setup. You’ll review the details before creating anything."}
                  </p>
                  <Button asChild className="w-full">
                    <a href={productSetupStart}>
                      Continue with Kuubu <ArrowRight className="size-4" aria-hidden="true" />
                    </a>
                  </Button>
                  {error && !expired ? (
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => setLoadVersion((v) => v + 1)}
                    >
                      Check again
                    </Button>
                  ) : null}
                </>
              ) : storageBlocked || identityMismatch ? (
                <>
                  <p className="text-sm leading-relaxed text-muted-foreground">
                    {identityMismatch
                      ? "A company request in this tab belongs to another Kuubu identity. Continue with that account to check its outcome before starting another request."
                      : "We need browser storage to retain your reviewed request safely. Enable storage for this site, or contact support if an earlier request needs checking."}
                  </p>
                  {identityMismatch ? (
                    <Button asChild className="w-full">
                      <a href={productSetupStart}>Continue with the original account</a>
                    </Button>
                  ) : (
                    <Button className="w-full" onClick={() => setLoadVersion((v) => v + 1)}>
                      Check again
                    </Button>
                  )}
                </>
              ) : step === "account" ? (
                <>
                  {payers.length === 0 ? (
                    <p className="text-sm leading-relaxed text-muted-foreground">
                      No eligible billing accounts were found. You need owner or administrator
                      access to an active Kuubu subscription with a company allowance.
                    </p>
                  ) : (
                    <div className="space-y-3">
                      <Label htmlFor="setup-payer">Billing account</Label>
                      <Select value={customerId} onValueChange={setCustomerId}>
                        <SelectTrigger id="setup-payer" className="min-h-11">
                          <SelectValue placeholder="Choose an account" />
                        </SelectTrigger>
                        <SelectContent>
                          {payers.map((payer) => (
                            <SelectItem key={payer.customerId} value={payer.customerId}>
                              {payer.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {selected ? (
                        <p className="text-sm text-muted-foreground">
                          Purchased allowance: {selected.workspaceLimit}{" "}
                          {selected.workspaceLimit === 1 ? "company" : "companies"}. Availability is
                          checked when you create.
                        </p>
                      ) : null}
                    </div>
                  )}
                  {cursor ? (
                    <Button
                      variant="outline"
                      className="w-full"
                      disabled={paging}
                      onClick={() => void loadMore()}
                    >
                      {paging ? "Loading accounts…" : "Load more accounts"}
                    </Button>
                  ) : null}
                  <Button
                    className="w-full"
                    disabled={!selected || !allowed}
                    onClick={() => {
                      setStep("details");
                      setError(null);
                    }}
                  >
                    Continue <ArrowRight className="size-4" aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    className="w-full"
                    onClick={() => setLoadVersion((v) => v + 1)}
                  >
                    Refresh accounts
                  </Button>
                </>
              ) : step === "details" ? (
                <form
                  className="space-y-5"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (validName) {
                      setCompanyName(name);
                      setConfirmed(false);
                      setStep("review");
                      setError(null);
                    }
                  }}
                >
                  <div className="space-y-3">
                    <Label htmlFor="setup-company-name">Company name</Label>
                    <Input
                      id="setup-company-name"
                      autoComplete="organization"
                      value={companyName}
                      maxLength={200}
                      required
                      onChange={(event) => setCompanyName(event.target.value)}
                      placeholder="Your company’s name"
                    />
                    <p className="text-sm text-muted-foreground">
                      Use the name you want displayed in your workspace.
                    </p>
                  </div>
                  <Button type="submit" className="w-full" disabled={!validName}>
                    Review company <ArrowRight className="size-4" aria-hidden="true" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    className="w-full"
                    onClick={() => setStep("account")}
                  >
                    Back to accounts
                  </Button>
                </form>
              ) : (
                <form onSubmit={(event) => void submit(event)} className="space-y-5">
                  <dl className="space-y-4 rounded-lg border border-border bg-surface p-5">
                    <div>
                      <dt className="text-sm text-muted-foreground">Company name</dt>
                      <dd className="mt-1 break-words font-medium">{companyName}</dd>
                    </div>
                    <div>
                      <dt className="text-sm text-muted-foreground">Billing account</dt>
                      <dd className="mt-1 break-words font-medium">
                        {selected?.name ?? "Checking eligible accounts"}
                      </dd>
                    </div>
                    {selected ? (
                      <div>
                        <dt className="text-sm text-muted-foreground">Purchased allowance</dt>
                        <dd className="mt-1 text-sm">
                          {selected.workspaceLimit}{" "}
                          {selected.workspaceLimit === 1 ? "company" : "companies"} in total
                        </dd>
                      </div>
                    ) : null}
                  </dl>
                  {attempt ? (
                    <p className="text-sm leading-relaxed text-muted-foreground">
                      This request may already have been saved. Retrying checks the same request and
                      cannot create an additional company.
                    </p>
                  ) : null}
                  {!selected ? (
                    <>
                      <p className="text-sm text-muted-foreground">
                        This billing account is not in the loaded eligible accounts. Load more
                        accounts or refresh your access before retrying.
                      </p>
                      {cursor ? (
                        <Button
                          type="button"
                          variant="outline"
                          disabled={paging}
                          onClick={() => void loadMore()}
                        >
                          Load more accounts
                        </Button>
                      ) : (
                        <Button
                          type="button"
                          variant="outline"
                          onClick={() => setLoadVersion((v) => v + 1)}
                        >
                          Refresh access
                        </Button>
                      )}
                    </>
                  ) : null}
                  <label className="flex min-h-11 items-start gap-3 text-sm leading-relaxed">
                    <Checkbox
                      className="mt-1 shrink-0"
                      checked={confirmed}
                      disabled={busy}
                      onCheckedChange={(value) => setConfirmed(value === true)}
                    />
                    <span>
                      I confirm these details and want to create this company under the selected
                      Kuubu subscription.
                    </span>
                  </label>
                  <Button
                    type="submit"
                    className="w-full"
                    disabled={!confirmed || !allowed || !selected || busy}
                  >
                    {busy
                      ? "Confirming your company…"
                      : attempt
                        ? "Retry reviewed request"
                        : "Create company"}
                  </Button>
                  {!attempt ? (
                    <Button
                      type="button"
                      variant="ghost"
                      className="w-full"
                      disabled={busy}
                      onClick={() => {
                        setConfirmed(false);
                        setStep("details");
                      }}
                    >
                      Edit details
                    </Button>
                  ) : null}
                </form>
              )}
              {session && !result && !busy ? (
                <p className="border-t border-border pt-4 text-xs leading-relaxed text-muted-foreground">
                  Setup authorization is time limited.{" "}
                  <a className="font-medium underline underline-offset-4" href={productSetupStart}>
                    Continue with Kuubu again
                  </a>{" "}
                  if it expires.
                </p>
              ) : null}
              {session && !result ? (
                <Button
                  variant="ghost"
                  className="w-full"
                  disabled={busy}
                  onClick={() => void finish()}
                >
                  {attempt ? "Leave setup and retain request" : "Cancel setup"}
                </Button>
              ) : null}
            </CardContent>
          </Card>
          <p className="mt-5 text-xs text-muted-foreground">
            Kuubu is a product of GreenWebb Technologies.
          </p>
        </section>
      </main>
    </div>
  );
}
