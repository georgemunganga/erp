import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PageHeader } from "@/platform/components/PageHeader";
import { EmptyState, ErrorState, LoadingState } from "@/platform/components/States";
import { ApiError } from "@/platform/api-client";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import {
  listWorkflowConfiguration,
  type WorkflowConfigurationType,
} from "./workflow-configuration";

export function WorkflowConfigurationPage() {
  const { entityId } = useApp(),
    { user } = useAuth();
  const scope = JSON.stringify([user?.subject, user?.roles, entityId]);
  const current = useRef(scope);
  current.current = scope;
  const generation = useRef(0);
  const [items, setItems] = useState<WorkflowConfigurationType[]>([]),
    [loading, setLoading] = useState(true);
  const [error, setError] = useState(""),
    [moduleId, setModuleId] = useState("");
  const [recordType, setRecordType] = useState(""),
    [trigger, setTrigger] = useState("");
  const refresh = useCallback(async () => {
    const observed = scope,
      sequence = ++generation.current;
    setItems([]);
    setModuleId("");
    setRecordType("");
    setTrigger("");
    setLoading(true);
    setError("");
    try {
      const rows = await listWorkflowConfiguration();
      if (current.current === observed && generation.current === sequence) setItems(rows);
    } catch (cause) {
      if (current.current !== observed || generation.current !== sequence) return;
      setError(
        cause instanceof ApiError && cause.status === 403
          ? "You do not have permission to open workflow settings."
          : "Workflow settings could not be loaded. Please try again.",
      );
    } finally {
      if (current.current === observed && generation.current === sequence) setLoading(false);
    }
  }, [scope]);
  useEffect(() => {
    void refresh();
    return () => {
      ++generation.current;
    };
  }, [refresh]);
  const modules = [...new Map(items.map((x) => [x.moduleId, x.moduleLabel])).entries()];
  const records = [
    ...new Map(
      items.filter((x) => x.moduleId === moduleId).map((x) => [x.recordType, x.recordLabel]),
    ).entries(),
  ];
  const triggers = items.filter((x) => x.moduleId === moduleId && x.recordType === recordType);
  const selected = triggers.find((x) => x.trigger === trigger);
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <PageHeader
        title="Approval workflows"
        description="Choose what you want to configure. Each module keeps its business rules and approval controls."
        primaryAction={
          <Button variant="outline" disabled={loading} onClick={() => void refresh()}>
            Refresh
          </Button>
        }
      />
      {loading ? (
        <LoadingState rows={3} label="Loading workflow settings" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void refresh()} />
      ) : !items.length ? (
        <EmptyState
          title="No workflow settings registered"
          body="An installed module must provide its workflow settings before they can appear here."
        />
      ) : (
        <>
          <section
            className="grid gap-5 rounded-xl border border-border bg-card p-6 sm:grid-cols-3"
            aria-label="Choose workflow"
          >
            <div className="space-y-2">
              <Label htmlFor="workflow-module">Module</Label>
              <Select
                value={moduleId}
                onValueChange={(value) => {
                  setModuleId(value);
                  setRecordType("");
                  setTrigger("");
                }}
              >
                <SelectTrigger id="workflow-module">
                  <SelectValue placeholder="Choose a module" />
                </SelectTrigger>
                <SelectContent>
                  {modules.map(([id, label]) => (
                    <SelectItem key={id} value={id}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="workflow-record">Record type</Label>
              <Select
                value={recordType}
                disabled={!moduleId}
                onValueChange={(value) => {
                  setRecordType(value);
                  const choices = items.filter(
                    (x) => x.moduleId === moduleId && x.recordType === value,
                  );
                  setTrigger(choices.length === 1 ? choices[0].trigger : "");
                }}
              >
                <SelectTrigger id="workflow-record">
                  <SelectValue placeholder="Choose a record" />
                </SelectTrigger>
                <SelectContent>
                  {records.map(([id, label]) => (
                    <SelectItem key={id} value={id}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="workflow-trigger">When</Label>
              <Select value={trigger} disabled={!recordType} onValueChange={setTrigger}>
                <SelectTrigger id="workflow-trigger">
                  <SelectValue placeholder="Choose when" />
                </SelectTrigger>
                <SelectContent>
                  {triggers.map((x) => (
                    <SelectItem key={x.id} value={x.trigger}>
                      {x.triggerLabel}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </section>
          {selected ? (
            <section
              className="space-y-4 rounded-xl border border-border bg-card p-6"
              aria-label="Selected workflow settings"
            >
              <h2 className="text-lg font-semibold">
                {selected.recordLabel} — {selected.triggerLabel.toLowerCase()}
              </h2>
              <p className="text-sm text-muted-foreground">{selected.description}</p>
              <p className="text-sm" role="status">
                {selected.availabilityNote}
              </p>
              <div className="flex flex-wrap gap-3">
                {selected.settings.map((link, index) => (
                  <Button key={link.path} variant={index === 0 ? "default" : "outline"} asChild>
                    <a href={link.path}>{link.label}</a>
                  </Button>
                ))}
              </div>
            </section>
          ) : (
            <p className="text-sm text-muted-foreground">
              Select a module and record to see its approval settings.
            </p>
          )}
        </>
      )}
    </div>
  );
}
