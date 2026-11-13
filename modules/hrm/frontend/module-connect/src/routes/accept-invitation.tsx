import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { KuubuIdentity } from "@/platform/components/KuubuIdentity";
import {
  createInvitationClient,
  invitationStart,
  InvitationError,
  type InvitationAttempt,
  type InvitationReview,
  type InvitationSession,
} from "@/modules/core/product-invitation-api";

declare global {
  interface Window {
    __kuubuTakeInvitationFragment?: () => string | null;
  }
}

export const Route = createFileRoute("/accept-invitation")({
  head: () => ({
    meta: [
      { title: "Your company invitation — Kuubu" },
      { name: "robots", content: "noindex, nofollow" },
      { name: "referrer", content: "no-referrer" },
    ],
  }),
  component: AcceptInvitation,
});
const api = createInvitationClient();
const account = "https://www.kuubuerp.com/account/";
function message(error: unknown) {
  if (error instanceof InvitationError) {
    if (error.status === 401)
      return "Your invitation sign-in has expired. Reopen the invitation link to sign in again.";
    if (error.status === 403 || error.status === 409)
      return "This invitation is unavailable. It may have been accepted, revoked or expired, or your company access has changed. Open your account or contact the sender.";
    if (error.status === 400)
      return "This request could not be accepted. Reopen your invitation link and review it again.";
  }
  return "We couldn’t confirm the result. Please try again.";
}
function AcceptInvitation() {
  const secret = useRef<string | null>(null);
  const captured = useRef(false);
  const mounted = useRef(false);
  const pending = useRef(false);
  const [session, setSession] = useState<InvitationSession | null>(null);
  const [review, setReview] = useState<InvitationReview | null>(null);
  const [attempt, setAttempt] = useState<InvitationAttempt | null>(null);
  const attemptRef = useRef<InvitationAttempt | null>(null);
  const [hasLink, setHasLink] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [expired, setExpired] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [ended, setEnded] = useState(false);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    mounted.current = true;
    if (!captured.current) {
      captured.current = true;
      secret.current = window.__kuubuTakeInvitationFragment?.() ?? null;
      setHasLink(secret.current !== null);
    }
    return () => {
      mounted.current = false;
      queueMicrotask(() => {
        if (!mounted.current) secret.current = null;
      });
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    setLoading(true);
    setError("");
    setReview(null);
    setConfirmed(false);
    api
      .session(controller.signal)
      .then(async (value) => {
        if (!active) return;
        setSession(value);
        setExpired(false);
        if (value.verified && !secret.current) {
          // A fresh email link always starts an explicit new handoff, even if this
          // browser still has a grant for a different invitation.
          const row = await api.review(controller.signal);
          if (active) setReview(row);
        }
      })
      .catch((failure) => {
        if (active) setError(message(failure));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
      controller.abort();
    };
  }, [version]);
  useEffect(() => {
    if (!session) return;
    const time = Math.max(0, Date.parse(session.expiresAt) - Date.now());
    const timer = setTimeout(() => {
      setExpired(true);
      setConfirmed(false);
    }, time);
    return () => clearTimeout(timer);
  }, [session]);

  async function signIn() {
    if (pending.current || !secret.current) return;
    pending.current = true;
    setBusy(true);
    setError("");
    try {
      let value = await api.session();
      if (value.verified) {
        await api.cancel(value.csrf);
        value = await api.session();
      }
      if (value.verified) throw new InvitationError(409);
      await api.prepare(secret.current, value.csrf);
      secret.current = null;
      if (mounted.current) window.location.assign(invitationStart);
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function accept() {
    if (pending.current || !session || !review || expired || (!confirmed && !attemptRef.current))
      return;
    const current = attemptRef.current ?? {
      key: crypto.randomUUID(),
      revision: review.revision,
      invitationId: review.id,
    };
    attemptRef.current = current;
    setAttempt(current);
    pending.current = true;
    setBusy(true);
    setError("");
    try {
      await api.accept(current, session.csrf);
      if (mounted.current) {
        setAccepted(true);
        secret.current = null;
        setHasLink(false);
      }
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function end() {
    if (pending.current || !session) return;
    pending.current = true;
    setBusy(true);
    setError("");
    try {
      const value = await api.session();
      await api.cancel(value.csrf);
      if (mounted.current) {
        secret.current = null;
        setHasLink(false);
        setEnded(true);
        setSession(null);
      }
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  return (
    <main className="kuubu-product-shell min-h-dvh bg-background text-foreground">
      <header className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-7">
        <KuubuIdentity />
        <a href="https://www.kuubuerp.com/contact/" className="text-sm underline">
          Get help
        </a>
      </header>
      <section className="mx-auto max-w-xl px-5 pb-12 pt-7" aria-busy={busy || loading}>
        <p className="mb-3 text-sm text-muted-foreground">Your Kuubu workspace</p>
        <h1 className="text-2xl font-semibold">Your company invitation</h1>
        <p className="mb-7 mt-3 text-muted-foreground">
          Review who you’re joining and the role you’ve been offered.
        </p>
        <Card>
          <CardContent className="space-y-5 pt-6">
            {loading ? (
              <p role="status">Checking your invitation…</p>
            ) : ended ? (
              <p role="status">Your invitation sign-in has ended.</p>
            ) : accepted ? (
              <div className="space-y-3" role="status">
                <h2 className="text-lg font-semibold">Your invitation was accepted.</h2>
                <p>Your company manages access to its modules. Continue to your Kuubu account.</p>
              </div>
            ) : review ? (
              <>
                <div>
                  <p className="text-sm text-muted-foreground">Company</p>
                  <h2 className="break-words text-xl font-semibold">{review.companyName}</h2>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Your company role</p>
                  <p className="font-semibold">
                    {review.role === "administrator" ? "Administrator" : "Member"}
                  </p>
                </div>
                <p className="text-sm text-muted-foreground">
                  Module permissions are managed separately by your company.
                </p>
                <div className="flex items-start gap-3">
                  <Checkbox
                    id="accept-company-invitation"
                    className="mt-1 size-5 shrink-0"
                    checked={confirmed}
                    disabled={busy || expired || attempt !== null}
                    onCheckedChange={(value) => setConfirmed(value === true)}
                  />
                  <Label htmlFor="accept-company-invitation" className="min-h-12 leading-relaxed">
                    I confirm that I want to join {review.companyName} as{" "}
                    {review.role === "administrator" ? "an administrator" : "a member"}.
                  </Label>
                </div>
                {expired ? (
                  <p role="alert">
                    Your invitation sign-in has expired. Reopen the invitation link to sign in
                    again.
                  </p>
                ) : (
                  <Button
                    className="w-full"
                    disabled={busy || (!confirmed && !attempt)}
                    onClick={accept}
                  >
                    {busy
                      ? "Confirming…"
                      : attempt
                        ? "Retry the same acceptance request"
                        : "Accept company invitation"}
                  </Button>
                )}
              </>
            ) : !error ? (
              <div className="space-y-4">
                <p>
                  {hasLink
                    ? "Sign in with your Kuubu account to review this invitation. You will confirm the company and role before joining."
                    : "Open your invitation link to continue."}
                </p>
                {hasLink && (
                  <Button className="w-full" disabled={busy} onClick={signIn}>
                    {busy ? "Continuing…" : "Continue with Kuubu"}
                  </Button>
                )}
              </div>
            ) : null}
            {error && (
              <p role="alert" className="break-words text-sm">
                {error}
              </p>
            )}
            {!loading && !busy && !accepted && !ended && error && !attempt && (
              <Button
                variant="outline"
                className="w-full"
                onClick={() => setVersion((value) => value + 1)}
              >
                Try again
              </Button>
            )}
            {!loading && !ended && session && (
              <Button variant="outline" className="w-full" disabled={busy} onClick={end}>
                End invitation sign-in
              </Button>
            )}
            <a href={account} className="inline-flex min-h-12 items-center text-sm underline">
              Go to my Kuubu account
            </a>
          </CardContent>
        </Card>
      </section>
    </main>
  );
}
