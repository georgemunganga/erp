import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
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
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { KuubuIdentity } from "@/platform/components/KuubuIdentity";
import { CompanyInvitations } from "@/modules/core/CompanyInvitations";
import {
  createProductTeamClient,
  productTeamStart,
  ProductTeamError,
  type TeamCommand,
  type TeamCompany,
  type TeamMember,
  type TeamRole,
  type TeamSession,
} from "@/modules/core/product-team-api";

export const Route = createFileRoute("/team")({
  head: () => ({
    meta: [
      { title: "Your company team — Kuubu" },
      { name: "robots", content: "noindex, nofollow" },
      { name: "referrer", content: "no-referrer" },
    ],
  }),
  component: CompanyTeam,
});
const api = createProductTeamClient();
const labels: Record<TeamRole, string> = {
  owner: "Owner",
  administrator: "Administrator",
  member: "Member",
};
type MemberPage = Awaited<ReturnType<typeof api.members>> & { companyId: string; linkId: string };
type Review = { member: TeamMember; operation: "role-change" | "revoke" };
function message(error: unknown) {
  if (error instanceof ProductTeamError) {
    if (error.status === 401) return "Your team sign-in has expired. Continue with Kuubu again.";
    if (error.status === 403)
      return "Your company access or subscription has changed. Refresh before making another change.";
    if (error.code === "LastOwner")
      return "Keep another active owner before removing or changing this owner.";
    if (error.status === 409)
      return "This team record has changed. Close this review and refresh the current team.";
    if (error.status === 400)
      return "This request could not be accepted. Close this review and refresh the current team.";
  }
  return "We couldn’t confirm the result. Retry the same reviewed request, or close and refresh the team.";
}

function memberStatus(member: TeamMember) {
  return !member.active ? "Revoked" : !member.identityAvailable ? "Identity unavailable" : "Active";
}
function MemberActions({
  member,
  actorRole,
  disabled,
  onReview,
}: {
  member: TeamMember;
  actorRole: "owner" | "administrator";
  disabled: boolean;
  onReview: (member: TeamMember, operation: Review["operation"]) => void;
}) {
  return (
    <div className="flex flex-wrap gap-2 md:justify-end">
      {actorRole === "owner" && member.active && member.identityAvailable ? (
        <Button
          variant="outline"
          disabled={disabled}
          aria-label={`Change role for ${member.name}`}
          onClick={() => onReview(member, "role-change")}
        >
          Change role
        </Button>
      ) : null}
      {member.active && (actorRole === "owner" || member.role === "member") ? (
        <Button
          variant="outline"
          disabled={disabled}
          aria-label={`Revoke access for ${member.name}`}
          onClick={() => onReview(member, "revoke")}
        >
          Revoke
        </Button>
      ) : null}
    </div>
  );
}

function CompanyTeam() {
  const [session, setSession] = useState<TeamSession | null>(null);
  const [companies, setCompanies] = useState<TeamCompany[]>([]);
  const [companyCursor, setCompanyCursor] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState("");
  const [members, setMembers] = useState<MemberPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [memberLoading, setMemberLoading] = useState(false);
  const [paging, setPaging] = useState(false);
  const [companyPaging, setCompanyPaging] = useState(false);
  const [loadVersion, setLoadVersion] = useState(0);
  const [memberVersion, setMemberVersion] = useState(0);
  const [expired, setExpired] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [review, setReview] = useState<Review | null>(null);
  const [newRole, setNewRole] = useState<TeamRole>("member");
  const [confirmed, setConfirmed] = useState(false);
  const [attempt, setAttempt] = useState<TeamCommand | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [invitationLocked, setInvitationLocked] = useState(false);
  const sending = useRef(false);
  const mounted = useRef(true);
  const memberController = useRef<AbortController | null>(null);
  const companyController = useRef<AbortController | null>(null);
  const selected = companies.find((company) => company.id === selectedId);
  const selectedLink = selected?.linkId ?? "";
  const current =
    members?.companyId === selectedId && members.linkId === selectedLink ? members : null;

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    companyController.current = controller;
    setLoading(true);
    setSession(null);
    setCompanies([]);
    setMembers(null);
    setSelectedId("");
    setExpired(false);
    setUnavailable(false);
    setError(null);
    setCompanyPaging(false);
    void (async () => {
      try {
        const proof = await api.session(controller.signal);
        const page = await api.companies(undefined, controller.signal);
        if (controller.signal.aborted) return;
        setSession(proof);
        setCompanies(page.companies);
        setCompanyCursor(page.next);
        setSelectedId(page.companies[0]?.id ?? "");
      } catch (failure) {
        if (controller.signal.aborted) return;
        if (failure instanceof ProductTeamError && failure.status === 401) setExpired(true);
        else {
          setError(message(failure));
          setUnavailable(failure instanceof ProductTeamError && failure.status === 503);
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [loadVersion]);
  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(
      () => setExpired(true),
      Math.max(0, Date.parse(session.expiresAt) - Date.now()),
    );
    return () => window.clearTimeout(timer);
  }, [session]);
  useEffect(() => {
    const controller = new AbortController();
    memberController.current = controller;
    setMembers(null);
    setPaging(false);
    setMemberLoading(false);
    if (!selectedId || !selectedLink || !session || expired) return () => controller.abort();
    setMemberLoading(true);
    setError(null);
    void api
      .members({ id: selectedId, linkId: selectedLink }, undefined, controller.signal)
      .then((page) => {
        if (!controller.signal.aborted)
          setMembers({ ...page, companyId: selectedId, linkId: selectedLink });
      })
      .catch((failure) => {
        if (!controller.signal.aborted) {
          setError(message(failure));
          if (failure instanceof ProductTeamError && failure.status === 401) setExpired(true);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setMemberLoading(false);
      });
    return () => controller.abort();
  }, [selectedId, selectedLink, session, memberVersion, expired]);

  async function moreCompanies() {
    const signal = companyController.current?.signal;
    if (!companyCursor || !signal || signal.aborted || companyPaging) return;
    setCompanyPaging(true);
    setError(null);
    try {
      const page = await api.companies(companyCursor, signal);
      if (!signal.aborted) {
        setCompanies((previous) => [
          ...new Map(
            [...previous, ...page.companies].map((company) => [company.id, company]),
          ).values(),
        ]);
        setCompanyCursor(page.next);
      }
    } catch (failure) {
      if (!signal.aborted) setError(message(failure));
    } finally {
      if (!signal.aborted) setCompanyPaging(false);
    }
  }
  async function moreMembers() {
    const signal = memberController.current?.signal;
    if (!selected || !current?.next || !signal || signal.aborted || paging) return;
    setPaging(true);
    setError(null);
    try {
      const page = await api.members(selected, current.next, signal);
      if (!signal.aborted)
        setMembers((previous) =>
          previous
            ? {
                ...previous,
                actorRole: page.actorRole,
                next: page.next,
                members: [
                  ...new Map(
                    [...previous.members, ...page.members].map((member) => [member.id, member]),
                  ).values(),
                ],
              }
            : null,
        );
    } catch (failure) {
      if (!signal.aborted) {
        setError(message(failure));
        setMembers(null);
      }
    } finally {
      if (!signal.aborted) setPaging(false);
    }
  }
  function begin(member: TeamMember, operation: Review["operation"]) {
    setReview({ member, operation });
    setNewRole(member.role);
    setConfirmed(false);
    setAttempt(null);
    setReviewError(null);
  }
  function closeReview() {
    if (sending.current) return;
    setReview(null);
    setAttempt(null);
    setConfirmed(false);
    setReviewError(null);
    setMembers(null);
    setMemberVersion((value) => value + 1);
  }
  async function submit() {
    if (!session || !selected || !review || !confirmed || expired || sending.current) return;
    const command = attempt ?? {
      companyId: selected.id,
      linkId: selected.linkId,
      memberId: review.member.id,
      key: crypto.randomUUID(),
      revision: review.member.revision,
      ...(review.operation === "revoke"
        ? { operation: "revoke" as const }
        : { operation: "role-change" as const, role: newRole }),
    };
    sending.current = true;
    setBusy(true);
    setAttempt(command);
    setReviewError(null);
    try {
      await api.execute(command, session.csrf);
      if (mounted.current) {
        setReview(null);
        setAttempt(null);
        setConfirmed(false);
        setMembers(null);
        setNotice("Request acknowledged. Refreshing the current team.");
        setMemberVersion((value) => value + 1);
      }
    } catch (failure) {
      if (mounted.current) {
        setReviewError(message(failure));
        if (failure instanceof ProductTeamError && failure.status === 401) setExpired(true);
      }
    } finally {
      sending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function cancel() {
    if (!session || sending.current) return;
    sending.current = true;
    setBusy(true);
    try {
      await api.cancel(session.csrf);
      if (mounted.current) {
        setSession(null);
        setCompanies([]);
        setMembers(null);
        setExpired(true);
        setError(null);
      }
    } catch (failure) {
      if (mounted.current) setError(message(failure));
    } finally {
      sending.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  const ready = !!session && !expired;
  return (
    <div className="kuubu-product-shell min-h-dvh bg-background text-foreground">
      <header className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-7 md:px-10">
        <KuubuIdentity />
        <a className="text-sm underline underline-offset-4" href="mailto:hello@greenwebb.tech">
          Get help
        </a>
      </header>
      <main className="mx-auto max-w-6xl space-y-6 px-5 pb-12 pt-6 md:px-10">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="mb-2 text-sm text-muted-foreground">Your Kuubu workspace</p>
            <h1 className="text-2xl font-semibold">Your company team</h1>
            <p className="mt-2 max-w-xl text-sm text-muted-foreground">
              Manage who belongs to your company. Roles for accounting, payroll and other modules
              are managed separately.
            </p>
          </div>
          {ready ? (
            <div className="flex gap-2">
              <Button
                variant="outline"
                disabled={invitationLocked || busy || loading || !!review}
                onClick={() => setLoadVersion((value) => value + 1)}
              >
                Refresh
              </Button>
              <Button
                variant="outline"
                disabled={invitationLocked || busy || !!review}
                onClick={() => void cancel()}
              >
                End team sign-in
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
          <p role="status">Loading your team access…</p>
        ) : !ready ? (
          <Card>
            <CardContent className="space-y-4 p-6 md:p-8">
              <h2 className="text-xl font-semibold">
                {unavailable
                  ? "Team management is unavailable"
                  : "Continue with your Kuubu account"}
              </h2>
              <p className="max-w-lg text-sm text-muted-foreground">
                {unavailable
                  ? "Please try again later or contact support."
                  : "Sign in to view the companies you manage and review team changes."}
              </p>
              {!unavailable ? (
                <Button className="rounded-full px-6" asChild>
                  <a href={productTeamStart}>Continue with Kuubu</a>
                </Button>
              ) : null}
            </CardContent>
          </Card>
        ) : (
          <>
            <Card>
              <CardContent className="space-y-4 p-6">
                <Label htmlFor="team-company">Company</Label>
                {companies.length ? (
                  <Select
                    value={selectedId}
                    onValueChange={setSelectedId}
                    disabled={invitationLocked || busy || !!review}
                  >
                    <SelectTrigger id="team-company" className="min-h-11 max-w-xl">
                      <SelectValue placeholder="Choose a company" />
                    </SelectTrigger>
                    <SelectContent>
                      {companies.map((company) => (
                        <SelectItem key={company.id} value={company.id}>
                          {company.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    No companies with owner or administrator access were found.
                  </p>
                )}
                {companyCursor ? (
                  <Button
                    variant="outline"
                    disabled={invitationLocked || busy || companyPaging || !!review}
                    onClick={() => void moreCompanies()}
                  >
                    {companyPaging ? "Loading…" : "Load more companies"}
                  </Button>
                ) : null}
                <p className="text-sm text-muted-foreground">
                  A company’s subscription and your current access are checked before its team is
                  shown.
                </p>
              </CardContent>
            </Card>
            {memberLoading ? (
              <p role="status">Loading the current company team…</p>
            ) : current ? (
              <Card>
                <CardContent className="p-0">
                  <div className="border-b border-border p-6">
                    <h2 className="text-xl font-semibold">{selected?.name}</h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                      Your role: {labels[current.actorRole]}
                    </p>
                  </div>
                  <ul
                    className="divide-y divide-border md:hidden"
                    aria-label="Company team members"
                  >
                    {current.members.map((member) => (
                      <li key={member.id} className="space-y-4 p-6">
                        <div>
                          <p className="font-medium">{member.name}</p>
                          <p className="mt-1 text-sm text-muted-foreground">
                            {labels[member.role]} · {memberStatus(member)}
                          </p>
                        </div>
                        <MemberActions
                          member={member}
                          actorRole={current.actorRole}
                          disabled={invitationLocked || busy || !!review}
                          onReview={begin}
                        />
                      </li>
                    ))}
                  </ul>
                  <div className="hidden overflow-x-auto md:block">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Name</TableHead>
                          <TableHead>Company role</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>
                            <span className="sr-only">Actions</span>
                          </TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {current.members.map((member) => (
                          <TableRow key={member.id}>
                            <TableCell className="font-medium">{member.name}</TableCell>
                            <TableCell>{labels[member.role]}</TableCell>
                            <TableCell>{memberStatus(member)}</TableCell>
                            <TableCell>
                              <MemberActions
                                member={member}
                                actorRole={current.actorRole}
                                disabled={invitationLocked || busy || !!review}
                                onReview={begin}
                              />
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                  {!current.members.length ? (
                    <p className="p-6 text-sm text-muted-foreground">
                      No team memberships were found.
                    </p>
                  ) : null}
                  {current.next ? (
                    <div className="border-t border-border p-6">
                      <Button
                        variant="outline"
                        disabled={invitationLocked || busy || paging || !!review}
                        onClick={() => void moreMembers()}
                      >
                        {paging ? "Loading…" : "Load more members"}
                      </Button>
                    </div>
                  ) : null}
                </CardContent>
              </Card>
            ) : null}
            {session?.invitationManagementAvailable && selected && current ? (
              <CompanyInvitations
                key={`${selected.id}:${selected.linkId}`}
                company={selected}
                session={session}
                disabled={busy || !!review || expired}
                onLockChange={setInvitationLocked}
                onExpired={setExpired}
              />
            ) : null}
          </>
        )}
        <Dialog
          open={!!review}
          onOpenChange={(open) => {
            if (!open) closeReview();
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
                {review?.operation === "revoke"
                  ? "Review access revocation"
                  : "Review company role"}
              </DialogTitle>
              <DialogDescription>
                {selected?.name} · {review?.member.name}
              </DialogDescription>
            </DialogHeader>
            <p className="text-sm">
              Current company role: {review ? labels[review.member.role] : ""}
            </p>
            {review?.operation === "role-change" ? (
              <div className="space-y-2">
                <Label htmlFor="team-new-role">New company role</Label>
                <Select
                  value={newRole}
                  disabled={busy || !!attempt}
                  onValueChange={(value) => {
                    setNewRole(value as TeamRole);
                    setConfirmed(false);
                  }}
                >
                  <SelectTrigger id="team-new-role" className="min-h-11">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(labels).map(([value, label]) => (
                      <SelectItem key={value} value={value}>
                        {label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : (
              <p className="text-sm">
                This removes this person’s company membership access. Their account and retained
                business records remain.
              </p>
            )}
            <p className="text-sm text-muted-foreground">
              Owners manage company roles. Administrators can remove ordinary members. At least one
              active owner must remain.
            </p>
            <div className="flex items-start gap-3">
              <Checkbox
                id="team-confirm"
                checked={confirmed}
                disabled={busy || !!attempt}
                onCheckedChange={(value) => setConfirmed(value === true)}
              />
              <Label htmlFor="team-confirm" className="text-sm leading-relaxed">
                I confirm this change for {review?.member.name} in {selected?.name}.
              </Label>
            </div>
            {reviewError ? (
              <p role="alert" className="text-sm text-destructive">
                {reviewError}
              </p>
            ) : null}
            {expired ? (
              <a className="text-sm underline" href={productTeamStart}>
                Continue with Kuubu again
              </a>
            ) : null}
            <DialogFooter>
              <Button variant="outline" disabled={busy} onClick={closeReview}>
                Close and refresh
              </Button>
              <Button
                className="rounded-full px-6"
                disabled={!confirmed || busy || expired}
                onClick={() => void submit()}
              >
                {busy ? "Saving…" : attempt ? "Retry reviewed request" : "Confirm change"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </main>
    </div>
  );
}
