import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogCancel,
} from "@/components/ui/alert-dialog";
import { OperationalError, type OperationalCompany } from "./product-operational-api";
import {
  createModuleAccessClient,
  type AccessChange,
  type AccessLocation,
  type AccessMember,
  type AccessModule,
  type AccessReview,
} from "./product-module-access-api";

const api = createModuleAccessClient();
const moduleNames = { hrm: "Human resources", procurement: "Procurement" };
function label(value: string) {
  if (value === "hrm-access") return "Use HR and payroll";
  if (value === "hrm-admin") return "Manage HR administration";
  if (value === "hr_ops") return "HR operations";
  if (value === "hr_admin") return "HR administrator";
  if (value === "procurement-access") return "Use Procurement";
  return value
    .replace(/^procurement-/, "")
    .replace(/[-_]/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}
function errorMessage(error: unknown) {
  if (error instanceof OperationalError) {
    if (error.status === 401) return "Your sign-in has expired. Continue with Kuubu again.";
    if (error.status === 403)
      return "Company access or the subscription has changed. Reload permissions before continuing.";
    if (error.status === 409)
      return "Permissions or company membership changed. Reload and review the latest settings.";
    if (error.status === 429) return "Please wait a moment before trying again.";
  }
  return "Permission management is temporarily unavailable. Please try again.";
}
function Choice({
  text,
  checked,
  disabled,
  onChange,
}: {
  text: string;
  checked: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="flex min-h-11 cursor-pointer items-start gap-3 py-3 text-sm">
      <Checkbox
        aria-label={text}
        checked={checked}
        disabled={disabled}
        onCheckedChange={(value) => onChange(value === true)}
        className="mt-0.5"
      />
      <span className="min-w-0 break-words">{text}</span>
    </label>
  );
}
function BranchChoices({
  company,
  selected,
  known,
  locked,
  onChange,
  onError,
  onInventory,
}: {
  company: OperationalCompany;
  selected: string[];
  known: AccessLocation[];
  locked: boolean;
  onChange: (ids: string[]) => void;
  onError: (error: unknown) => void;
  onInventory: (rows: AccessLocation[]) => void;
}) {
  const [rows, setRows] = useState<AccessLocation[]>([]);
  const [next, setNext] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const pending = new AbortController();
    controller.current = pending;
    setBusy(true);
    void api
      .locations(company, null, pending.signal)
      .then((page) => {
        if (!pending.signal.aborted) {
          setRows(page.rows);
          setNext(page.next);
          onInventory(page.rows);
        }
      })
      .catch((failure) => {
        if (!pending.signal.aborted) {
          setError(errorMessage(failure));
          onError(failure);
        }
      })
      .finally(() => {
        if (!pending.signal.aborted) {
          setBusy(false);
          controller.current = null;
        }
      });
    return () => {
      pending.abort();
      controller.current?.abort();
    };
  }, [company, onError, onInventory]);
  async function more() {
    if (controller.current || locked) return;
    const pending = new AbortController();
    controller.current = pending;
    setBusy(true);
    setError(null);
    try {
      const page = await api.locations(
        company,
        error && rows.length === 0 ? null : next,
        pending.signal,
      );
      if (!pending.signal.aborted) {
        setRows((previous) => [
          ...previous,
          ...page.rows.filter((row) => !previous.some((x) => x.id === row.id)),
        ]);
        setNext(page.next);
        onInventory(page.rows);
      }
    } catch (failure) {
      if (!pending.signal.aborted) {
        setError(errorMessage(failure));
        onError(failure);
      }
    } finally {
      if (!pending.signal.aborted) {
        setBusy(false);
        controller.current = null;
      }
    }
  }
  const all = [...rows, ...known.filter((row) => !rows.some((x) => x.id === row.id))];
  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">
        Select up to 128 branches. An empty selection gives no branch access.
      </p>
      {all.map((row) => (
        <Choice
          key={row.id}
          text={row.name ?? "Unavailable branch — remove this selection"}
          checked={selected.includes(row.id)}
          disabled={
            locked ||
            (!row.available && !selected.includes(row.id)) ||
            (selected.length >= 128 && !selected.includes(row.id))
          }
          onChange={(checked) =>
            onChange(checked ? [...selected, row.id] : selected.filter((id) => id !== row.id))
          }
        />
      ))}
      {busy && <p role="status">Loading branches…</p>}
      {!busy && !all.length && !error && (
        <p className="text-sm">No active branches are available.</p>
      )}
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {(next || error) && (
        <Button variant="outline" disabled={busy || locked} onClick={() => void more()}>
          {error ? "Retry branches" : "More branches"}
        </Button>
      )}
    </div>
  );
}

export function CompanyModuleAccess({
  company,
  modules,
  csrf,
  onExpired,
}: {
  company: OperationalCompany;
  modules: string[];
  csrf: string;
  onExpired: () => void;
}) {
  const available = modules.filter(
    (value): value is AccessModule => value === "hrm" || value === "procurement",
  );
  const [members, setMembers] = useState<AccessMember[]>([]);
  const [next, setNext] = useState<string | null>(null);
  const [memberId, setMemberId] = useState("");
  const [module, setModule] = useState<AccessModule>(available[0] ?? "hrm");
  const [review, setReview] = useState<AccessReview | null>(null);
  const [loading, setLoading] = useState(true);
  const [paging, setPaging] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const [directoryVersion, setDirectoryVersion] = useState(0);
  const [roles, setRoles] = useState<string[]>([]);
  const [actions, setActions] = useState<string[]>([]);
  const [scope, setScope] = useState<"company" | "locations">("locations");
  const [locations, setLocations] = useState<string[]>([]);
  const [locationOptions, setLocationOptions] = useState<AccessLocation[]>([]);
  const inventory = useCallback(
    (rows: AccessLocation[]) =>
      setLocationOptions((previous) => [
        ...previous.filter((row) => !rows.some((x) => x.id === row.id)),
        ...rows,
      ]),
    [],
  );
  const [allCompany, setAllCompany] = useState(false);
  const [confirmation, setConfirmation] = useState<AccessChange | null>(null);
  const [uncertain, setUncertain] = useState<AccessChange | null>(null);
  const [saving, setSaving] = useState(false);
  const pending = useRef(false);
  const lifetime = useRef<AbortController | null>(null);
  const pageRequest = useRef<AbortController | null>(null);
  const expired = useRef(onExpired);
  expired.current = onExpired;
  const onFailure = useRef((failure: unknown) => {
    if (failure instanceof OperationalError && failure.status === 401) expired.current();
  }).current;
  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => {
      controller.abort();
      pageRequest.current?.abort();
    };
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    setReview(null);
    setMemberId("");
    setMembers([]);
    setNext(null);
    void api
      .members(company, null, controller.signal)
      .then((page) => {
        if (!controller.signal.aborted) {
          setMembers(page.rows);
          setNext(page.next);
        }
      })
      .catch((failure) => {
        if (!controller.signal.aborted) {
          setError(errorMessage(failure));
          onFailure(failure);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [company, directoryVersion, onFailure]);
  useEffect(() => {
    const controller = new AbortController();
    setReview(null);
    setReviewing(Boolean(memberId));
    setAllCompany(false);
    setConfirmation(null);
    setUncertain(null);
    if (memberId)
      void api
        .review(company, memberId, module, controller.signal)
        .then((value) => {
          if (controller.signal.aborted) return;
          setReview(value);
          const grant = value.grant?.state === "granted" && !value.grant.stale ? value.grant : null;
          setRoles(grant?.roles.filter((key) => value.roles.includes(key)) ?? []);
          setActions(grant?.actions.filter((key) => value.actions.includes(key)) ?? []);
          setScope(grant?.scope === "company" ? "company" : "locations");
          setLocations(grant?.locations.map((row) => row.id) ?? []);
          setLocationOptions(value.grant?.locations ?? []);
        })
        .catch((failure) => {
          if (!controller.signal.aborted) {
            setError(errorMessage(failure));
            onFailure(failure);
          }
        })
        .finally(() => {
          if (!controller.signal.aborted) setReviewing(false);
        });
    return () => controller.abort();
  }, [company, memberId, module, version, onFailure]);
  async function moreMembers() {
    if (!next || pageRequest.current || pending.current) return;
    const controller = new AbortController();
    pageRequest.current = controller;
    setPaging(true);
    try {
      const page = await api.members(company, next, controller.signal);
      if (!controller.signal.aborted) {
        setMembers((previous) => [
          ...previous,
          ...page.rows.filter((row) => !previous.some((x) => x.id === row.id)),
        ]);
        setNext(page.next);
      }
    } catch (failure) {
      if (!controller.signal.aborted) {
        setError(errorMessage(failure));
        onFailure(failure);
      }
    } finally {
      if (!controller.signal.aborted) {
        setPaging(false);
        pageRequest.current = null;
      }
    }
  }
  const current = review?.member.id === memberId && review.module === module ? review : null;
  const locked = saving || uncertain !== null;
  const entryAction = module === "hrm" ? "hrm-access" : "procurement-access";
  const valid =
    current &&
    current.member.active &&
    current.member.identityAvailable &&
    actions.includes(entryAction) &&
    (module !== "hrm" ||
      (roles.length > 0 && (!actions.includes("hrm-admin") || roles.includes("hr_admin")))) &&
    (scope === "company"
      ? allCompany
      : locations.length > 0 &&
        locations.every((id) => locationOptions.some((row) => row.id === id && row.available)));
  function prepare(revoke: boolean) {
    if (!current || locked || (!revoke && !valid)) return;
    setError(null);
    setNotice(null);
    setConfirmation({
      company: { ...company },
      member: { ...current.member },
      module,
      expectedRevision: current.grant?.revision ?? 0,
      commandKey: crypto.randomUUID(),
      revoke,
      roles: revoke ? [] : [...roles],
      actions: revoke ? [] : [...actions],
      scope: revoke ? "none" : scope,
      locations: revoke || scope === "company" ? [] : [...locations],
      allCompanyConfirmed: !revoke && scope === "company" && allCompany,
    });
  }
  async function save(change: AccessChange) {
    if (pending.current || !lifetime.current) return;
    pending.current = true;
    setSaving(true);
    setConfirmation(null);
    setError(null);
    try {
      await api.save(change, csrf, lifetime.current.signal);
      if (!lifetime.current.signal.aborted) {
        setUncertain(null);
        setNotice("Permission decision recorded. The latest settings are shown below.");
        setVersion((value) => value + 1);
      }
    } catch (failure) {
      if (!lifetime.current.signal.aborted) {
        onFailure(failure);
        setError(errorMessage(failure));
        if (
          !(failure instanceof OperationalError) ||
          failure.status >= 500 ||
          failure.status === 429
        )
          setUncertain(change);
        else {
          setUncertain(null);
          setReview(null);
        }
      }
    } finally {
      pending.current = false;
      if (!lifetime.current.signal.aborted) setSaving(false);
    }
  }
  function reload() {
    if (saving) return;
    setUncertain(null);
    setError(null);
    setNotice(null);
    setVersion((value) => value + 1);
  }
  return (
    <section aria-label="Module permissions" className="mt-8 space-y-4">
      <h2 className="text-lg font-semibold">Module permissions</h2>
      <p className="max-w-3xl text-sm text-muted-foreground">
        Choose a team member, then assign the work they can do and the branches they can access.
        Company membership and subscription access are checked separately.
      </p>
      {notice && (
        <p role="status" className="text-sm">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {loading ? (
        <p role="status">Checking permission management…</p>
      ) : members.length === 0 ? (
        <Button variant="outline" onClick={() => setDirectoryVersion((value) => value + 1)}>
          Check permission management again
        </Button>
      ) : !available.length ? (
        <p>No supported modules are included in this subscription.</p>
      ) : (
        <Card>
          <CardContent className="space-y-6 p-6">
            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-2">
                <label id="permission-member-label" className="text-sm">
                  Team member
                </label>
                <Select
                  value={memberId}
                  onValueChange={(value) => {
                    setError(null);
                    setNotice(null);
                    setMemberId(value);
                  }}
                  disabled={locked}
                >
                  <SelectTrigger aria-labelledby="permission-member-label">
                    <SelectValue placeholder="Choose a team member" />
                  </SelectTrigger>
                  <SelectContent>
                    {members.map((row) => (
                      <SelectItem key={row.id} value={row.id}>
                        {row.name}
                        {!row.active ? " (membership inactive)" : ""}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <label id="permission-module-label" className="text-sm">
                  Module
                </label>
                <Select
                  value={module}
                  onValueChange={(value) => {
                    setError(null);
                    setNotice(null);
                    setModule(value as AccessModule);
                  }}
                  disabled={locked}
                >
                  <SelectTrigger aria-labelledby="permission-module-label">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {available.map((code) => (
                      <SelectItem key={code} value={code}>
                        {moduleNames[code]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {next && (
              <Button
                variant="outline"
                disabled={paging || locked}
                onClick={() => void moreMembers()}
              >
                {paging ? "Loading members…" : "More team members"}
              </Button>
            )}
            {reviewing ? (
              <p role="status">Reviewing current permissions…</p>
            ) : current ? (
              <>
                <div className="space-y-2">
                  <h3 className="font-semibold">
                    {current.member.name} · {moduleNames[module]}
                  </h3>
                  <p className="text-sm text-muted-foreground">
                    {current.grant
                      ? current.grant.state === "revoked"
                        ? "Module permissions were revoked."
                        : "Review the existing permissions before saving changes."
                      : "No module permissions have been assigned."}
                  </p>
                  {current.grant?.stale && (
                    <p role="alert" className="text-sm">
                      Membership or branch access changed. Choose and confirm permissions again.
                    </p>
                  )}
                  {(!current.member.active || !current.member.identityAvailable) && (
                    <p role="alert" className="text-sm">
                      This member is not currently available for new permissions. Existing
                      permissions can still be revoked.
                    </p>
                  )}
                </div>
                <div className="grid gap-6 md:grid-cols-2">
                  <fieldset
                    disabled={locked || !current.member.active || !current.member.identityAvailable}
                  >
                    <legend className="mb-2 text-sm font-semibold">Work roles</legend>
                    {current.roles.map((role) => (
                      <Choice
                        key={role}
                        text={label(role)}
                        checked={roles.includes(role)}
                        disabled={!roles.includes(role) && roles.length >= 16}
                        onChange={(checked) =>
                          setRoles((previous) =>
                            checked ? [...previous, role] : previous.filter((x) => x !== role),
                          )
                        }
                      />
                    ))}
                  </fieldset>
                  <fieldset
                    disabled={locked || !current.member.active || !current.member.identityAvailable}
                  >
                    <legend className="mb-2 text-sm font-semibold">Allowed actions</legend>
                    {current.actions.map((action) => (
                      <Choice
                        key={action}
                        text={label(action)}
                        checked={actions.includes(action)}
                        disabled={!actions.includes(action) && actions.length >= 256}
                        onChange={(checked) =>
                          setActions((previous) =>
                            checked ? [...previous, action] : previous.filter((x) => x !== action),
                          )
                        }
                      />
                    ))}
                  </fieldset>
                </div>
                <fieldset
                  disabled={locked || !current.member.active || !current.member.identityAvailable}
                  className="space-y-4"
                >
                  <legend className="text-sm font-semibold">Branch access</legend>
                  <Select
                    value={scope}
                    onValueChange={(value) => {
                      setScope(value as "company" | "locations");
                      setAllCompany(false);
                    }}
                  >
                    <SelectTrigger aria-label="Branch access">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="locations">Selected branches</SelectItem>
                      <SelectItem value="company">All company branches</SelectItem>
                    </SelectContent>
                  </Select>
                  {scope === "company" ? (
                    <Choice
                      text="Allow access to all current and future branches in this company"
                      checked={allCompany}
                      onChange={setAllCompany}
                    />
                  ) : (
                    <BranchChoices
                      key={memberId + module}
                      company={company}
                      selected={locations}
                      known={current.grant?.locations ?? []}
                      locked={locked || !current.member.active || !current.member.identityAvailable}
                      onChange={setLocations}
                      onError={onFailure}
                      onInventory={inventory}
                    />
                  )}
                </fieldset>
                {!actions.includes(entryAction) && (
                  <p className="text-sm text-muted-foreground">
                    Select “{label(entryAction)}” to allow entry to this module.
                  </p>
                )}
                {module === "hrm" &&
                  actions.includes("hrm-admin") &&
                  !roles.includes("hr_admin") && (
                    <p className="text-sm">
                      HR administration also requires the HR administrator work role.
                    </p>
                  )}
                <div className="flex flex-wrap gap-3">
                  <Button disabled={locked || !valid} onClick={() => prepare(false)}>
                    Review permissions
                  </Button>
                  {current.grant?.state === "granted" && (
                    <Button variant="outline" disabled={locked} onClick={() => prepare(true)}>
                      Revoke module permissions
                    </Button>
                  )}
                </div>
              </>
            ) : memberId ? (
              <Button variant="outline" disabled={locked} onClick={reload}>
                Reload permissions
              </Button>
            ) : null}
            {saving && <p role="status">Recording the permission decision…</p>}
            {uncertain && (
              <div className="space-y-3">
                <p className="text-sm">
                  We couldn’t confirm whether the save finished. Retry this same reviewed change, or
                  reload current permissions.
                </p>
                <div className="flex flex-wrap gap-3">
                  <Button disabled={saving} onClick={() => void save(uncertain)}>
                    Retry reviewed change
                  </Button>
                  <Button variant="outline" disabled={saving} onClick={reload}>
                    Reload current permissions
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}
      <AlertDialog
        open={confirmation !== null}
        onOpenChange={(open) => {
          if (!open) setConfirmation(null);
        }}
      >
        <AlertDialogContent className="max-h-[85dvh] overflow-y-auto">
          <AlertDialogTitle>
            {confirmation?.revoke ? "Revoke module permissions?" : "Confirm module permissions"}
          </AlertDialogTitle>
          <AlertDialogDescription>
            Review this change for {confirmation?.member.name} at {company.name}. It records
            permissions; ERP entry is checked separately.
          </AlertDialogDescription>
          {confirmation && (
            <div className="space-y-3 text-sm">
              <p>
                <strong>{moduleNames[confirmation.module]}</strong>
              </p>
              {confirmation.revoke ? (
                <p>Remove this member’s module permissions for every branch in this company.</p>
              ) : (
                <>
                  <p>Roles: {confirmation.roles.map(label).join(", ") || "No work role"}</p>
                  <p>Actions: {confirmation.actions.map(label).join(", ")}</p>
                  <p>
                    Branches:{" "}
                    {confirmation.scope === "company"
                      ? "All current and future company branches"
                      : `${confirmation.locations.length} selected branches`}
                  </p>
                  {confirmation.scope === "locations" && (
                    <ul className="list-disc space-y-1 pl-5">
                      {confirmation.locations.map((id) => (
                        <li key={id}>
                          {locationOptions.find((row) => row.id === id)?.name ??
                            "Unavailable branch"}
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}
            </div>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel>Back</AlertDialogCancel>
            <Button
              onClick={() => {
                if (confirmation) void save(confirmation);
              }}
            >
              {confirmation?.revoke ? "Confirm revocation" : "Confirm permissions"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  );
}
