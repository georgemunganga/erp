/**
 * The complete employee record, beyond the directory entry in `data.ts`.
 *
 * `Employee` is the directory: who someone is, where they sit, enough to list
 * and search them. This is everything else a real HR system holds — identity,
 * contact, next of kin, statutory registrations, schooling, prior employment,
 * dependants and, eventually, how they left.
 *
 * Statutory fields are Zambian: NRC for identity, TPIN for tax, NAPSA for
 * pension and NHIMA for health. A deployment in another country swaps the
 * country pack, not this shape.
 */

const delay = (ms = 380) => new Promise((r) => setTimeout(r, ms));

export type Sensitivity = "open" | "restricted";

export interface EducationRecord {
  id: string;
  qualification: string;
  institution: string;
  field: string;
  completedYear: string;
  verified: boolean;
}

export interface PriorEmployment {
  id: string;
  employer: string;
  jobTitle: string;
  from: string;
  to: string;
  reasonForLeaving: string;
  referenceChecked: boolean;
}

export interface Dependant {
  id: string;
  name: string;
  relationship: string;
  dateOfBirth: string;
  onMedicalScheme: boolean;
}

export interface EmergencyContact {
  id: string;
  name: string;
  relationship: string;
  phone: string;
  isPrimary: boolean;
}

export interface EmployeeProfile {
  employeeId: string;
  profileDetailsJson?: string;

  /* Identity */
  salutation: string;
  gender: string;
  dateOfBirth: string;
  maritalStatus: string;
  legacyEmploymentType?: string;
  legalEntityName?: string;
  nationality: string;
  passportNo?: string;
  passportExpiry?: string;

  /* Contact */
  personalEmail?: string;
  alternatePhone?: string;
  residentialAddress: string;
  postalAddress?: string;
  homeTown?: string;

  emergency: EmergencyContact[];

  /* Employment terms */
  probationEndsOn?: string;
  confirmedOn?: string;
  noticePeriodDays: number;
  reportsTo: string;
  costCentre: string;
  payGroup: string;
  shiftPattern: string;
  holidayCalendar: string;
  leavePolicy: string;
  attendanceDeviceId?: string;

  /* Pay and statutory registrations */
  paymentMethod: string;
  bankDetailId?: string;
  bankName: string;
  bankBranch: string;
  bankAccount: string;
  accountName?: string;
  mobileMoneyNumber?: string;
  tpin: string;
  napsaNumber: string;
  nhimaNumber: string;

  /* Personal — collected only where it changes how someone is supported */
  bloodGroup?: string;
  workplaceAdjustments?: string;
  dietaryRequirements?: string;

  education: EducationRecord[];
  previousEmployment: PriorEmployment[];
  dependants: Dependant[];

  /* Only present once someone has left or is leaving */
  exit?: {
    lastWorkingDay: string;
    reason: string;
    noticeGivenOn: string;
    interviewHeld: boolean;
    eligibleForRehire: boolean;
    note?: string;
  };
}

/** Sensible Zambian defaults, so each record states only what differs. */
function profile(
  employeeId: string,
  over: Partial<EmployeeProfile> & Pick<EmployeeProfile, "salutation" | "gender" | "dateOfBirth" | "residentialAddress" | "tpin" | "napsaNumber" | "nhimaNumber" | "bankAccount" | "reportsTo">,
): EmployeeProfile {
  return {
    employeeId,
    maritalStatus: "Single",
    nationality: "Zambian",
    noticePeriodDays: 30,
    costCentre: "CC-100 Operations",
    payGroup: "Lusaka monthly salaried",
    shiftPattern: "Day shift, Monday to Friday",
    holidayCalendar: "Zambia national 2026",
    leavePolicy: "Standard annual leave — 24 days",
    paymentMethod: "Bank transfer",
    bankName: "Zanaco",
    bankBranch: "Cairo Road, Lusaka",
    education: [],
    previousEmployment: [],
    dependants: [],
    emergency: [],
    ...over,
  };
}

export const employeeProfiles: EmployeeProfile[] = [
  profile("w-1001", {
    salutation: "Ms",
    gender: "Female",
    dateOfBirth: "1988-06-14",
    maritalStatus: "Married",
    passportNo: "ZN0448192",
    passportExpiry: "2029-11-02",
    personalEmail: "c.mwansa88@gmail.com",
    alternatePhone: "+260 96 1180 4472",
    residentialAddress: "Plot 4412, Kabulonga Road, Lusaka",
    postalAddress: "P.O. Box 320145, Lusaka",
    homeTown: "Mansa, Luapula Province",
    reportsTo: "Mutale Kabwe",
    probationEndsOn: "2019-09-11",
    confirmedOn: "2019-09-11",
    noticePeriodDays: 60,
    costCentre: "CC-210 Maintenance planning",
    shiftPattern: "Day shift, Monday to Friday",
    attendanceDeviceId: "LSK-BIO-0142",
    bankBranch: "Cairo Road, Lusaka",
    bankAccount: "0142 1000 3389 21",
    tpin: "1002 4471 88",
    napsaNumber: "NAP-884 122 907",
    nhimaNumber: "NH-2291 4408",
    bloodGroup: "O+",
    workplaceAdjustments: "Ground-floor desk following a 2024 knee injury.",
    emergency: [
      { id: "ec-1", name: "Chileshe Mwansa", relationship: "Spouse", phone: "+260 97 4471 2280", isPrimary: true },
      { id: "ec-2", name: "Beatrice Mwansa", relationship: "Mother", phone: "+260 95 2210 8841", isPrimary: false },
    ],
    education: [
      { id: "ed-1", qualification: "BEng Mechanical Engineering", institution: "University of Zambia", field: "Engineering", completedYear: "2011", verified: true },
      { id: "ed-2", qualification: "Diploma in Maintenance Management", institution: "Copperbelt University", field: "Engineering", completedYear: "2016", verified: true },
    ],
    previousEmployment: [
      { id: "pe-1", employer: "Kafue Steel Ltd", jobTitle: "Maintenance planner", from: "2012-02-01", to: "2019-02-28", reasonForLeaving: "Promotion opportunity", referenceChecked: true },
    ],
    dependants: [
      { id: "dp-1", name: "Mapalo Mwansa-Chileshe", relationship: "Daughter", dateOfBirth: "2015-03-22", onMedicalScheme: true },
      { id: "dp-2", name: "Bupe Mwansa-Chileshe", relationship: "Son", dateOfBirth: "2019-08-09", onMedicalScheme: true },
    ],
  }),

  profile("w-1002", {
    salutation: "Mr",
    gender: "Male",
    dateOfBirth: "1982-01-27",
    maritalStatus: "Married",
    personalEmail: "mutale.kabwe@outlook.com",
    residentialAddress: "House 18, Roma Township, Lusaka",
    homeTown: "Kasama, Northern Province",
    reportsTo: "Thandiwe Banda",
    confirmedOn: "2015-05-01",
    noticePeriodDays: 90,
    costCentre: "CC-210 Maintenance planning",
    bankAccount: "0142 1000 7741 03",
    tpin: "1001 8823 40",
    napsaNumber: "NAP-771 480 226",
    nhimaNumber: "NH-1180 6642",
    bloodGroup: "A+",
    emergency: [
      { id: "ec-1", name: "Mwaka Kabwe", relationship: "Spouse", phone: "+260 97 3308 1194", isPrimary: true },
    ],
    education: [
      { id: "ed-1", qualification: "BSc Production Management", institution: "Copperbelt University", field: "Operations", completedYear: "2005", verified: true },
    ],
    dependants: [
      { id: "dp-1", name: "Chanda Kabwe", relationship: "Son", dateOfBirth: "2012-11-04", onMedicalScheme: true },
    ],
  }),

  profile("w-1003", {
    salutation: "Ms",
    gender: "Female",
    dateOfBirth: "1991-09-03",
    personalEmail: "nalukui.s@gmail.com",
    residentialAddress: "Flat 7B, Northmead, Lusaka",
    homeTown: "Mongu, Western Province",
    reportsTo: "Thandiwe Banda",
    confirmedOn: "2021-01-15",
    costCentre: "CC-410 Finance",
    bankName: "Stanbic",
    bankBranch: "Manda Hill, Lusaka",
    bankAccount: "9033 1188 4420",
    tpin: "1003 9917 22",
    napsaNumber: "NAP-903 118 442",
    nhimaNumber: "NH-3390 1174",
    emergency: [
      { id: "ec-1", name: "Situmbeko Simasiku", relationship: "Brother", phone: "+260 96 8841 2207", isPrimary: true },
    ],
    education: [
      { id: "ed-1", qualification: "ACCA", institution: "ZICA / ACCA", field: "Accounting", completedYear: "2018", verified: true },
    ],
  }),

  profile("w-1004", {
    salutation: "Mr",
    gender: "Male",
    dateOfBirth: "1995-04-19",
    residentialAddress: "Plot 220, Chingola Township",
    homeTown: "Chingola, Copperbelt Province",
    reportsTo: "Mutale Kabwe",
    probationEndsOn: "2026-10-01",
    noticePeriodDays: 30,
    costCentre: "CC-310 Quality",
    payGroup: "Copperbelt monthly salaried",
    holidayCalendar: "Zambia national 2026 + Copperbelt site closures",
    bankName: "FNB",
    bankBranch: "Chingola",
    bankAccount: "6210 4478 9930",
    tpin: "1004 2280 61",
    napsaNumber: "NAP-621 044 789",
    nhimaNumber: "NH-6210 4478",
    emergency: [
      { id: "ec-1", name: "Agness Mwanza", relationship: "Mother", phone: "+260 95 7712 3348", isPrimary: true },
    ],
  }),

  profile("w-1005", {
    salutation: "Mrs",
    gender: "Female",
    dateOfBirth: "1979-12-08",
    maritalStatus: "Married",
    personalEmail: "t.banda@gmail.com",
    residentialAddress: "Plot 91, Ibex Hill, Lusaka",
    homeTown: "Livingstone, Southern Province",
    reportsTo: "Board of directors",
    confirmedOn: "2013-02-01",
    noticePeriodDays: 90,
    costCentre: "CC-500 Executive",
    bankName: "Absa",
    bankBranch: "Longacres, Lusaka",
    bankAccount: "4471 8820 1193",
    tpin: "1005 4471 88",
    napsaNumber: "NAP-447 188 201",
    nhimaNumber: "NH-4471 8820",
    bloodGroup: "B+",
    emergency: [
      { id: "ec-1", name: "Joseph Banda", relationship: "Spouse", phone: "+260 97 1122 4408", isPrimary: true },
    ],
    education: [
      { id: "ed-1", qualification: "MBA", institution: "University of Cape Town", field: "Business", completedYear: "2010", verified: true },
      { id: "ed-2", qualification: "BA Human Resource Management", institution: "University of Zambia", field: "People", completedYear: "2002", verified: true },
    ],
    dependants: [
      { id: "dp-1", name: "Natasha Banda", relationship: "Daughter", dateOfBirth: "2008-06-30", onMedicalScheme: true },
    ],
  }),

  profile("w-1006", {
    salutation: "Mr",
    gender: "Male",
    dateOfBirth: "1990-07-21",
    residentialAddress: "Plot 44, Solwezi Central",
    homeTown: "Solwezi, North-Western Province",
    reportsTo: "Mutale Kabwe",
    costCentre: "CC-620 Logistics",
    payGroup: "Contractor — not on payroll",
    paymentMethod: "Paid through accounts payable, not payroll",
    bankName: "Zanaco",
    bankBranch: "Solwezi",
    bankAccount: "0142 6620 1108",
    tpin: "1006 3308 15",
    napsaNumber: "Not applicable — contractor",
    nhimaNumber: "Not applicable — contractor",
    emergency: [
      { id: "ec-1", name: "Lillian Zulu", relationship: "Sister", phone: "+260 96 4471 9928", isPrimary: true },
    ],
  }),

  profile("w-1007", {
    salutation: "Ms",
    gender: "Female",
    dateOfBirth: "1998-02-11",
    residentialAddress: "Plot 6, Dambwa North, Livingstone",
    homeTown: "Livingstone, Southern Province",
    reportsTo: "Thandiwe Banda",
    probationEndsOn: "2026-12-14",
    costCentre: "CC-410 Finance",
    bankName: "Stanbic",
    bankBranch: "Livingstone",
    bankAccount: "9033 7714 2206",
    tpin: "1007 7714 22",
    napsaNumber: "NAP-903 377 142",
    nhimaNumber: "NH-9033 7714",
    emergency: [
      { id: "ec-1", name: "Peter Chirwa", relationship: "Father", phone: "+260 97 8820 3341", isPrimary: true },
    ],
    education: [
      { id: "ed-1", qualification: "BSc Accounting and Finance", institution: "Mulungushi University", field: "Accounting", completedYear: "2021", verified: false },
    ],
  }),

  profile("w-1008", {
    salutation: "Mr",
    gender: "Male",
    dateOfBirth: "1985-10-30",
    maritalStatus: "Married",
    residentialAddress: "Plot 118, Riverside, Kitwe",
    homeTown: "Kitwe, Copperbelt Province",
    reportsTo: "Mutale Kabwe",
    confirmedOn: "2017-04-01",
    costCentre: "CC-210 Maintenance planning",
    payGroup: "Copperbelt monthly salaried",
    bankName: "FNB",
    bankBranch: "Kitwe",
    bankAccount: "6210 1180 4471",
    tpin: "1008 1180 44",
    napsaNumber: "NAP-621 011 804",
    nhimaNumber: "NH-6210 1180",
    emergency: [
      { id: "ec-1", name: "Ruth Sakala", relationship: "Spouse", phone: "+260 95 3341 7728", isPrimary: true },
    ],
    exit: {
      lastWorkingDay: "2026-09-30",
      reason: "Resignation — relocating to South Africa",
      noticeGivenOn: "2026-08-28",
      interviewHeld: false,
      eligibleForRehire: true,
      // Deliberately does not restate the leave figure — that is derived below,
      // and a second copy here would be one more thing that can go stale.
      note: "Final pay is calculated in the September run, and settles any untaken leave.",
    },
  }),
];

/** Which fields are restricted, so the profile can hide rather than expose them. */
export const RESTRICTED_FIELDS = [
  "dateOfBirth",
  "nationalId",
  "passportNo",
  "bankAccount",
  "tpin",
  "napsaNumber",
  "nhimaNumber",
  "bloodGroup",
  "workplaceAdjustments",
  "dependants",
] as const;

export const employeeProfileApi = {
  profile: async (employeeId: string) => {
    await delay();
    return employeeProfiles.find((p) => p.employeeId === employeeId) ?? null;
  },
};
