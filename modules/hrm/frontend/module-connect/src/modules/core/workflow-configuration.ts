import { hrmApi } from "@/platform/api-client";
import {
  parseWorkflowConfiguration,
  type WorkflowConfigurationType,
} from "./workflow-configuration-contract";
export type { WorkflowConfigurationType } from "./workflow-configuration-contract";

export async function listWorkflowConfiguration(): Promise<WorkflowConfigurationType[]> {
  return parseWorkflowConfiguration(await hrmApi.get<unknown>("/core/v1/workflow-configuration"));
}
