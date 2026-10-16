import { createFileRoute, Navigate } from "@tanstack/react-router";
import { Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { EmptyState } from "@/platform/components/States";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

const description =
  "Confinement control. Assigning a platform user to a branch permanently narrows their work scope to that branch: the organisation switcher only offers it, and every create and list executes under it. Operators without any assignment (top-level HR) stay organisation-wide. Configuration is never scoped.";

export const Route = createFileRoute("/hrm/configuration/branch-access")({
  head: () => ({
    meta: [
      { title: "Branch access — Newworldcargo HRM" },
      { name: "description", content: description },
      { property: "og:title", content: "Branch access — Newworldcargo HRM" },
    ],
  }),
  component: () => <Navigate to="/core/branch-access" replace />,
});

export function BranchAccessPage() {
  const accessState = useApi(() => realApi.branchAccess());
  const [assigning, setAssigning] = useState(false);

  const reload = () => accessState.reload();

  return (
    <div className="mx-auto max-w-5xl px-4 py-6 md:px-8">
      <PageHeader
        title="Branch access"
        description={description}
        meta={<ScopeBadge />}
        actions={
          <Button size="sm" onClick={() => setAssigning(true)}>
            <Plus className="size-4" aria-hidden /> Assign branch
          </Button>
        }
      />

      <Async state={accessState} rows={6}>
        {accessState.data ? (
          <AccessTable data={accessState.data} reload={reload} />
        ) : (
          <EmptyState title="No branch access data" description="Could not load assignments." />
        )}
      </Async>

      {assigning && accessState.data ? (
        <AssignDialog
          locations={accessState.data.locations}
          onDone={() => {
            setAssigning(false);
            reload();
          }}
          onCancel={() => setAssigning(false)}
        />
      ) : null}
    </div>
  );
}

function AccessTable({
  data,
  reload,
}: {
  data: {
    items: { id: string; userId: string; userEmail: string; locationId: string; locationName?: string | null }[];
    locations: { id: string; name: string; legalEntityId: string }[];
  };
  reload: () => void;
}) {
  const [removing, setRemoving] = useState<string | null>(null);
  const byLocation = useMemo(
    () => Object.fromEntries(data.locations.map((l) => [l.id, `${l.name}`])),
    [data.locations],
  );

  if (data.items.length === 0) {
    return (
      <div className="rounded-lg border bg-card p-8">
        <EmptyState
          title="No confined HR staff"
          description={
            "No platform user has been assigned to a branch yet, which means every HR staff member " +
            "currently works organisation-wide. Assign a user below to confine them to a specific branch."
          }
        />
      </div>
    );
  }

  return (
    <div className="rounded-lg border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Platform user</TableHead>
            <TableHead>User identifier</TableHead>
            <TableHead>Assigned branch</TableHead>
            <TableHead className="w-20" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.items.map((row) => (
            <TableRow key={row.id}>
              <TableCell className="font-medium">{row.userEmail || "—"}</TableCell>
              <TableCell className="text-muted-foreground">
                <code className="text-xs">{row.userId}</code>
              </TableCell>
              <TableCell>{byLocation[row.locationId] ?? row.locationId}</TableCell>
              <TableCell>
                <Button
                  variant="ghost"
                  size="icon"
                  className="size-7 text-destructive hover:text-destructive"
                  title="Remove assignment — user returns to organisation-wide scope"
                  disabled={removing === row.id}
                  onClick={async () => {
                    setRemoving(row.id);
                    try {
                      await realApi.removeBranchAccess(row.id);
                      feedback.submitted("Assignment removed", "User works organisation-wide again.");
                      reload();
                    } catch (err) {
                      feedback.blocked("Failed to remove assignment", err instanceof Error ? err.message : "Unknown error");
                    } finally {
                      setRemoving(null);
                    }
                  }}
                >
                  <Trash2 className="size-3.5" aria-hidden />
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function AssignDialog({
  locations,
  onDone,
  onCancel,
}: {
  locations: { id: string; name: string; legalEntityId: string }[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [userId, setUserId] = useState("");
  const [userEmail, setUserEmail] = useState("");
  const [locationId, setLocationId] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      role="dialog"
      aria-modal="true"
    >
      <div className="w-full max-w-md rounded-lg border bg-card p-6 shadow-lg">
        <h2 className="text-lg font-semibold">Assign a branch</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          The user will only ever be able to work inside the chosen branch. Their requests always
          carry that scope, even if they try to pick something else in the switcher.
        </p>
        <div className="mt-4 space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium" htmlFor="ba-user-id">
              User identifier (Keycloak subject)
            </label>
            <Input
              id="ba-user-id"
              placeholder="e.g. 019ffa91-…-e14865"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              autoComplete="off"
            />
            <p className="text-xs text-muted-foreground">
              Paste the subject claim from the user's Keycloak token (or use the same id as their
              worker record's account link).
            </p>
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium" htmlFor="ba-user-email">
              Email (display hint)
            </label>
            <Input
              id="ba-user-email"
              placeholder="e.g. jane@newworldcargo.com"
              value={userEmail}
              onChange={(e) => setUserEmail(e.target.value)}
              autoComplete="off"
            />
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium">Branch</label>
            <Select value={locationId} onValueChange={(v) => setLocationId(v)}>
              <SelectTrigger>
                <SelectValue placeholder="Choose a branch…" />
              </SelectTrigger>
              <SelectContent>
                {locations.map((l) => (
                  <SelectItem key={l.id} value={l.id}>
                    {l.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <div className="mt-6 flex justify-end gap-2">
          <Button variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button
            disabled={busy || !userId.trim() || !locationId}
            onClick={async () => {
              setBusy(true);
              try {
                await realApi.assignBranchAccess({ userId: userId.trim(), userEmail: userEmail.trim() || undefined, locationId: locationId! });
                feedback.submitted("Branch access assigned", "User is confined to that branch.");
                onDone();
              } catch (err) {
                feedback.blocked("Failed to assign branch access", err instanceof Error ? err.message : "Unknown error");
              } finally {
                setBusy(false);
              }
            }}
          >
            Assign
          </Button>
        </div>
      </div>
    </div>
  );
}
