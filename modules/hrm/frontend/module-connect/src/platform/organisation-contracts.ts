/** Existing OrganizationServices.cs transport contracts, not additional Core masters. */
export interface OrgChartNode {
  id: string;
  code: string;
  name: string;
  unitType: string;
  status: string;
  parentId: string | null;
  managerId: string | null;
  managerName: string | null;
  managerJobTitle: string | null;
  headcount: number;
  legalEntityName: string | null;
  children: OrgChartNode[];
}
export interface OrgChart { asAt: string; roots: OrgChartNode[]; }
export interface ReportingLine {
  workerId: string;
  employeeNo: string;
  fullName: string;
  status: string;
  orgUnitId: string | null;
  orgUnitName: string | null;
  managerId: string | null;
  managerName: string | null;
  grade: string | null;
  jobTitle: string | null;
  managerNamePath: string | null;
}
export interface ReportingLineList { items: ReportingLine[]; total: number; }
