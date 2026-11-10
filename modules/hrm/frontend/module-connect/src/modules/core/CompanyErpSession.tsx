import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { OperationalError, type OperationalCompany } from "./product-operational-api";
import {
  createErpSelectionAttempt,
  createErpSessionClient,
  type ErpModule,
  type ErpSelectionAttempt,
  type ErpSessionReview,
  type SelectedErpSession,
} from "./product-erp-session-api";

const api = createErpSessionClient();
const moduleNames = { hrm: "Human resources", procurement: "Procurement" };
function failureMessage(error: unknown) {
  if (error instanceof OperationalError) {
    if (error.status === 401) return "Your sign-in has expired. Continue with Kuubu again.";
    if (error.status === 403)
      return "Your permissions or subscription have changed. Review your access again.";
    if (error.status === 409)
      return "Your access changed while this request was pending. Review it again.";
    if (error.status === 429) return "Please wait a moment before trying again.";
  }
  return "We couldn’t confirm the result. Retry the same selection to check it safely.";
}

/** Self-service selection is separate from the owner's permission editor. */
export function CompanyErpSession({
  company,
  modules,
  csrf,
  onExpired,
  onBusy,
}: {
  company: OperationalCompany;
  modules: string[];
  csrf: string;
  onExpired: () => void;
  onBusy: (busy: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const [module, setModule] = useState<ErpModule | null>(null);
  const [review, setReview] = useState<ErpSessionReview | null>(null);
  const [selected, setSelected] = useState<SelectedErpSession | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const [endUnconfirmed, setEndUnconfirmed] = useState(false);
  const pending = useRef(false);
  const attempt = useRef<ErpSelectionAttempt | null>(null);
  const controller = useRef<AbortController | null>(null);
  const callbacks = useRef({ onExpired, onBusy });
  callbacks.current = { onExpired, onBusy };
  useEffect(
    () => () => {
      controller.current?.abort();
      callbacks.current.onBusy(false);
      void queryClient.cancelQueries();
      queryClient.clear();
    },
    [queryClient],
  );
  function begin() {
    if (pending.current) return null;
    pending.current = true;
    const request = new AbortController();
    controller.current = request;
    setBusy(true);
    callbacks.current.onBusy(true);
    setError(null);
    return request;
  }
  function finish(request: AbortController) {
    if (request.signal.aborted) return;
    pending.current = false;
    controller.current = null;
    setBusy(false);
    callbacks.current.onBusy(false);
  }
  function failure(error: unknown, request: AbortController, writing: boolean) {
    if (request.signal.aborted) return;
    const definitive =
      error instanceof OperationalError && [400, 401, 403, 409].includes(error.status);
    setError(
      writing
        ? failureMessage(error)
        : error instanceof OperationalError && error.status === 403
          ? "You do not currently have access to this module. Ask a company administrator."
          : "We couldn’t review your ERP access. Please try again.",
    );
    setSelected(null);
    if (definitive) {
      attempt.current = null;
      setReview(null);
      setUncertain(false);
    } else if (writing) setUncertain(true);
    if (error instanceof OperationalError && error.status === 401) callbacks.current.onExpired();
  }
  async function reviewModule(value: ErpModule) {
    const request = begin();
    if (!request) return;
    setModule(value);
    setReview(null);
    setSelected(null);
    setConfirmed(false);
    attempt.current = null;
    setUncertain(false);
    try {
      const result = await api.review(company, value, request.signal);
      if (!request.signal.aborted) setReview(result);
    } catch (error) {
      failure(error, request, false);
    } finally {
      finish(request);
    }
  }
  async function select() {
    if (!review || (review.scope === "company" && !confirmed)) return;
    const request = begin();
    if (!request) return;
    setSelected(null);
    try {
      await queryClient.cancelQueries();
      queryClient.clear();
      attempt.current ??= createErpSelectionAttempt(
        review,
        review.scope === "company" && confirmed,
      );
      const result = await api.select(attempt.current, csrf, request.signal);
      const current = await api.session(request.signal);
      if (
        current.companyId !== result.companyId ||
        current.module !== result.module ||
        current.scope !== review.scope ||
        current.locations.length !== review.locations.length ||
        current.locations.some((id) => !review.locations.includes(id))
      )
        throw new OperationalError(409);
      if (!request.signal.aborted) {
        setSelected(result);
        attempt.current = null;
        setUncertain(false);
      }
    } catch (error) {
      failure(error, request, true);
    } finally {
      finish(request);
    }
  }
  async function endSession() {
    const request = begin();
    if (!request) return;
    setSelected(null);
    setReview(null);
    attempt.current = null;
    setUncertain(false);
    setEndUnconfirmed(true);
    try {
      await queryClient.cancelQueries();
      queryClient.clear();
      await api.logout(csrf, request.signal);
      if (!request.signal.aborted) setEndUnconfirmed(false);
    } catch (error) {
      if (!request.signal.aborted) {
        setError("We couldn’t confirm that the company session ended. Try ending it again.");
        if (error instanceof OperationalError && error.status === 401)
          callbacks.current.onExpired();
      }
    } finally {
      finish(request);
    }
  }
  const available = (["hrm", "procurement"] as const).filter((value) => modules.includes(value));
  if (!available.length) return null;
  return (
    <section aria-label="Your ERP session" className="mt-6">
      <Card>
        <CardContent className="space-y-5 p-6">
          <div>
            <h2 className="text-lg font-semibold">Your ERP session</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Review your own assigned permissions before selecting a company session.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            {available.map((value) => (
              <Button
                key={value}
                variant="outline"
                disabled={busy || uncertain || endUnconfirmed}
                onClick={() => void reviewModule(value)}
              >
                Review {moduleNames[value]} access
              </Button>
            ))}
          </div>
          {busy && <p role="status">Checking your ERP session…</p>}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          {endUnconfirmed && (
            <Button variant="outline" disabled={busy} onClick={() => void endSession()}>
              Retry ending company session
            </Button>
          )}
          {review && !selected && (
            <div className="space-y-4">
              <h3 className="font-semibold">
                {company.name} · {moduleNames[review.module]}
              </h3>
              <p className="text-sm">
                {review.scope === "company"
                  ? "All company locations"
                  : `${review.locations.length} assigned location${review.locations.length === 1 ? "" : "s"}`}
              </p>
              <p className="text-sm text-muted-foreground">
                Assigned roles: {review.roles.map((value) => value.replaceAll("_", " ")).join(", ")}
              </p>
              {review.scope === "company" && (
                <label className="flex min-h-11 items-center gap-3 text-sm">
                  <input
                    type="checkbox"
                    checked={confirmed}
                    disabled={busy || uncertain}
                    onChange={(event) => setConfirmed(event.target.checked)}
                  />
                  I confirm access to all company locations.
                </label>
              )}
              <Button
                disabled={busy || (review.scope === "company" && !confirmed)}
                onClick={() => void select()}
              >
                {uncertain ? "Retry same selection" : "Select company session"}
              </Button>
            </div>
          )}
          {selected && (
            <div className="space-y-4" role="status">
              <h3 className="font-semibold">Company session selected</h3>
              <p className="text-sm">
                {company.name} · {moduleNames[selected.module]}
              </p>
              <p className="text-sm text-muted-foreground">
                Your selected scope is confirmed. ERP operations remain subject to their current
                permissions and availability.
              </p>
              <Button variant="outline" disabled={busy} onClick={() => void endSession()}>
                End company session
              </Button>
            </div>
          )}
          {!review && module && !busy && (
            <p className="text-sm text-muted-foreground">
              Review the module again to check your current permissions.
            </p>
          )}
        </CardContent>
      </Card>
    </section>
  );
}
