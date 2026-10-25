import type { DataKey, DemoStatus } from "../types";

export type FlowDomain = "Sales" | "Purchases" | "Banking" | "Accountant" | "Items & Projects" | "Settings & Reports";
export type FlowValue = string | number | boolean | string[] | FlowRow[];
export type FlowValues = Record<string, FlowValue>;
export type FlowRow = Record<string, string | number>;
export interface FlowField {
  id: string;
  label: string;
  type: "text" | "email" | "number" | "date" | "select" | "textarea" | "checkbox" | "record" | "multi-record" | "lines" | "journal-lines" | "allocations" | "attachment" | "csv";
  required?: boolean;
  min?: number;
  options?: string[];
  collection?: DataKey | string;
  help?: string;
  defaultValue?: FlowValue;
  placeholder?: string;
}
export interface FlowStep { title: string; description: string; fields: FlowField[]; }
export type EffectKind = "create" | "edit" | "status" | "convert" | "payment" | "credit" | "recurring" | "journal" | "time-invoice" | "inventory" | "artifact" | "artifact-status" | "bank-match" | "bank-categorize" | "bank-reconcile" | "document-process" | "automation-test" | "import";
export interface FlowEffect {
  kind: EffectKind;
  entity?: DataKey;
  target?: DataKey;
  collection?: string;
  status?: DemoStatus;
  action?: string;
}
export interface FlowDefinition {
  id: string;
  domain: FlowDomain;
  title: string;
  description: string;
  module?: DataKey;
  steps: FlowStep[];
  effect: FlowEffect;
  completionTitle: string;
  completionMessage: string;
  nextFlowIds?: string[];
  evidence: "tenant-pattern" | "local-concept";
}
export interface LocalArtifact {
  id: string;
  collection: string;
  name: string;
  status: string;
  values: FlowValues;
  history: { at: string; action: string }[];
}
export interface FlowReceipt {
  id: string;
  flowId: string;
  title: string;
  completedAt: string;
  summary: string;
  values: FlowValues;
  links: { label: string; path: string }[];
}
export interface FlowStore {
  artifacts: LocalArtifact[];
  receipts: FlowReceipt[];
}
