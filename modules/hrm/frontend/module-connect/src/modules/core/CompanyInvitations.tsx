import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Card, CardContent } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  createProductTeamClient,
  ProductTeamError,
  type InvitationCommand,
  type TeamCompany,
  type TeamInvitation,
  type TeamSession,
} from "./product-team-api";

const api = createProductTeamClient();
type Page = Awaited<ReturnType<typeof api.invitations>>;
const roles = { administrator: "Administrator", member: "Member" };
const states = {
  pending: "Awaiting acceptance",
  accepted: "Accepted",
  revoked: "Revoked",
  expired: "Expired",
};
const deliveryStates: Record<TeamInvitation["delivery"]["state"], string> = {
  queued: "Email queued",
  retrying: "Email retry scheduled",
  accepted: "Email submitted",
  suppressed: "Email delivery stopped",
  invalid: "Email delivery needs attention",
  exhausted: "Email delivery needs attention",
  "not-required": "No further email needed",
  unavailable: "Email status unavailable",
};
function errorMessage(error: unknown) {
  if (error instanceof ProductTeamError) {
    if (error.status === 401) return "Your team sign-in has expired. Continue with Kuubu again.";
    if (error.status === 403)
      return "Your company access or subscription has changed. Close and refresh your access.";
    if (error.status === 409)
      return "This invitation or request has changed. Close and refresh before continuing.";
    if (error.status === 400)
      return "Check the recipient and role, then close and review a new request.";
  }
  return "We couldn’t confirm the result. Retry this same reviewed request, or close and refresh.";
}

export function CompanyInvitations({
  company,
  session,
  disabled,
  onLockChange,
  onExpired,
}: {
  company: TeamCompany;
  session: TeamSession;
  disabled: boolean;
  onLockChange: (locked: boolean) => void;
  onExpired: (expired: boolean) => void;
}) {
  const [page, setPage] = useState<Page | null>(null);
  const [version, setVersion] = useState(0);
  const [loading, setLoading] = useState(true);
  const [paging, setPaging] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [review, setReview] = useState<"issue" | TeamInvitation | null>(null);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<TeamInvitation["role"]>("member");
  const [confirmed, setConfirmed] = useState(false);
  const [attempt, setAttempt] = useState<InvitationCommand | null>(null);
  const [busy, setBusy] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const sending = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    onLockChange(review !== null || busy);
  }, [review, busy, onLockChange]);
  useEffect(() => () => onLockChange(false), [onLockChange]);
  useEffect(() => {
    const request = new AbortController();
    controller.current = request;
    setLoading(true);
    setPage(null);
    setError(null);
    void api
      .invitations({ id: company.id, linkId: company.linkId }, undefined, request.signal)
      .then((value) => {
        if (!request.signal.aborted) setPage(value);
      })
      .catch((failure) => {
        if (!request.signal.aborted) {
          setError(errorMessage(failure));
          if (failure instanceof ProductTeamError && failure.status === 401) onExpired(true);
        }
      })
      .finally(() => {
        if (!request.signal.aborted) setLoading(false);
      });
    return () => request.abort();
  }, [company.id, company.linkId, session.csrf, version, onExpired]);

  function begin(value: "issue" | TeamInvitation) {
    if (disabled || busy || !page) return;
    setReview(value);
    setEmail("");
    setRole("member");
    setConfirmed(false);
    setAttempt(null);
    setReviewError(null);
  }
  function close() {
    if (sending.current) return;
    setReview(null);
    setAttempt(null);
    setConfirmed(false);
    setReviewError(null);
    setVersion((value) => value + 1);
  }
  async function more() {
    if (!page?.next || paging) return;
    const request = controller.current;
    if (!request || request.signal.aborted) return;
    setPaging(true);
    try {
      const next = await api.invitations(company, page.next, request.signal);
      if (!request.signal.aborted)
        setPage((previous) =>
          previous
            ? { ...next, invitations: [...previous.invitations, ...next.invitations] }
            : next,
        );
    } catch (failure) {
      if (!request.signal.aborted) {
        setPage(null);
        setError(errorMessage(failure));
        if (failure instanceof ProductTeamError && failure.status === 401) onExpired(true);
      }
    } finally {
      if (!request.signal.aborted) setPaging(false);
    }
  }
  async function submit() {
    if (!review || !page || !confirmed || sending.current || disabled) return;
    const command: InvitationCommand =
      attempt ??
      (review === "issue"
        ? {
            operation: "issue",
            companyId: company.id,
            linkId: company.linkId,
            key: crypto.randomUUID(),
            email: email.trim().toLowerCase(),
            role,
          }
        : {
            operation: "revoke",
            companyId: company.id,
            linkId: company.linkId,
            key: crypto.randomUUID(),
            invitationId: review.id,
            revision: review.revision,
          });
    sending.current = true;
    setBusy(true);
    setAttempt(command);
    setReviewError(null);
    try {
      await api.invitationCommand(command, session.csrf);
      if (mounted.current) {
        setReview(null);
        setAttempt(null);
        setConfirmed(false);
        setPage(null);
        setVersion((value) => value + 1);
        setNotice(
          command.operation === "issue"
            ? "Invitation created. Delivery has been requested; the person must accept before joining."
            : "Invitation revocation confirmed.",
        );
      }
    } catch (failure) {
      if (mounted.current) {
        setReviewError(errorMessage(failure));
        if (failure instanceof ProductTeamError && failure.status === 401) onExpired(true);
      }
    } finally {
      sending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  const validEmail =
    email.trim().length > 0 &&
    email.trim().length <= 254 &&
    /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
  return (
    <Card>
      <CardContent className="space-y-5 p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-xl font-semibold">Company invitations</h2>
            <p className="mt-2 max-w-xl text-sm text-muted-foreground">
              People must sign in and accept before joining. Company roles do not include billing or
              module access.
            </p>
          </div>
          {page ? (
            <div className="flex flex-wrap gap-3">
              <Button
                variant="outline"
                disabled={disabled || !!review || busy || loading || paging}
                onClick={() => setVersion((value) => value + 1)}
              >
                Refresh invitations
              </Button>
              <Button
                disabled={disabled || !!review || busy || loading}
                onClick={() => begin("issue")}
              >
                Invite teammate
              </Button>
            </div>
          ) : null}
        </div>
        {notice ? (
          <p role="status" className="text-sm text-muted-foreground">
            {notice}
          </p>
        ) : null}
        {error ? (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        ) : null}
        {loading ? (
          <p role="status">Loading company invitations…</p>
        ) : page ? (
          <>
            {page.invitations.length ? (
              <ul
                aria-label="Company invitations"
                className="divide-y divide-border rounded-lg border border-border"
              >
                {page.invitations.map((invitation) => (
                  <li
                    key={invitation.id}
                    className="flex flex-wrap items-center justify-between gap-4 p-4"
                  >
                    <div className="min-w-0">
                      <p className="break-all font-medium">{invitation.recipient}</p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {roles[invitation.role]} · {states[invitation.state]}
                      </p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {deliveryStates[invitation.delivery.state]}
                        {invitation.delivery.attempts > 0
                          ? ` · ${invitation.delivery.attempts} ${invitation.delivery.attempts === 1 ? "attempt" : "attempts"}`
                          : ""}
                      </p>
                      {invitation.delivery.acceptedAt ? (
                        <p className="mt-1 text-xs text-muted-foreground">
                          Submitted {new Date(invitation.delivery.acceptedAt).toLocaleString()};
                          inbox delivery is unconfirmed.
                        </p>
                      ) : invitation.delivery.retryAt ? (
                        <p className="mt-1 text-xs text-muted-foreground">
                          Next attempt {new Date(invitation.delivery.retryAt).toLocaleString()}.
                        </p>
                      ) : null}
                      <p className="mt-1 text-xs text-muted-foreground">
                        Expires {new Date(invitation.expiresAt).toLocaleString()}
                      </p>
                    </div>
                    {invitation.state === "pending" &&
                    (page.actorRole === "owner" || invitation.role === "member") ? (
                      <Button
                        variant="outline"
                        disabled={disabled || !!review || busy}
                        onClick={() => begin(invitation)}
                        aria-label={`Revoke invitation for ${invitation.recipient}`}
                      >
                        Revoke invitation
                      </Button>
                    ) : null}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">
                No company invitations have been created.
              </p>
            )}
            {page.next ? (
              <Button
                variant="outline"
                disabled={disabled || paging || !!review}
                onClick={() => void more()}
              >
                {paging ? "Loading…" : "Load more invitations"}
              </Button>
            ) : null}
          </>
        ) : (
          <Button
            variant="outline"
            disabled={disabled}
            onClick={() => setVersion((value) => value + 1)}
          >
            Refresh invitations
          </Button>
        )}
        <Dialog
          open={!!review}
          onOpenChange={(open) => {
            if (!open) close();
          }}
        >
          <DialogContent
            onEscapeKeyDown={(event) => {
              if (busy) event.preventDefault();
            }}
            onInteractOutside={(event) => {
              if (busy) event.preventDefault();
            }}
          >
            <DialogHeader>
              <DialogTitle>
                {review === "issue" ? "Review company invitation" : "Review invitation revocation"}
              </DialogTitle>
              <DialogDescription>{company.name}</DialogDescription>
            </DialogHeader>
            {review === "issue" ? (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="invitation-email">Recipient email</Label>
                  <Input
                    id="invitation-email"
                    type="email"
                    autoComplete="email"
                    maxLength={254}
                    autoFocus
                    value={email}
                    disabled={busy || !!attempt}
                    onChange={(event) => {
                      setEmail(event.target.value);
                      setConfirmed(false);
                    }}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="invitation-role">Company role</Label>
                  <Select
                    value={role}
                    disabled={busy || !!attempt}
                    onValueChange={(value) => {
                      setRole(value as TeamInvitation["role"]);
                      setConfirmed(false);
                    }}
                  >
                    <SelectTrigger id="invitation-role">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="member">Member</SelectItem>
                      {page?.actorRole === "owner" ? (
                        <SelectItem value="administrator">Administrator</SelectItem>
                      ) : null}
                    </SelectContent>
                  </Select>
                </div>
                <p className="text-sm text-muted-foreground">
                  The person will need a verified Kuubu account and must accept this company
                  invitation.
                </p>
              </div>
            ) : review ? (
              <p className="break-words text-sm">
                Revoke the invitation for {review.recipient} as {roles[review.role]}? This does not
                remove an already accepted membership.
              </p>
            ) : null}
            <div className="flex items-start gap-3">
              <Checkbox
                id="invitation-confirm"
                checked={confirmed}
                disabled={busy || !!attempt}
                onCheckedChange={(value) => setConfirmed(value === true)}
              />
              <Label htmlFor="invitation-confirm" className="text-sm leading-relaxed">
                I confirm this request for {company.name}.
              </Label>
            </div>
            {reviewError ? (
              <p role="alert" className="text-sm text-destructive">
                {reviewError}
              </p>
            ) : null}
            <DialogFooter>
              <Button variant="outline" disabled={busy} onClick={close}>
                Close and refresh
              </Button>
              <Button
                disabled={busy || !confirmed || (review === "issue" && !validEmail)}
                onClick={() => void submit()}
              >
                {busy
                  ? "Confirming…"
                  : attempt
                    ? "Retry reviewed invitation request"
                    : review === "issue"
                      ? "Create invitation"
                      : "Confirm revocation"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </CardContent>
    </Card>
  );
}
