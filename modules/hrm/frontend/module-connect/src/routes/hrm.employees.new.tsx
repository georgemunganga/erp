import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, Info } from "lucide-react";
import { feedback } from "@/platform/feedback";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { employees, entities } from "@/mock/data";
import { api } from "@/mock/service";
import { ApiError } from "@/platform/api-client";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import {
  demoEntityTree,
  flattenEntityTree,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/employees/new")({
  head: () => ({
    meta: [
      { title: "Add an employee — HRM" },
      { name: "description", content: "Create an employee record, place them in the organisation, and hand over to onboarding." },
      { property: "og:title", content: "Add an employee — HRM" },
      { property: "og:description", content: "Create an employee record, place them in the organisation, and hand over to onboarding." },
    ],
  }),
  component: NewEmployee,
});

const employmentTypes = ["Permanent", "Fixed term", "Part time", "Contractor", "Intern"] as const;

/** Zambian NRC: six digits, two digits, one digit — 123456/78/9. */
const NRC = /^\d{6}\/\d{2}\/\d$/;

/** Realistic date-of-birth window: 1900-01-01 … today (YYYY-MM-DD). */
const MIN_DOB = "1900-01-01";
const TODAY_ISO = new Date().toISOString().slice(0, 10);

const ISO8601 = /^\d{4}-\d{2}-\d{2}$/;

/** Returns the stored YYYY-MM-DD string for a typed date input. Reading
 * `value` from a native date input returns whatever the user typed in the
 * browser's locale (MDY on US locales — typing 2001-01-25 can store a year
 * like 0115). `valueAsDate` always parses the control's value in the input's
 * local time zone and is locale-independent for well-formed strings; we fall
 * back to the raw string only when it is already ISO. */
function readDateInput(el: HTMLInputElement): string {
  if (el.valueAsDate instanceof Date && !Number.isNaN(el.valueAsDate.getTime())) {
    const iso = el.valueAsDate.toISOString().slice(0, 10);
    if (ISO8601.test(iso)) return iso;
  }
  return ISO8601.test(el.value) ? el.value : "";
}

const emergencyRelationships = ["Spouse", "Parent", "Sibling", "Child", "Friend", "Other"];
const salutations = ["Mr", "Mrs", "Ms", "Dr", "Prof"];
const genders = ["Female", "Male", "Prefer not to say"];
const maritalStatuses = ["Single", "Married", "Divorced", "Widowed", "Prefer not to say"];

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function NewEmployee() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [createdId, setCreatedId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [middleName, setMiddleName] = useState("");
  const [salutation, setSalutation] = useState("");
  const [gender, setGender] = useState("");
  const [maritalStatus, setMaritalStatus] = useState("");
  const [nationality, setNationality] = useState("");
  const [homeTown, setHomeTown] = useState("");
  const [email, setEmail] = useState("");
  const [personalEmail, setPersonalEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [alternatePhone, setAlternatePhone] = useState("");
  const [residentialAddress, setResidentialAddress] = useState("");
  const [postalAddress, setPostalAddress] = useState("");
  const [nrc, setNrc] = useState("");
  const [passportNo, setPassportNo] = useState("");
  const [passportExpiry, setPassportExpiry] = useState("");
  const [bloodGroup, setBloodGroup] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [tpin, setTpin] = useState("");
  const [napsaNumber, setNapsaNumber] = useState("");
  const [nhimaNumber, setNhimaNumber] = useState("");
  const [grade, setGrade] = useState("");
  const [emergencyName, setEmergencyName] = useState("");
  const [emergencyRelationship, setEmergencyRelationship] = useState("");
  const [emergencyPhone, setEmergencyPhone] = useState("");
  const [entityId, setEntityId] = useState("");
  const [locationId, setLocationId] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [orgUnitId, setOrgUnitId] = useState("");
  const [managerId, setManagerId] = useState("");
  const [employmentType, setEmploymentType] = useState<string>("Permanent");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [noticePeriodDays, setNoticePeriodDays] = useState("30");

  const references = useApi(async () => {
    if (!USE_REAL) return { legalEntities: [], orgUnits: [], locations: [], workers: [], grades: [] as string[] };
    const [legalEntities, orgUnits, locations, workerPage, stepData] = await Promise.all([
      realApi.legalEntities(),
      realApi.orgUnits(),
      realApi.locations(),
      realApi.employees({ page: 1, pageSize: 500, status: "active" }),
      realApi.setupStepData("employment").catch(() => null),
    ]);
    // The /admin/* list endpoints return paged envelopes {items:[...]}; the
    // reference helpers return whatever the API returned, so unwrap defensively.
    const unwrapItems = (v: unknown): unknown[] => {
      if (Array.isArray(v)) return v;
      if (v && typeof v === "object") {
        const o = v as Record<string, unknown>;
        if (Array.isArray(o.items)) return o.items;
        // setupStepData nests the JSON string under { dataJson }
        if (typeof o.dataJson === "string") return [o];
      }
      return [];
    };
    const toPage = (v: unknown) => {
      if (v && typeof v === "object" && !Array.isArray(v)) return v as Record<string, unknown>;
      return { items: Array.isArray(v) ? v : [] };
    };
    return {
      legalEntities: unwrapItems(legalEntities).map((raw) => {
        const e = raw as Record<string, unknown>;
        return { id: String(e.id ?? ""), registeredName: String(e.registeredName ?? ""), countryCode: String(e.countryCode ?? e.country ?? "") };
      }),
      orgUnits: unwrapItems(orgUnits).map((raw) => {
        const u = raw as Record<string, unknown>;
        return { id: String(u.id ?? ""), name: String(u.name ?? ""), code: String(u.code ?? ""), legalEntityId: u.legalEntityId ? String(u.legalEntityId) : "" };
      }),
      locations: unwrapItems(locations).map((raw) => {
        const l = raw as Record<string, unknown>;
        return { id: String(l.id ?? ""), name: String(l.name ?? ""), code: String(l.code ?? ""), legalEntityId: l.legalEntityId ? String(l.legalEntityId) : "" };
      }),
      workers: adaptWorkers(toPage(workerPage)),
      grades: parseStepGrades(stepData?.dataJson ?? null),
    };
  }, []);

  const gradeOptions = useMemo(() => {
    const mock = (USE_REAL ? references.data?.grades ?? [] : ["Grade 1", "Grade 2", "Manager"]);
    return Array.from(new Set(mock.filter(Boolean)));
  }, [USE_REAL, references.data?.grades]);
  const legalEntities = USE_REAL ? references.data?.legalEntities ?? [] : entities.map((e) => ({ id: e.id, registeredName: e.name, name: e.name, countryCode: e.country }));
  const orgUnits = USE_REAL ? references.data?.orgUnits ?? [] : ["Operations", "Manufacturing", "Logistics", "Finance", "People"].map((name) => ({ id: name, name, code: "", legalEntityId: "" }));
  const locations = USE_REAL ? references.data?.locations ?? [] : entities.flatMap((e) => e.branches.map((name) => ({ id: name, name, code: "", legalEntityId: e.id })));
  const managerOptions = USE_REAL ? references.data?.workers ?? [] : employees;
  const entity = legalEntities.find((e) => String(e.id) === entityId);

/** Re-shape the flat org-unit list into a hierarchy: units whose parentId
 *  points to another unit become children; units without a parent are roots. */
function buildOrgTree(
  units: Array<{ id: string; name: string; code: string; legalEntityId: string; parentId?: string }>,
): OrgTreeNode[] {
  const byId = new Map(units.map((u) => [u.id, u]));
  const roots: OrgTreeNode[] = [];
  const childrenOf = new Map<string, OrgTreeNode[]>();
  for (const u of units) {
    const node: OrgTreeNode = {
      id: u.id,
      code: u.code,
      name: u.name,
      unitType: "department",
      status: "active",
      managerId: null,
      managerName: null,
      effectiveFrom: "",
      effectiveTo: null,
      children: [],
    };
    if (u.parentId && byId.has(u.parentId)) {
      const list = childrenOf.get(u.parentId) ?? [];
      list.push(node);
      childrenOf.set(u.parentId, list);
    } else {
      roots.push(node);
    }
  }
  const attach = (node: OrgTreeNode) => {
    node.children = childrenOf.get(node.id) ?? [];
    for (const child of node.children) attach(child);
  };
  for (const root of roots) attach(root);
  return roots;
}
  const entityLocations = locations.filter((l) => !entityId || String(l.legalEntityId ?? "") === entityId);
  const entityUnits = orgUnits.filter((u) => !entityId || !u.legalEntityId || String(u.legalEntityId) === entityId);
  // Tree-shaped unit list so the department picker shows the org hierarchy
  // (Entity › Department › Team) instead of flat unit names.
  const flatUnits = references.data?.orgUnits ?? [];
  const treeRoots = USE_REAL && flatUnits.length ? buildOrgTree(flatUnits) : demoEntityTree;
  const treeUnits = flattenEntityTree(treeRoots, false);
  const treeOptions = treeToSelectOptions(treeRoots);
  const treeUnitById = new Map(treeUnits.map((t) => [t.unitId, t]));
  const filteredTreeOptions = treeOptions.filter(
    (o) => !o.value.startsWith("entity:") || o.value === `entity:${entityId}`,
  );
  const selectedLocation = locations.find((l) => String(l.id) === locationId);
  const selectedUnit = orgUnits.find((u) => String(u.id) === orgUnitId);

  useEffect(() => {
    if (!entityId && legalEntities.length) setEntityId(String(legalEntities[0].id));
  }, [entityId, legalEntities]);

  useEffect(() => {
    if (entityLocations.length && !entityLocations.some((l) => String(l.id) === locationId))
      setLocationId(String(entityLocations[0].id));
    if (entityUnits.length && !entityUnits.some((u) => String(u.id) === orgUnitId))
      setOrgUnitId(String(entityUnits[0].id));
  }, [entityId, entityLocations, entityUnits, locationId, orgUnitId]);
  const fullName = [firstName, middleName, lastName].filter(Boolean).join(" ");
  const needsEndDate = employmentType === "Fixed term" || employmentType === "Intern";
  const nrcInvalid = nrc.trim() && !NRC.test(nrc.trim());

  /** DOB must be present, well-formed, and within the realistic window. */
  const dobInvalid = Boolean(dateOfBirth) &&
    (!ISO8601.test(dateOfBirth) || dateOfBirth < MIN_DOB || dateOfBirth > TODAY_ISO);
  const hasEmergency = emergencyName.trim().length > 0;

  function parseStepGrades(dataJson: string | null): string[] {
    if (!dataJson) return [];
    try {
      const p = JSON.parse(dataJson);
      return (Array.isArray(p?.Grades) ? p.Grades : [])
        .map((g: { Name?: unknown }) => String(g?.Name ?? "")).filter(Boolean);
    } catch { return []; }
  }

  const steps: FlowStep[] = [
    {
      id: "identity",
      title: "Who is joining",
      purpose: "The details HR collects at the door. Payroll-critical IDs can still be completed later on the profile.",
      validate: () => {
        if (!firstName.trim()) return "First name is required — the record needs a name.";
        if (dobInvalid) return "The date of birth is not valid — pick a realistic date (1900 – today) from the calendar.";
        if (nrcInvalid) return "The NRC number is not valid — enter it as 123456/78/9 (six digits, two, then one).";
        return null;
      },
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="first">First name</Label>
            <Input id="first" className="mt-1" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="last">Last name (if recorded)</Label>
            <Input id="last" className="mt-1" value={lastName} onChange={(e) => setLastName(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="middle">Middle name (optional)</Label>
            <Input id="middle" className="mt-1" value={middleName} onChange={(e) => setMiddleName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="salutation">Salutation (optional)</Label>
            <Select value={salutation || "none"} onValueChange={(v) => setSalutation(v === "none" ? "" : v)}>
              <SelectTrigger id="salutation" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="none">Not recorded</SelectItem>{salutations.map((v) => <SelectItem key={v} value={v}>{v}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="gender">Gender (optional)</Label>
            <Select value={gender || "none"} onValueChange={(v) => setGender(v === "none" ? "" : v)}>
              <SelectTrigger id="gender" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="none">Not recorded</SelectItem>{genders.map((v) => <SelectItem key={v} value={v}>{v}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="marital">Marital status (optional)</Label>
            <Select value={maritalStatus || "none"} onValueChange={(v) => setMaritalStatus(v === "none" ? "" : v)}>
              <SelectTrigger id="marital" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="none">Not recorded</SelectItem>{maritalStatuses.map((v) => <SelectItem key={v} value={v}>{v}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="nationality">Nationality (optional)</Label>
            <Input id="nationality" className="mt-1" value={nationality} onChange={(e) => setNationality(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="home-town">Home town (optional)</Label>
            <Input id="home-town" className="mt-1" value={homeTown} onChange={(e) => setHomeTown(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="phone">Phone number (optional)</Label>
            <Input id="phone" className="mt-1" value={phone} onChange={(e) => setPhone(e.target.value)} />
            <p className="mt-1 text-xs text-muted-foreground">For payroll mobile-money payments and notifications.</p>
          </div>
          <div>
            <Label htmlFor="email">Work email (optional)</Label>
            <Input id="email" type="email" className="mt-1" value={email} onChange={(e) => setEmail(e.target.value)} />
            <p className="mt-1 text-xs text-muted-foreground">
              Leave blank if IT will issue it during onboarding.
            </p>
          </div>
          <div>
            <Label htmlFor="personal-email">Personal email (optional)</Label>
            <Input id="personal-email" type="email" className="mt-1" value={personalEmail} onChange={(e) => setPersonalEmail(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="alternate-phone">Alternate phone (optional)</Label>
            <Input id="alternate-phone" className="mt-1" value={alternatePhone} onChange={(e) => setAlternatePhone(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="nrc">NRC number (optional during pre-hire)</Label>
            <Input
              id="nrc"
              className={"mt-1" + (nrcInvalid ? " border-danger" : "")}
              placeholder="123456/78/9"
              value={nrc}
              onChange={(e) => setNrc(e.target.value)}
            />
            {nrcInvalid ? (
              <p className="mt-1 text-xs text-danger">An NRC looks like 123456/78/9 — six digits, two, then one.</p>
            ) : (
              <p className="mt-1 text-xs text-muted-foreground">Payroll and the bank both check the legal identity against it.</p>
            )}
          </div>
          <div>
            <Label htmlFor="dob">Date of birth (optional during pre-hire)</Label>
            <Input
              id="dob"
              type="date"
              className={"mt-1" + (dobInvalid ? " border-danger" : "")}
              min={MIN_DOB}
              max={TODAY_ISO}
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(readDateInput(e.currentTarget))}
            />
            {dobInvalid ? (
              <p className="mt-1 text-xs text-danger">Pick a realistic date of birth (1900 – today).</p>
            ) : (
              <p className="mt-1 text-xs text-muted-foreground">Use the calendar to avoid typed-value mix-ups.</p>
            )}
          </div>
          <div>
            <Label htmlFor="passport">Passport number (optional)</Label>
            <Input id="passport" className="mt-1" value={passportNo} onChange={(e) => setPassportNo(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="passport-expiry">Passport expiry (optional)</Label>
            <Input id="passport-expiry" type="date" className="mt-1" value={passportExpiry} onChange={(e) => setPassportExpiry(readDateInput(e.currentTarget))} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="address">Residential address (optional)</Label>
            <Input id="address" className="mt-1" value={residentialAddress} onChange={(e) => setResidentialAddress(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="postal-address">Postal address (optional)</Label>
            <Input id="postal-address" className="mt-1" value={postalAddress} onChange={(e) => setPostalAddress(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="blood-group">Blood group (optional)</Label>
            <Input id="blood-group" className="mt-1" value={bloodGroup} onChange={(e) => setBloodGroup(e.target.value)} />
          </div>
        </div>
      ),
    },
    {
      id: "statutory",
      title: "Statutory registrations",
      purpose: "Zambian registrations a pay run cannot run without — complete them now, or finish them on the profile later.",
      validate: () => {
        if (emergencyName.trim() || emergencyRelationship || emergencyPhone.trim()) {
          if (!emergencyName.trim()) return "Give a name for this emergency contact.";
          if (!emergencyRelationship) return "Choose the emergency contact relationship.";
          if (!emergencyPhone.trim()) return "Give a phone number for this emergency contact.";
        }
        return null;
      },
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="tpin">TPIN (ZRA) (optional)</Label>
            <Input id="tpin" className="mt-1" value={tpin} onChange={(e) => setTpin(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="napsa">NAPSA number (optional)</Label>
            <Input id="napsa" className="mt-1" value={napsaNumber} onChange={(e) => setNapsaNumber(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="nhima">NHIMA number (optional)</Label>
            <Input id="nhima" className="mt-1" value={nhimaNumber} onChange={(e) => setNhimaNumber(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="emergency-name">Emergency contact name</Label>
            <Input id="emergency-name" className="mt-1" value={emergencyName} onChange={(e) => setEmergencyName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="emergency-rel">Relationship</Label>
            <Select value={emergencyRelationship || "none"} onValueChange={(v) => setEmergencyRelationship(v === "none" ? "" : v)}>
              <SelectTrigger id="emergency-rel" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select</SelectItem>
                {emergencyRelationships.map((r) => (
                  <SelectItem key={r} value={r}>{r}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="emergency-phone">Emergency contact phone</Label>
            <Input id="emergency-phone" className="mt-1" value={emergencyPhone} onChange={(e) => setEmergencyPhone(e.target.value)} />
          </div>
          <p className="sm:col-span-2 flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              Bank details are collected on the profile under Pay and bank — a pay run needs a
              payment account before it can include this employee.
            </span>
          </p>
        </div>
      ),
    },
    {
      id: "placement",
      title: "Where they sit",
      purpose: "Entity and branch decide which policies, calendar and statutory rules apply.",
      validate: () => {
        if (!entityId) return "A legal entity is required — choose it from the list.";
        if (!locationId) return "A work location is required — pick the branch this employee works at.";
        if (!orgUnitId) return "A department is required — pick the department (or team) this employee belongs to.";
        // Tree options use the plain unit id, but legacy entity: prefixed values
        // would bypass the backend org-unit lookup too.
        if (orgUnitId.startsWith("entity:")) return "A department is required — an entity-level placement is not enough; pick a department or team.";
        return null;
      },
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="entity">Legal entity</Label>
            <Select
              value={entityId}
              onValueChange={(v) => {
                setEntityId(v);
                setLocationId("");
                setOrgUnitId("");
              }}
            >
              <SelectTrigger id="entity" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {legalEntities.map((e) => (
                  <SelectItem key={String(e.id)} value={String(e.id)}>
                    {String(e.registeredName ?? e.id)} · {String(e.countryCode ?? "ZM")}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="branch">Work location</Label>
            <Select value={locationId} onValueChange={setLocationId}>
              <SelectTrigger id="branch" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {entityLocations.map((location) => (
                  <SelectItem key={String(location.id)} value={String(location.id)}>
                    {String(location.name ?? location.code ?? location.id)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="dept">Department</Label>
            <Select value={orgUnitId || filteredTreeOptions[0]?.value || ""} onValueChange={setOrgUnitId}>
              <SelectTrigger id="dept" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {filteredTreeOptions.length
                  ? filteredTreeOptions.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))
                  : entityUnits.map((unit) => (
                      <SelectItem key={String(unit.id)} value={String(unit.id)}>
                        {String(unit.name ?? unit.id)}
                      </SelectItem>
                    ))}
              </SelectContent>
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="manager">Reports to</Label>
            <Select value={managerId || "none"} onValueChange={(value) => setManagerId(value === "none" ? "" : value)}>
              <SelectTrigger id="manager" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No manager assigned</SelectItem>
                {managerOptions.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.fullName} — {e.jobTitle}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      ),
    },
    {
      id: "employment",
      title: "Employment terms",
      purpose: "What kind of engagement this is, what level they join at, and when it starts.",
      validate: () => {
        if (!jobTitle.trim()) return "Job title is required — the contract needs a role.";
        if (!startDate) return "A start date is required.";
        if (!ISO8601.test(startDate)) return "The start date is not valid — pick a date from the calendar.";
        if (needsEndDate && !endDate) return `An end date is required for ${employmentType.toLowerCase()} engagements.`;
        if (needsEndDate && endDate && (startDate && endDate <= startDate)) return "The end date must come after the start date.";
        if (!/^\d+$/.test(noticePeriodDays) || Number(noticePeriodDays) < 0)
          return "Notice period must be zero or a positive number of days.";
        return null;
      },
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="title">Job title</Label>
            <Input id="title" className="mt-1" value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="grade">Grade</Label>
            <Select value={grade || "none"} onValueChange={(v) => setGrade(v === "none" ? "" : v)}>
              <SelectTrigger id="grade" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No grade assigned</SelectItem>
                {gradeOptions.map((g) => (
                  <SelectItem key={g} value={g}>{g}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="mt-1 text-xs text-muted-foreground">
              Grades are managed in the setup wizard — they drive the pay range.
            </p>
          </div>
          <div>
            <Label htmlFor="type">Employment type</Label>
            <Select value={employmentType} onValueChange={setEmploymentType}>
              <SelectTrigger id="type" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {employmentTypes.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="start">Start date</Label>
            <Input id="start" type="date" className="mt-1" value={startDate} onChange={(e) => setStartDate(readDateInput(e.currentTarget))} />
          </div>
          <div>
            <Label htmlFor="notice-days">Notice period (days)</Label>
            <Input id="notice-days" type="number" min="0" className="mt-1" value={noticePeriodDays} onChange={(e) => setNoticePeriodDays(e.target.value)} />
          </div>
          {needsEndDate ? (
            <div className="sm:col-span-2">
              <Label htmlFor="end">
                End date <span className="text-danger">(required for {employmentType.toLowerCase()})</span>
              </Label>
              <Input id="end" type="date" className="mt-1" value={endDate} onChange={(e) => setEndDate(readDateInput(e.currentTarget))} />
              <p className="mt-1 text-xs text-muted-foreground">
                An expiry alert is raised 60 days before this date.
              </p>
            </div>
          ) : null}
          {employmentType === "Contractor" ? (
            <p className="sm:col-span-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span>
                Contractors are engaged, not employed. Check the classification rules for{" "}
                {String(entity?.countryCode ?? "Zambia")} before continuing — misclassification carries real liability.
              </span>
            </p>
          ) : null}
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and create",
      purpose: "Creating the record does not complete the hire — onboarding does.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            {[
              ["Name", fullName || "Not given"],
              ["Job title", jobTitle || "Not given"],
              ["Grade", grade || "Not assigned"],
              ["Employment type", employmentType],
              ["Entity", String(entity?.registeredName ?? "Not selected")],
              ["Work location", String(selectedLocation?.name ?? "Not selected")],
              ["Department", String(selectedUnit?.name ?? "Not selected")],
              ["Reports to", managerOptions.find((e) => e.id === managerId)?.fullName ?? "—"],
              ["Start date", startDate],
              ["Notice period", `${noticePeriodDays} days`],
              ...(needsEndDate ? [["End date", endDate || "Not set"]] : []),
              ["Phone", phone.trim() || "Not given"],
              ["Personal email", personalEmail.trim() || "Not given"],
              ["NRC", nrc.trim() || "Not given"],
              ["Passport", passportNo.trim() || "Not given"],
              ["Date of birth", dateOfBirth || "Not given"],
              ["TPIN", tpin.trim() || "Not given"],
              ["NAPSA", napsaNumber.trim() || "Not given"],
              ["NHIMA", nhimaNumber.trim() || "Not given"],
              ["Emergency contact", hasEmergency ? `${emergencyName.trim()}${emergencyRelationship ? ` (${emergencyRelationship})` : ""}${emergencyPhone.trim() ? `, ${emergencyPhone.trim()}` : ""}` : "Not recorded"],
            ].map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-muted-foreground">
            The employee number is issued automatically. A future start date creates a <em>Pre-hire</em> record;
            a start date of today or earlier creates an active record.
          </p>

          <div className="rounded-md border border-warning/40 bg-warning-soft p-3">
            <p className="text-xs font-medium text-warning">
              Still needed before this person can be paid
            </p>
            <ul className="mt-1.5 list-inside list-disc space-y-0.5 text-xs text-foreground">
              {["Bank name, branch and account number",
                ...(nrc.trim() ? [] : ["NRC number"]),
                ...(dateOfBirth ? [] : ["Date of birth"]),
                ...(tpin.trim() ? [] : ["TPIN"]),
                ...(napsaNumber.trim() ? [] : ["NAPSA number"]),
                ...(nhimaNumber.trim() ? [] : ["NHIMA number"]),
                ...(hasEmergency ? [] : ["An emergency contact"]),
              ].map((missing) => (
                <li key={missing}>{missing}</li>
              ))}
            </ul>
            <p className="mt-1.5 text-xs text-muted-foreground">
              A pay run will not include an employee missing any of these. You can complete them on
              the profile, or the employee supplies them during onboarding.
            </p>
          </div>
        </div>
      ),
    },
  ];

  if (ref) {
    return (
      <AuthGate>
      <AppShell>
        <PageHeader eyebrow="People" title="Employee record created" />
        <NextSteps
          reference={ref}
          title={`${fullName || "The employee"} has been added as ${startDate > TODAY_ISO ? "Pre-hire" : "Active"}`}
          steps={[
            "Complete the profile — bank details and the NAPSA, NHIMA and TPIN numbers, without which payroll cannot include them.",
            "Record an emergency contact. It is the one field that should never be left blank.",
            startDate > TODAY_ISO ? "Review onboarding before the start date." : "Review the profile and finish any missing payroll details.",
          ]}
          actions={
            <>
              <Button asChild>
                {createdId ? <Link to="/hrm/employees/$id" params={{ id: createdId }}>Complete the profile</Link> : <Link to="/hrm/employees">Employees</Link>}
              </Button>
              <Button variant="outline" asChild>
                <Link to="/hrm/employees">Back to employees</Link>
              </Button>
            </>
          }
        />
      </AppShell>
      </AuthGate>
    );
  }

  return (
    <AppShell>
      <PageHeader
        eyebrow="People"
        title="Add an employee"
        description="Five short steps. This creates the record only — onboarding is a separate, tracked process."
        primaryAction={
          <Button variant="ghost" asChild>
            <Link to="/hrm/employees">Cancel</Link>
          </Button>
        }
      />
      <GuidedFlow
        flowId="employee-new"
        steps={steps}
        submitLabel={creating ? "Creating…" : "Create employee record"}
        onSubmit={async () => {
          if (!firstName.trim()) {
            feedback.blocked("A name is required to create the record.", "Go back to the first step and enter the employee's name.");
            setCreating(false);
            return;
          }
          if (nrcInvalid) {
            feedback.blocked("The NRC number is not valid.", "Go back to the first step and enter it as 123456/78/9 (six digits, two, then one).");
            setCreating(false);
            return;
          }
          if (dobInvalid) {
            feedback.blocked("The date of birth is not valid.", "Go back to Personal details and pick the date from the calendar (1900 – today).");
            setCreating(false);
            return;
          }
          if (USE_REAL && (!entityId || !locationId || !orgUnitId)) {
            feedback.blocked(
              "Organisation placement is required.",
              "Choose a legal entity, work location and department configured by HR administration.",
            );
            return;
          }
          if (needsEndDate && !endDate) {
            feedback.blocked(
              "An end date is required for this employment type.",
              "Go back to Employment and enter the agreed final date.",
            );
            return;
          }
          setCreating(true);
          try {
            if (USE_REAL) {
              const created = await realApi.createWorker({
                employeeNo: "",
                firstName: firstName.trim(),
                middleName: middleName.trim() || null,
                lastName: lastName.trim(),
                email: email.trim() || null,
                personalEmail: personalEmail.trim() || null,
                phone: phone.trim() || null,
                nrc: nrc.trim() || null,
                passportNo: passportNo.trim() || null,
                tpin: tpin.trim() || null,
                napsaNumber: napsaNumber.trim() || null,
                nhimaNumber: nhimaNumber.trim() || null,
                nationality: nationality.trim() || null,
                dateOfBirth: dateOfBirth || null,
                profileDetailsJson: JSON.stringify({ salutation, gender, maritalStatus, residentialAddress,
                  postalAddress, homeTown, alternatePhone, passportExpiry, bloodGroup,
                  noticePeriodDays: Number(noticePeriodDays), legalEntityName: String(entity?.registeredName ?? "") }),
                orgUnitId,
                locationId,
                managerId: managerId || null,
                grade: grade || null,
                jobTitle: jobTitle.trim() || null,
                startDate,
                workerType:
                  employmentType === "Contractor" ? "contingent" : employmentType === "Intern" ? "intern" : "employee",
                emergencyContacts: hasEmergency
                  ? [{ relationship: emergencyRelationship || "Other", fullName: emergencyName.trim(), phone: emergencyPhone.trim() || null, isPrimary: true }]
                  : [],
                bankDetails: [],
              });
              await realApi.createWorkerAssignment(String(created.id), {
                workerId: created.id,
                legalEntityId: entityId,
                orgUnitId,
                locationId,
                managerId: managerId || null,
                startDate,
                endDate: needsEndDate ? endDate : null,
                jobTitle: jobTitle.trim() || null,
                grade: grade || null,
                contractType:
                  employmentType === "Fixed term"
                    ? "fixed-term"
                    : employmentType === "Contractor"
                      ? "contractor"
                      : employmentType === "Intern"
                        ? "intern"
                        : employmentType === "Part time"
                          ? "part-time"
                          : "permanent",
                workPattern: employmentType === "Part time" ? "part-time" : "full-time",
                noticeDays: Number(noticePeriodDays),
              });
              setRef(String(created.employeeNo || created.id));
              setCreatedId(String(created.id));
              return;
            }
            const r = await api.submit("employee", { fullName, jobTitle, entityId, locationId, startDate });
            setRef(`TMP-${r.id}`);
          } catch (err) {
            const msg = err instanceof ApiError ? err.message : String(err);
            feedback.blocked("The employee could not be created.", msg);
            throw err;
          } finally {
            setCreating(false);
          }
        }}
      />
    </AppShell>
  );
}
