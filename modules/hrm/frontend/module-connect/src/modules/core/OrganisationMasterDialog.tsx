import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ApiError } from "@/platform/api-client";
import { realApi } from "@/platform/use-api";
import { organisationEditorPayload, type OrganisationEditorFields, type OrganisationEditorKind } from "./organisation-editing";

export function OrganisationMasterDialog({ kind, initial, companies, canAct, onClose, onSaved, onReconcile }: {
  kind: OrganisationEditorKind;
  initial: Record<string, unknown> | null;
  companies: Array<{ id: string; registeredName: string }>;
  canAct: boolean;
  onClose: () => void;
  onSaved: () => void;
  onReconcile: () => void;
}) {
  const editing = initial !== null;
  const id = String(initial?.id ?? "");
  const [fields, setFields] = useState<OrganisationEditorFields>(() => ({
    code: String(initial?.code ?? ""),
    name: String(initial?.[kind === "company" ? "registeredName" : "name"] ?? ""),
    tradingName: String(initial?.tradingName ?? ""),
    currency: String(initial?.currency ?? ""),
    country: String(initial?.countryCode ?? "ZM"),
    companyId: String(initial?.legalEntityId ?? (companies.length === 1 ? companies[0].id : "")),
    costCentre: String(initial?.costCentreRef ?? ""),
  }));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const alive = useRef(false);
  useEffect(() => { alive.current = true; return () => { alive.current = false; }; }, []);
  const update = (field: keyof OrganisationEditorFields, value: string) => setFields((current) => ({ ...current, [field]: value }));
  const noun = kind === "company" ? "company" : "department";
  const locked = busy || uncertain || !canAct;
  const close = () => { if (!busy) { if (uncertain) onReconcile(); else onClose(); } };
  return <Dialog open onOpenChange={(open) => { if (!open) close(); }}>
    <DialogContent className="max-w-lg">
      <DialogHeader>
        <DialogTitle>{editing ? "Edit" : "New"} {noun}</DialogTitle>
        <DialogDescription>{kind === "company"
          ? "Default currency applies to new transactions that use company defaults. Existing transaction currencies stay unchanged."
          : "Maintain department details. Existing manager, reporting structure and historical records stay linked."}</DialogDescription>
      </DialogHeader>
      <form className="space-y-4" onSubmit={async (event) => {
        event.preventDefault();
        if (locked) return;
        let payload: Record<string, unknown>;
        try { payload = organisationEditorPayload(kind, fields, editing); }
        catch (failure) { setError(failure instanceof Error ? failure.message : "Check the fields."); return; }
        setBusy(true); setError(null);
        try {
          if (kind === "company") {
            if (editing) await realApi.updateLegalEntity(id, payload);
            else await realApi.createLegalEntity(payload);
          } else {
            if (editing) await realApi.updateOrgUnit(id, payload);
            else await realApi.createOrgUnit(payload);
          }
          if (alive.current) onSaved();
        } catch (failure) {
          if (!alive.current) return;
          const denied = failure instanceof ApiError && failure.status >= 400 && failure.status < 500 && failure.status !== 408;
          setUncertain(!denied);
          setError(denied ? failure.message : "Save could not be confirmed. Close and refresh, then check this record before trying again.");
        } finally { if (alive.current) setBusy(false); }
      }}>
        <div><Label htmlFor="org-editor-code">Code</Label><Input id="org-editor-code" value={fields.code} disabled={locked || editing} required={!editing} onChange={(e) => update("code", e.target.value)} /></div>
        <div><Label htmlFor="org-editor-name">{kind === "company" ? "Company name" : "Department name"}</Label><Input id="org-editor-name" value={fields.name} disabled={locked} required onChange={(e) => update("name", e.target.value)} /></div>
        {kind === "company" ? <>
          <div><Label htmlFor="org-editor-trading-name">Trading name</Label><Input id="org-editor-trading-name" value={fields.tradingName} disabled={locked} onChange={(e) => update("tradingName", e.target.value)} /></div>
          <div><Label htmlFor="org-editor-currency">Default currency</Label><Input id="org-editor-currency" value={fields.currency} disabled={locked} required maxLength={3} placeholder="ZMW" onChange={(e) => update("currency", e.target.value)} /></div>
          <div><Label htmlFor="org-editor-country">Country code</Label><Input id="org-editor-country" value={fields.country} disabled={locked || editing} required={!editing} maxLength={2} onChange={(e) => update("country", e.target.value)} /></div>
        </> : <>
          <div><Label htmlFor="org-editor-company">Company</Label><Select value={fields.companyId} onValueChange={(value) => update("companyId", value)} disabled={locked || editing}>
            <SelectTrigger id="org-editor-company"><SelectValue placeholder="Select company" /></SelectTrigger>
            <SelectContent>{companies.map((company) => <SelectItem key={company.id} value={company.id}>{company.registeredName}</SelectItem>)}</SelectContent>
          </Select></div>
          <div><Label htmlFor="org-editor-cost-centre">Cost centre reference</Label><Input id="org-editor-cost-centre" value={fields.costCentre} disabled={locked} onChange={(e) => update("costCentre", e.target.value)} /></div>
        </>}
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        {!canAct ? <p role="alert" className="text-sm text-muted-foreground">You do not have permission to save organization settings.</p> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" disabled={busy} onClick={close}>{uncertain ? "Close and refresh" : "Cancel"}</Button>
          <Button type="submit" disabled={locked}>{busy ? "Saving…" : "Save"}</Button>
        </div>
      </form>
    </DialogContent>
  </Dialog>;
}
