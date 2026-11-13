import { AlertCircle, Check, ChevronLeft, ChevronRight } from "lucide-react";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";

export interface FlowStep {
  id: string;
  title: string;
  /** Short answer to "what is this step for?" */
  purpose: string;
  render: () => ReactNode;
  optional?: boolean;
  /**
   * Optional per-step gate: returns null when the step is complete, or an
   * error message explaining what is still missing. While this returns a
   * message, the Continue button is disabled and a small inline hint is
   * shown — the user cannot reach the next step (or the final submit) until
   * the required fields are filled.
   */
  validate?: () => string | null;
}

/**
 * Guided step flow for create/change actions:
 * purpose > essentials > policy > evidence > review > submit > next steps.
 * The current step is remembered for this browser tab. Individual flows own
 * their field state and must not claim server-side draft persistence unless
 * they implement it explicitly.
 */
export function GuidedFlow({
  flowId,
  steps,
  onSubmit,
  submitLabel = "Submit",
  submitted,
  allowSkip,
  submitHint,
  busyLabel = "Submitting…",
}: {
  flowId: string;
  steps: FlowStep[];
  onSubmit: () => void | Promise<void>;
  submitLabel?: string;
  submitted?: ReactNode;
  allowSkip?: boolean;
  submitHint?: string;
  busyLabel?: string;
}) {
  const [index, setIndex] = useState(0);
  const [busy, setBusy] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  // Which step is currently showing a blocking validation message. Keys are
  // step indices; the message is recomputed live as the user types.
  const [stepError, setStepError] = useState<string | null>(null);

  useEffect(() => {
    const raw = sessionStorage.getItem(`flow:${flowId}`);
    if (raw) {
      const n = Number(raw);
      if (!Number.isNaN(n) && n < steps.length) setIndex(n);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [flowId]);

  // Recompute the current step's blocking error on every render so the hint
  // and Continue button stay in sync as the user types. Skipping optional
  // steps (and the review step, which has nothing to fill) is allowed.
  useEffect(() => {
    if (index >= steps.length - 1 || steps[index]?.optional) {
      setStepError(null);
      return;
    }
    const check = steps[index]?.validate?.();
    setStepError(check ?? null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  });

  useEffect(() => {
    sessionStorage.setItem(`flow:${flowId}`, String(index));
    setSavedAt(new Date().toLocaleTimeString());
  }, [index, flowId]);

  if (submitted) return <>{submitted}</>;

  const step = steps[index];
  const last = index === steps.length - 1;

  return (
    <div className="grid gap-6 lg:grid-cols-[16rem_minmax(0,1fr)]">
      <nav aria-label="Steps" className="rounded-lg border bg-surface p-4">
        <ol className="space-y-1">
          {steps.map((s, i) => {
            const done = i < index;
            const current = i === index;
            return (
              <li key={s.id}>
                <button
                  onClick={() => setIndex(i)}
                  aria-current={current ? "step" : undefined}
                  className={`flex w-full items-start gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors ${
                    current ? "bg-primary-soft font-medium text-primary" : "hover:bg-surface-muted"
                  }`}
                >
                  <span
                    aria-hidden
                    className={`mt-0.5 grid size-5 shrink-0 place-items-center rounded-full border text-[11px] ${
                      done ? "border-success bg-success-soft text-success" : "border-border-strong text-muted-foreground"
                    }`}
                  >
                    {done ? <Check className="size-3" /> : i + 1}
                  </span>
                  <span className="min-w-0">
                    <span className="block">{s.title}</span>
                    {s.optional ? <span className="text-xs text-muted-foreground">Optional</span> : null}
                  </span>
                </button>
              </li>
            );
          })}
        </ol>
        <p className="mt-3 border-t pt-3 text-xs text-muted-foreground">
          Step remembered for this tab{savedAt ? ` at ${savedAt}` : ""}. Finish before closing it.
        </p>
      </nav>

      <div className="rounded-lg border bg-surface p-5">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Step {index + 1} of {steps.length}
        </p>
        <h2 className="mt-1 text-lg font-semibold">{step.title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{step.purpose}</p>
        <div className="mt-5">{step.render()}</div>
        {stepError ? (
          <p className="mt-4 flex gap-2 rounded-md border border-warning/40 bg-warning-soft px-3 py-2.5 text-sm text-warning">
            <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>{stepError}</span>
          </p>
        ) : null}

        <div className="mt-6 flex flex-wrap items-center gap-2 border-t pt-4">
          <Button variant="outline" disabled={index === 0} onClick={() => setIndex((i) => i - 1)}>
            <ChevronLeft className="size-4" aria-hidden />
            Back
          </Button>
          {last ? (
            <Button
              disabled={busy || Boolean(stepError)}
              onClick={async () => {
                setSubmitError(null);setBusy(true);
                try { await onSubmit(); }
                catch (error) { setSubmitError(error instanceof Error ? error.message : "The save could not be confirmed. Check the list before trying again."); }
                finally { setBusy(false); }
              }}
            >
              {busy ? busyLabel : submitLabel}
            </Button>
          ) : (
            <Button disabled={Boolean(stepError)} onClick={() => setIndex((i) => i + 1)}>
              Continue
              <ChevronRight className="size-4" aria-hidden />
            </Button>
          )}
          {allowSkip && !last ? (
            <Button variant="ghost" onClick={() => setIndex(steps.length - 1)}>
              Skip to review
            </Button>
          ) : null}
          <span className="text-xs text-muted-foreground">
            {last ? submitHint ?? "Submitting sends this for review; you'll see what happens next." : "Nothing is submitted until the final step."}
          </span>
        </div>
        {submitError ? <p role="alert" className="mt-3 text-sm text-destructive">{submitError}</p> : null}
      </div>
    </div>
  );
}

export function NextSteps({
  reference,
  title,
  steps,
  actions,
}: {
  reference: string;
  title: string;
  steps: string[];
  actions?: ReactNode;
}) {
  return (
    <div className="rounded-lg border border-success/40 bg-success-soft p-6">
      <h2 className="text-lg font-semibold text-success">{title}</h2>
      <p className="mt-1 font-mono text-sm">{reference}</p>
      <h3 className="mt-4 text-sm font-semibold">What happens next</h3>
      <ol className="mt-2 list-inside list-decimal space-y-1 text-sm text-foreground">
        {steps.map((s) => (
          <li key={s}>{s}</li>
        ))}
      </ol>
      {actions ? <div className="mt-4 flex flex-wrap gap-2">{actions}</div> : null}
    </div>
  );
}
