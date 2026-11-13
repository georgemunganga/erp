import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { FormSelect } from "@/components/ui/form-select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { KuubuIdentity } from "@/platform/components/KuubuIdentity";
import { ListPage, type ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { createOperationalClient } from "./product-operational-api";
import {
  archiveProductWorker,
  isWorkspaceLoss,
  PRODUCT_WORKER_PAGE_SIZE,
  readProductWorkerContext,
  readProductWorkers,
  type DirectoryWorker,
  type ProductWorkerContext,
} from "./product-worker-api";

const operational = createOperationalClient();
const columns: ColumnDef<DirectoryWorker>[] = [
  { id: "name", header: "Employee", cell: (row) => row.name },
  { id: "number", header: "Employee number", cell: (row) => row.number },
  { id: "title", header: "Job title", cell: (row) => row.title },
  { id: "department", header: "Department", cell: (row) => row.department },
  { id: "branch", header: "Branch", cell: (row) => row.branch },
  {
    id: "status",
    header: "Status",
    cell: (row) => (row.archived ? "Archived" : row.status.replaceAll("-", " ")),
  },
];

export function ProductEmployeeDirectory() {
  const [context, setContext] = useState<ProductWorkerContext | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const expected = useRef<string | undefined>(undefined);
  const accessLost = useCallback(() => {
    setContext(null);
    setError("Your workspace access changed. Choose a company to continue.");
  }, []);

  useEffect(() => {
    if (error) return;
    let pending: AbortController | undefined;
    let lastCheck = 0;
    async function check() {
      if (document.hidden || pending || Date.now() - lastCheck < 1000) return;
      const request = new AbortController();
      pending = request;
      lastCheck = Date.now();
      try {
        const current = await readProductWorkerContext(request.signal, expected.current);
        if (request.signal.aborted) return;
        expected.current = current.marker;
        setContext((previous) => previous ?? current);
      } catch (failure) {
        if (request.signal.aborted) return;
        setContext(null);
        setError(
          isWorkspaceLoss(failure)
            ? "Your workspace access changed. Choose a company to continue."
            : "We couldn’t check your workspace access.",
        );
      } finally {
        if (pending === request) pending = undefined;
      }
    }
    function hideRecords() {
      pending?.abort();
      pending = undefined;
      lastCheck = 0;
      setContext(null);
    }
    function onVisibility() {
      if (document.hidden) hideRecords();
      else void check();
    }
    const timer = window.setInterval(() => void check(), 30000);
    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("pageshow", onVisibility);
    window.addEventListener("pagehide", hideRecords);
    void check();
    return () => {
      pending?.abort();
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("pageshow", onVisibility);
      window.removeEventListener("pagehide", hideRecords);
    };
  }, [attempt, error]);

  return (
    <main className="mx-auto min-h-dvh max-w-6xl space-y-6 px-5 py-8">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <KuubuIdentity />
        <Button asChild variant="outline">
          <a href="/workspace-access">Choose company</a>
        </Button>
      </header>
      {context ? (
        <Directory key={context.marker} context={context} onAccessLost={accessLost} />
      ) : (
        <>
          <PageHeader title="Employees" />
          {error ? (
            <div className="space-y-3">
              <p role="alert">{error}</p>
              <Button
                variant="outline"
                onClick={() => {
                  setError("");
                  setAttempt((value) => value + 1);
                }}
              >
                Check access again
              </Button>
            </div>
          ) : (
            <p role="status">Checking workspace access…</p>
          )}
        </>
      )}
    </main>
  );
}

function Directory({
  context,
  onAccessLost,
}: {
  context: ProductWorkerContext;
  onAccessLost: () => void;
}) {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [branch, setBranch] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  const [result, setResult] = useState<{
    key: string;
    rows: DirectoryWorker[];
    total: number;
  } | null>(null);
  const [readError, setReadError] = useState("");
  const [message, setMessage] = useState("");
  const [archive, setArchive] = useState<DirectoryWorker | null>(null);
  const [archiving, setArchiving] = useState(false);
  const mutation = useRef<AbortController | null>(null);
  const queryKey = JSON.stringify([search, branch, includeArchived, page, refresh]);
  const current = result?.key === queryKey ? result : null;
  const totalPages = Math.max(1, Math.ceil((current?.total ?? 0) / PRODUCT_WORKER_PAGE_SIZE));

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);
  useEffect(() => {
    const request = new AbortController();
    setReadError("");
    void readProductWorkers(
      context,
      { search, locationId: branch, page, includeArchived },
      request.signal,
    )
      .then((data) => {
        if (request.signal.aborted) return;
        const lastPage = Math.max(1, Math.ceil(data.total / PRODUCT_WORKER_PAGE_SIZE));
        if (page > lastPage) setPage(lastPage);
        else setResult({ key: queryKey, ...data });
      })
      .catch((failure) => {
        if (request.signal.aborted) return;
        setResult(null);
        if (isWorkspaceLoss(failure)) onAccessLost();
        else setReadError("We couldn’t load employees. Try again.");
      });
    return () => request.abort();
  }, [context, search, branch, page, includeArchived, queryKey, onAccessLost]);
  useEffect(() => () => mutation.current?.abort(), []);

  async function confirmArchive() {
    if (!archive || mutation.current) return;
    const request = new AbortController();
    mutation.current = request;
    setArchiving(true);
    setMessage("");
    try {
      const { csrf } = await operational.session(request.signal);
      await archiveProductWorker(context, archive.id, csrf, request.signal);
      if (!request.signal.aborted) setMessage(`${archive.name} archived.`);
    } catch (failure) {
      if (request.signal.aborted) return;
      if (isWorkspaceLoss(failure)) {
        onAccessLost();
        return;
      }
      setMessage(
        "We couldn’t confirm the archive. Check the refreshed directory before trying again.",
      );
    } finally {
      mutation.current = null;
      if (!request.signal.aborted) {
        setArchiving(false);
        setArchive(null);
        setResult(null);
        setRefresh((value) => value + 1);
      }
    }
  }

  const visibleColumns = context.canArchive
    ? [
        ...columns,
        {
          id: "actions",
          header: "Actions",
          cell: (row: DirectoryWorker) =>
            row.archived ? null : (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => setArchive(row)}
                aria-label={`Archive ${row.name}`}
              >
                Archive
              </Button>
            ),
        },
      ]
    : columns;

  return (
    <>
      <PageHeader
        eyebrow={context.company.name}
        title="Employees"
        description={context.allLocations ? "All company locations" : "Your assigned locations"}
      />
      <div className="flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2 text-sm">
          Branch
          <FormSelect
            aria-label="Branch"
            value={branch || "all"}
            onChange={(event) => {
              setBranch(event.target.value === "all" ? "" : event.target.value);
              setPage(1);
            }}
          >
            <option value="all">All locations in scope</option>
            {context.branches.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </FormSelect>
        </label>
        <label className="flex min-h-11 items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.target.checked);
              setPage(1);
            }}
          />
          Include archived employees
        </label>
      </div>
      {message && <p role="status">{message}</p>}
      {readError ? (
        <div className="space-y-3">
          <p role="alert">{readError}</p>
          <Button variant="outline" onClick={() => setRefresh((value) => value + 1)}>
            Retry
          </Button>
        </div>
      ) : (
        <div aria-busy={!current}>
          <ListPage
            rows={current?.rows ?? []}
            columns={visibleColumns}
            searchFields={(row) => `${row.name} ${row.number}`}
            searchValue={searchInput}
            onSearchChange={setSearchInput}
            searchPlaceholder="Search employees"
            emptyBody={current ? "No employees found." : "Loading employees…"}
            footer={
              current && (
                <div className="flex flex-wrap items-center gap-3">
                  <span>
                    {current.total} employees · Page {page} of {totalPages}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page <= 1}
                    onClick={() => setPage(page - 1)}
                  >
                    Previous
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page >= totalPages}
                    onClick={() => setPage(page + 1)}
                  >
                    Next
                  </Button>
                </div>
              )
            }
          />
        </div>
      )}
      {archive && (
        <Dialog
          open
          onOpenChange={(open) => {
            if (!open && !archiving) setArchive(null);
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Archive {archive.name}?</DialogTitle>
              <DialogDescription>
                The employee record will move out of the active directory and remain available in
                archived records.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" disabled={archiving} onClick={() => setArchive(null)}>
                Cancel
              </Button>
              <Button
                variant="destructive"
                disabled={archiving}
                onClick={() => void confirmArchive()}
              >
                {archiving ? "Archiving…" : "Archive employee"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </>
  );
}
