import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { ArrowLeft, History, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/platform/components/PageHeader";
import { RecordTable } from "@/platform/components/RecordTable";
import { EmptyState, ErrorState, LoadingState, RestrictedState } from "@/platform/components/States";
import { ApiError } from "@/platform/api-client";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import { coreCurrencyApi, type CurrencyPrecisionList, type CurrencyPrecisionVersion, type CurrencyRounding } from "./currency-precisions";
import { currencyCommandJournal, type PendingCurrencyCommand } from "./currency-command-journal";

const LIVE = import.meta.env.VITE_USE_REAL_API === "true";
const shortDate = (value: string | null) => value ? new Date(value).toLocaleString() : "—";
const messageFor = (error: unknown): string => {
  if (error instanceof ApiError) {
    if (error.status === 403) return "You do not have permission to view or change Core currency references.";
    if (error.status === 409) return "This currency changed while you were editing. Check the saved command, then refresh the current revision.";
    if (error.status === 422) return "The reference was rejected. Check the currency code, decimal places, rounding method, source and reason.";
    if (error.status === 503) return "Core currency references are temporarily unavailable. If a change was sent, its pending key is preserved.";
  }
  return error instanceof Error ? error.message : "The currency request could not be confirmed.";
};

export function CurrencyPrecisionsPage() {
  const { entityId: companyId } = useApp();
  const { user } = useAuth();
  const actorId = user?.subject ?? "";
  const [list, setList] = useState<CurrencyPrecisionList | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState<CurrencyPrecisionVersion | null>(null);
  const [detailError, setDetailError] = useState("");
  const [code, setCode] = useState("");
  const [minorUnits, setMinorUnits] = useState("2");
  const [rounding, setRounding] = useState<CurrencyRounding>("ToEven");
  const [sourceReference, setSourceReference] = useState("");
  const [reason, setReason] = useState("");
  const [retireReason, setRetireReason] = useState("");
  const [pending, setPending] = useState<PendingCurrencyCommand | null>(null);
  const [pendingError, setPendingError] = useState("");
  const [absentReceipt, setAbsentReceipt] = useState(false);
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const scopeRef = useRef("");
  const loadSeq = useRef(0);
  const detailSeq = useRef(0);
  const scope = `${actorId}:${companyId}`;
  scopeRef.current = scope;

  const refresh = useCallback(async () => {
    if (!companyId) return;
    const observedScope = `${actorId}:${companyId}`;
    const request = ++loadSeq.current;
    setLoading(true);
    setLoadError("");
    try {
      const next = await coreCurrencyApi.list(companyId);
      if (scopeRef.current !== observedScope || loadSeq.current !== request) return;
      setList(next);
      setCode((old) => old || next.companyCurrency);
    } catch (error) {
      if (scopeRef.current === observedScope && loadSeq.current === request) setLoadError(messageFor(error));
    } finally {
      if (scopeRef.current === observedScope && loadSeq.current === request) setLoading(false);
    }
  }, [actorId, companyId]);

  useEffect(() => {
    ++loadSeq.current; ++detailSeq.current;
    setList(null); setSelectedId(""); setDetail(null); setNotice(""); setAbsentReceipt(false); setBusy(false);
    setCode(""); setMinorUnits("2"); setRounding("ToEven"); setSourceReference(""); setReason(""); setRetireReason("");
    setPending(null); setPendingError("");
    if (!LIVE || !actorId || !companyId) return;
    try { setPending(currencyCommandJournal.read(actorId, companyId)); setPendingError(""); }
    catch (error) { setPendingError(messageFor(error)); setPending(null); }
    void refresh();
  }, [actorId, companyId, refresh]);

  const showDetail = async (id: string) => {
    setSelectedId(id); setDetail(null); setDetailError("");
    const observedScope = scope;
    const request = ++detailSeq.current;
    try {
      const next = await coreCurrencyApi.detail(companyId, id);
      if (scopeRef.current === observedScope && detailSeq.current === request) setDetail(next);
    } catch (error) {
      if (scopeRef.current === observedScope && detailSeq.current === request) setDetailError(messageFor(error));
    }
  };

  const recover = async () => {
    if (!pending || busy) return;
    const observedScope = scope;
    setBusy(true); setNotice(""); setAbsentReceipt(false);
    try {
      const saved = await coreCurrencyApi.recover(companyId, pending.operation, pending.key);
      currencyCommandJournal.clear(pending);
      if (scopeRef.current === observedScope) {
        setPending(null);
        setNotice(`${saved.currencyCode} revision ${saved.revision}: saved ${saved.operation.toLowerCase()} result recovered.`);
        await refresh();
      }
    } catch (error) {
      if (scopeRef.current !== observedScope) return;
      if (error instanceof ApiError && error.status === 404) {
        setAbsentReceipt(true);
        setNotice("No recorded result was found for this key. The command remains pending. Check with a Core administrator before discarding it.");
      } else setNotice(messageFor(error));
    } finally { if (scopeRef.current === observedScope) setBusy(false); }
  };

  const publish = async (event: FormEvent) => {
    event.preventDefault();
    if (!list?.canConfigure || pending || pendingError || busy || !actorId || list.companyId !== companyId) return;
    const normalizedCode = code.trim().toUpperCase();
    const units = Number(minorUnits);
    if (!/^[A-Z]{3}$/.test(normalizedCode) || !Number.isInteger(units) || units < 0 || units > 4 ||
      !sourceReference.trim() || sourceReference.trim().length > 200 || !reason.trim() || reason.trim().length > 500) {
      setNotice("Enter a three-letter code, 0–4 decimal places, a source and a reason."); return;
    }
    const latest = list.items.filter((item) => item.currencyCode === normalizedCode)
      .reduce((max, item) => Math.max(max, item.revision), 0);
    let entry: PendingCurrencyCommand;
    try { entry = currencyCommandJournal.begin(actorId, companyId, "Publish", normalizedCode); }
    catch (error) { setNotice(messageFor(error)); return; }
    setPending(entry); setBusy(true); setNotice(""); setAbsentReceipt(false);
    const observedScope = scope;
    try {
      const saved = await coreCurrencyApi.publish({ companyId, currencyCode: normalizedCode,
        expectedRevision: latest, minorUnits: units, rounding, sourceReference: sourceReference.trim(), reason: reason.trim() }, entry.key);
      currencyCommandJournal.clear(entry);
      if (scopeRef.current === observedScope) {
        setPending(null); setSelectedId(""); setDetail(null); setNotice(`${saved.currencyCode} revision ${saved.revision} was published. Existing transactions keep their previous precision.`);
        setReason(""); await refresh();
      }
    } catch (error) {
      if (scopeRef.current === observedScope) setNotice(`${messageFor(error)} Check the saved result before another change.`);
    } finally { if (scopeRef.current === observedScope) setBusy(false); }
  };

  const retire = async (item: CurrencyPrecisionVersion) => {
    if (!list?.canConfigure || list.companyId !== companyId || !item.current || !list.items.some((row) => row.id === item.id && row.current) || pending || pendingError || busy || !actorId || !retireReason.trim() || retireReason.trim().length > 500) return;
    let entry: PendingCurrencyCommand;
    try { entry = currencyCommandJournal.begin(actorId, companyId, "Retire", item.id); }
    catch (error) { setNotice(messageFor(error)); return; }
    setPending(entry); setBusy(true); setNotice(""); setAbsentReceipt(false);
    const observedScope = scope;
    try {
      const saved = await coreCurrencyApi.retire(item.id, { companyId, expectedRevision: item.revision, reason: retireReason.trim() }, entry.key);
      currencyCommandJournal.clear(entry);
      if (scopeRef.current === observedScope) {
        setPending(null); setSelectedId(""); setDetail(null); setNotice(`${saved.currencyCode} revision ${saved.revision} was retired for new use. Historical transactions remain unchanged.`);
        setRetireReason(""); await refresh();
      }
    } catch (error) {
      if (scopeRef.current === observedScope) setNotice(`${messageFor(error)} Check the saved result before another change.`);
    } finally { if (scopeRef.current === observedScope) setBusy(false); }
  };

  return <div className="space-y-6">
    <a href="/core/workspace/settings" className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-primary"><ArrowLeft className="size-4" /> All settings</a>
    <PageHeader eyebrow="ERP Core · Shared defaults" title="Currencies"
      description="Review calculation precision for currencies used by this company. Display formatting and exchange rates are managed separately."
      primaryAction={<Button type="button" variant="outline" onClick={() => void refresh()} disabled={!companyId || busy || loading}><RefreshCw className="mr-2 size-4" /> Refresh</Button>} />
    {!LIVE ? <RestrictedState title="Connected Core required" body="Currency references are saved in ERP Core. This preview cannot publish or retire them." /> :
      !companyId ? <EmptyState title="Choose a company" body="Select a company in the Core toolbar to review its currency references." /> :
      !actorId ? <RestrictedState title="Sign in required" body="Sign in to review Core currency references." /> : <>
      {loading && !list ? <LoadingState label="Loading currency references" /> : null}
      {loadError ? <ErrorState message={loadError} onRetry={() => void refresh()} /> : null}
      {list?.companyId === companyId ? <>
        <section className="rounded-xl border bg-surface p-5 shadow-sm">
          <h2 className="text-base font-semibold">Company default: {list.companyCurrency}</h2>
          <p className="mt-1 text-sm text-muted-foreground">This code comes from the selected Core company. A reference sets calculation decimal places and rounding; it does not set a display format or exchange rate.</p>
        </section>
        {pendingError ? <ErrorState message={pendingError} /> : null}
        {pending ? <section className="rounded-xl border border-warning/40 bg-warning-soft p-5" role="status">
          <h2 className="font-semibold">Check the saved result</h2>
          <p className="mt-1 text-sm">A {pending.operation.toLowerCase()} command for {pending.target} has an unconfirmed outcome. Its original key is saved in this browser. No new currency change can start until it is resolved.</p>
          <div className="mt-3 flex flex-wrap gap-2"><Button type="button" onClick={() => void recover()} disabled={busy}>Check saved result</Button>
            {absentReceipt ? <Button type="button" variant="outline" onClick={() => {
              if (!window.confirm("No saved result was found. Discard this pending key and allow a new command?")) return;
              currencyCommandJournal.clear(pending); setPending(null); setAbsentReceipt(false); setNotice("The unrecorded command was discarded. Review the latest reference before submitting a new change."); void refresh();
            }}>Discard unrecorded command</Button> : null}</div>
        </section> : null}
        {notice ? <p role="status" className="rounded-lg border bg-surface p-3 text-sm">{notice}</p> : null}
        {list.items.length === 0 ? <EmptyState title="No currency references" body="No published Core precision reference is available for this company. When Core publication is available, a configurator can publish the first revision." /> :
          <section className="overflow-x-auto rounded-xl border bg-surface shadow-sm" aria-label="Currency reference history">
            <RecordTable><thead><tr><th>Currency</th><th>Decimal places</th><th>Rounding</th><th>Revision</th><th>Published</th><th>Status</th><th>Details</th></tr></thead>
              <tbody>{list.items.map((item) => <tr key={item.id}>
                <td className="font-medium">{item.currencyCode}</td><td>{item.minorUnits}</td><td>{item.rounding === "ToEven" ? "To even" : "Away from zero"}</td><td>{item.revision}</td><td>{shortDate(item.publishedAtUtc)}</td><td>{item.current ? "Current" : "Historical"}</td>
                <td><Button type="button" variant="ghost" size="sm" onClick={() => void showDetail(item.id)}>View</Button></td>
              </tr>)}</tbody></RecordTable>
          </section>}
        {selectedId ? <section className="rounded-xl border bg-surface p-5 shadow-sm" aria-label="Currency revision detail">
          <div className="flex items-center gap-2"><History className="size-4 text-muted-foreground" /><h2 className="font-semibold">Revision detail</h2></div>
          {detailError ? <ErrorState message={detailError} /> : !detail ? <LoadingState rows={2} label="Loading revision" /> : <div className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
            <p><strong>Code:</strong> {detail.currencyCode}</p><p><strong>Revision:</strong> {detail.revision}</p>
            <p><strong>Calculation decimal places:</strong> {detail.minorUnits}</p><p><strong>Rounding:</strong> {detail.rounding === "ToEven" ? "To even" : "Away from zero"}</p>
            <p><strong>Published:</strong> {shortDate(detail.publishedAtUtc)}</p><p><strong>Retired:</strong> {shortDate(detail.retiredAtUtc)}</p>
            <p className="sm:col-span-2"><strong>Reference source:</strong> {detail.sourceReference}</p>
            {detail.retirementReason ? <p className="sm:col-span-2"><strong>Retirement reason:</strong> {detail.retirementReason}</p> : null}
            {list.canConfigure && detail.current ? <div className="space-y-2 sm:col-span-2"><Label htmlFor="retire-reason">Reason for retirement</Label><Input id="retire-reason" maxLength={500} value={retireReason} onChange={(event) => setRetireReason(event.target.value)} /><Button type="button" variant="outline" disabled={busy || !!pending || !!pendingError || !retireReason.trim()} onClick={() => void retire(detail)}>Retire this revision</Button></div> : null}
          </div>}
        </section> : null}
        {list.canConfigure ? <section className="rounded-xl border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">Publish a calculation revision</h2>
          <p className="mt-1 text-sm text-muted-foreground">Publishing replaces the current reference for new use. Previous transactions keep their recorded precision. Display formatting is unchanged.</p>
          <form className="mt-4 grid gap-4 sm:grid-cols-2" onSubmit={(event) => void publish(event)}>
            <div><Label htmlFor="currency-code">Currency code</Label><Input id="currency-code" maxLength={3} value={code} onChange={(event) => setCode(event.target.value.toUpperCase())} /></div>
            <div><Label htmlFor="currency-units">Calculation decimal places</Label><Select value={minorUnits} onValueChange={setMinorUnits}><SelectTrigger id="currency-units"><SelectValue /></SelectTrigger><SelectContent>{[0,1,2,3,4].map((n) => <SelectItem key={n} value={String(n)}>{n}</SelectItem>)}</SelectContent></Select></div>
            <div><Label htmlFor="currency-rounding">Rounding method</Label><Select value={rounding} onValueChange={(value) => setRounding(value as CurrencyRounding)}><SelectTrigger id="currency-rounding"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="ToEven">To even</SelectItem><SelectItem value="AwayFromZero">Away from zero</SelectItem></SelectContent></Select></div>
            <div><Label htmlFor="currency-source">Reference source</Label><Input id="currency-source" maxLength={200} value={sourceReference} onChange={(event) => setSourceReference(event.target.value)} /></div>
            <div className="sm:col-span-2"><Label htmlFor="currency-reason">Reason for publication</Label><Input id="currency-reason" maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} /></div>
            <div className="sm:col-span-2"><Button type="submit" disabled={busy || !!pending || !!pendingError || loading}>Publish revision</Button></div>
          </form>
        </section> : <p className="rounded-xl border bg-surface p-4 text-sm text-muted-foreground">You can review these references. Publishing and retirement are unavailable to this account right now.</p>}
      </> : null}
    </>}
  </div>;
}
