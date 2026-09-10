using System;
using System.Collections.Generic;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

// ===================== Workers =====================

public sealed record WorkerListFilters(
    string? Search, string? Status, Guid? LegalEntityId, Guid? OrgUnitId, Guid? LocationId,
    string? WorkerType, string? Grade, bool IncludeArchived = false,
    int Page = 1, int PageSize = 25);

public sealed record WorkerCreateRequest(
    string EmployeeNo, string FirstName, string LastName,
    string? MiddleName = null, string? PreferredName = null, string? Email = null, string? PersonalEmail = null,
    string? Phone = null, string? Nrc = null, string? PassportNo = null,
    string? Tpin = null, string? NapsaNumber = null, string? NhimaNumber = null,
    string? Nationality = null, string? DateOfBirth = null,
    Guid? OrgUnitId = null, Guid? LocationId = null, Guid? ManagerId = null,
    string? Grade = null, string? JobTitle = null, string? StartDate = null,
    string WorkerType = "employee",
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);

public sealed record EmergencyContactCreate(string Relationship, string FullName, string? Phone, bool IsPrimary);

// M15 self-service: the fields a worker may edit on their own record. SubjectId
// is filled server-side from the token, never from client input.
public sealed record WorkerSubjectUpdateRequest(
    string SubjectId,
    string? PreferredName = null, string? Email = null, string? PersonalEmail = null, string? Phone = null,
    string? Nrc = null, string? PassportNo = null, string? Tpin = null,
    string? NapsaNumber = null, string? NhimaNumber = null,
    string? Nationality = null, string? DateOfBirth = null,
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);
public sealed record WorkerBankDetailCreate(string BankName, string BranchCode, string AccountNumber, string AccountName, bool IsPrimary, string PaymentMethod = "bank", string? MobileMoneyNumber = null);
// M27 P0 UX audit: admin binding of a worker record to the identity provider
// subject so self-service surfaces resolve (PUT /workers/{id}/account-link).
public sealed record WorkerAccountLinkRequest(string SubjectId);

public sealed record WorkerUpdateRequest(
    string? FirstName = null, string? MiddleName = null, string? LastName = null,
    string? PreferredName = null, string? Email = null, string? PersonalEmail = null, string? Phone = null,
    string? Nrc = null, string? PassportNo = null, string? Tpin = null,
    string? NapsaNumber = null, string? NhimaNumber = null, string? Nationality = null,
    string? DateOfBirth = null, Guid? OrgUnitId = null, Guid? LocationId = null,
    Guid? ManagerId = null, string? Grade = null, string? JobTitle = null,
    string? Status = null, string? StartDate = null, string? EndDate = null, string? SubjectId = null,
    List<EmergencyContactCreate>? EmergencyContacts = null,
    List<WorkerBankDetailCreate>? BankDetails = null);

public sealed record WorkerDto(
    Guid Id, string EmployeeNo, string FirstName, string? MiddleName, string LastName,
    string FullName, string? PreferredName, string? Email, string? PersonalEmail, string? Phone, string? PhotoUrl,
    string? Nrc, string? PassportNo, string? Tpin, string? NapsaNumber, string? NhimaNumber,
    string? Nationality, string? DateOfBirth, string? SubjectId, string WorkerType, string Status,
    Guid? OrgUnitId, string? OrgUnitName, Guid? LocationId, string? LocationName,
    Guid? ManagerId, string? ManagerName, string? Grade, string? JobTitle,
    string? StartDate, string? EndDate,
    List<EmergencyContactDto> EmergencyContacts, List<WorkerBankDetailDto> BankDetails,
    List<WorkerEducationDto> Education, List<ExternalWorkHistoryDto> ExternalWorkHistory, List<InternalWorkHistoryDto> InternalWorkHistory,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record EmergencyContactDto(Guid Id, string Relationship, string FullName, string? Phone, bool IsPrimary);
public sealed record WorkerBankDetailDto(Guid Id, string BankName, string BranchCode, string AccountNumber, string AccountName, string PaymentMethod, string? MobileMoneyNumber, bool IsPrimary);

// M33: worker history child records — education, previous employers, and moves within this organisation.
public sealed record WorkerEducationDto(Guid Id, string Institution, string Qualification, string? FieldOfStudy, string? Grade, int? StartYear, int? EndYear);
public sealed record ExternalWorkHistoryDto(Guid Id, string Company, string? Role, string? StartDate, string? EndDate, string? Responsibilities);
public sealed record InternalWorkHistoryDto(Guid Id, string OrgUnitName, string? Role, string? Grade, string? StartDate, string? EndDate, string? Reason);
public sealed record EducationRequest(string Institution, string Qualification, string? FieldOfStudy = null, string? Grade = null, int? StartYear = null, int? EndYear = null);
public sealed record ExternalWorkHistoryRequest(string Company, string? Role = null, string? StartDate = null, string? EndDate = null, string? Responsibilities = null);
public sealed record InternalWorkHistoryRequest(string OrgUnitName, string? Role = null, string? Grade = null, string? StartDate = null, string? EndDate = null, string? Reason = null);

// ===================== Assignments & Movements =====================

public sealed record AssignmentCreateRequest(
    Guid WorkerId, Guid LegalEntityId, Guid OrgUnitId, Guid LocationId, string StartDate,
    Guid? ManagerId = null, string? JobTitle = null, string? Grade = null,
    string? PositionNo = null, string ContractType = "permanent",
    string WorkPattern = "full-time", int ProbationMonths = 3, int NoticeDays = 30,
    string? EndDate = null);

public sealed record MovementCreateRequest(
    Guid WorkerId, string MovementType, string Reason, string EffectiveDate,
    Guid? ToOrgUnitId, string? ToJobTitle, string? ToGrade, Guid? ToLocationId,
    Guid? ToManagerId, decimal? SalaryChange);

// ===================== Leave =====================

public sealed record LeaveRequestCreate(
    Guid WorkerId, string LeaveTypeCode, string StartDate, string EndDate,
    bool IsPartialDay = false, string? StartTime = null, string? EndTime = null,
    string? Reason = null, bool EvidenceAttached = false);

public sealed record LeaveBalanceDto(string LeaveTypeCode, string LeaveTypeName,
    decimal Accrued, decimal Taken, decimal Reserved, decimal Expired, decimal Available);

// ===================== Attendance =====================

public sealed record AttendanceCorrectionCreate(
    Guid WorkerId, string WorkDate, string IssueType, string? ProposedClockIn,
    string? ProposedClockOut, string? ProposedStatus, string Reason);

// ===================== Workflows =====================

public sealed record WorkflowDecisionRequest(string Action, string? Reason = null, Guid? DelegatedToId = null);
public sealed record WorkflowRequestDto(Guid Id, string WorkflowType, Guid? SubjectWorkerId, string? SubjectName, string Status, string? PayloadJson,
    string? RejectionReason, string? ReturnNote, Guid? CurrentApproverId, string? CurrentApproverName,
    DateTimeOffset? DueAt, DateTimeOffset? EscalatedAt, DateTimeOffset CreatedAt, List<WorkflowDecisionDto> Decisions);
public sealed record WorkflowDecisionDto(Guid Id, Guid RequestId, Guid ActorId, string? ActorName, string Action, string? Reason, Guid? DelegatedToId, string? DelegatedToName, DateTimeOffset CreatedAt);
public sealed record WorkflowEscalateRequest(Guid ActorId);
public sealed record WorkQueueItemDto(Guid RequestId, string WorkflowType, string Status,
    Guid? SubjectWorkerId, string? SubjectName, string? CurrentApproverName,
    DateTimeOffset? DueAt, bool IsOverdue, DateTimeOffset CreatedAt);

// ===================== HR requests & letters =====================

public sealed record HrRequestCreate(string Category, string Subject, string Body, string Confidentiality = "normal", Guid? WorkerId = null);
public sealed record HrRequestMessageCreate(string Body, bool IsInternalNote = false);
public sealed record HrLetterCreate(string LetterType, string Addressee, string Purpose, Guid? WorkerId = null);

// ===================== Speak up =====================

public sealed record ProtectedDisclosureCreate(string Category, string Severity, string Description);
public sealed record ProtectedDisclosureStatusResponse(string CaseReference, string Status,
    DateTimeOffset? LastUpdatedAt, string? NextStep);

// ===================== Payroll =====================

// ---------- M21: salary structure administration ----------
public sealed record SalaryStructureItemUpsert(Guid ComponentId, decimal? DefaultAmount = null, bool? IsOptional = null, int? Order = null);
public sealed record SalaryStructureDto(Guid Id, string Code, string Name, int Version, bool IsActive,
    List<SalaryStructureItemDto> Items);
public sealed record SalaryStructureItemDto(Guid Id, Guid ComponentId, string ComponentCode, string ComponentName,
    decimal? DefaultAmount, bool IsOptional, int Order);
public sealed record SalaryStructureCreateRequest(string Code, string Name, List<SalaryStructureItemUpsert> Items);
public sealed record SalaryStructureUpdateRequest(string? Name = null, bool? IsActive = null,
    List<SalaryStructureItemUpsert>? Items = null);

public sealed record PayrollRunCreate(Guid PayPeriodId, Guid PayGroupId, bool IsHistorical = false, string? HistoricalReason = null);
public sealed record PayrollRunUpdate(Guid PayPeriodId, Guid PayGroupId, string? ApprovalNote = null);
public sealed record PayrollRunPreflightDto(Guid PayPeriodId, Guid PayGroupId, Guid? LocationId,
    bool Ready, int IncludedWorkerCount, int WarningCount, List<PayrollRunPreflightCheckDto> Checks);
public sealed record PayrollRunPreflightCheckDto(string Id, string Label, string State, string Detail, int Count);
public sealed record PayrollCalculationReadinessDto(Guid RunId, bool Ready, int IncludedWorkerCount,
    int BlockingCount, int WarningCount, List<PayrollCalculationReadinessCheckDto> Checks,
    List<PayrollCalculationReadinessIssueDto> Issues);
public sealed record PayrollCalculationReadinessCheckDto(string Id, string Label, string State, string Detail, int Count);
public sealed record PayrollCalculationReadinessIssueDto(Guid? WorkerId, string EmployeeNo, string WorkerName,
    string Issue, string Severity);
// M41 Gap 3: PayBasis appended as an optional field so existing callers stay binary-compatible.
public sealed record WorkerPayrollProfileCreate(Guid WorkerId, Guid PayGroupId, string EffectiveFrom,
    List<WorkerComponentValueCreate> Values, string? PayBasis = null,
    string? OvertimeCategory = null, decimal? WeeklyOvertimeThresholdHours = null,
    decimal? MonthlyOvertimeDivisor = null);
public sealed record WorkerComponentValueCreate(Guid ComponentId, string? ComponentCode = null, decimal Amount = 0);

// M41 Gap 3: pay-basis control update (salary | timesheet). Timesheet pay is
// not implemented yet — the flag is a planning control for HR.
public sealed record PayBasisUpdateRequest(string PayBasis);
public sealed record OvertimePolicyUpdateRequest(string OvertimeCategory,
    decimal? WeeklyOvertimeThresholdHours = null, decimal? MonthlyOvertimeDivisor = null);
public sealed record WorkerPayrollProfileDto(Guid Id, Guid WorkerId, string? WorkerName, Guid PayGroupId, string? PayGroupName, string EffectiveFrom, List<WorkerComponentValueDto> Values,
    // M41 Gap 3: pay-basis control — "salary" | "timesheet" (timesheet pay not yet implemented; planning flag)
    string PayBasis = "salary", string OvertimeCategory = "ordinary",
    decimal WeeklyOvertimeThresholdHours = 48, decimal MonthlyOvertimeDivisor = 208);
public sealed record WorkerComponentValueDto(Guid ComponentId, string ComponentCode, string ComponentName, decimal Amount);
public sealed record PayrollRunDto(Guid Id, string Status, string PeriodLabel, int EmployeeCount,
    decimal TotalGross, decimal TotalDeductions, decimal TotalNet, decimal TotalEmployerCost,
    int ExceptionCount, string? CalcVersion, DateTimeOffset CreatedAt,
    bool IsReversal = false, Guid? ReversesRunId = null,
    string? PreparedBySubjectId = null, string? ApprovedBySubjectId = null,
    string? ReleasedBySubjectId = null, string PaymentStatus = "not-created",
    string? PaymentFileReference = null, string? PaymentFileGeneratedBySubjectId = null,
    string? PaymentApprovedBySubjectId = null, string? PaymentReleasedBySubjectId = null,
    string? ReconciliationReference = null,
    decimal? ReconciledAmount = null, DateTimeOffset? ReconciledAt = null, Guid? LocationId = null,
    Guid? PayPeriodId = null, Guid? PayGroupId = null, string? ApprovalNote = null,
    bool IsHistorical = false, string? HistoricalReason = null);
public sealed record PayrollRunLineDto(Guid Id, Guid WorkerId, string WorkerName, string EmployeeNo,
    decimal GrossPay, decimal TotalDeductions, decimal NetPay, decimal EmployerCost,
    bool HasException, string? ExceptionReason, List<PayrollLineComponentDto> Components,
    string ExceptionStatus = "open", string? ExceptionDecisionReason = null,
    string? ExceptionDecidedBySubjectId = null, DateTimeOffset? ExceptionDecidedAt = null,
    bool IsExcluded = false,
    // M41 Gap 2: proration accounting (appended so existing callers stay binary-compatible)
    int WorkingDays = 0, int PaymentDays = 0, string? ProrationNote = null);
public sealed record PayrollLineComponentDto(string ComponentCode, string ComponentName,
    string ComponentType, decimal Amount, string Explanation, bool IsStatutory);
public sealed record PayslipDto(Guid Id, string PayslipNo, int Version, decimal GrossPay,
    decimal TotalDeductions, decimal NetPay, string? YtdGross, string? YtdTax, string? YtdNet,
    string Status, string? DocumentUrl, DateTimeOffset? ReleasedAt, Guid? SupersedesId,
    // M24: statutory identity pack snapshotted at payment time (appended so existing callers stay binary-compatible)
    string? WorkerNrc = null, string? WorkerTpin = null,
    string? WorkerNapsaNumber = null, string? WorkerNhimaNumber = null,
    string? WorkerName = null, string? EmployeeNo = null, string? PeriodLabel = null,
    string? PayDate = null, Guid? RunId = null, string Currency = "ZMW",
    List<PayrollLineComponentDto>? Components = null, Guid? LocationId = null);
public sealed record PayrollRunReverseCreate(string? Reason = null);
public sealed record PayrollExceptionDecisionRequest(string Decision, string Reason);
public sealed record PayrollCorrectionRequest(string ComponentCode, decimal Amount, string Reason);
public sealed record PayrollPaymentApprovalRequest(string? Note = null);
public sealed record PayrollReconciliationRequest(string Reference, decimal ActualAmount, string? Note = null);
public sealed record PayrollPaymentReadinessDto(
    Guid RunId, bool Ready, int PayableCount, decimal TotalNet, int MissingBankDetailsCount,
    List<PayrollPaymentReadinessIssueDto> Issues);
public sealed record PayrollPaymentReadinessIssueDto(
    Guid WorkerId, string EmployeeNo, string WorkerName, decimal NetPay, string Issue);
public sealed record PayrollRunEventDto(Guid Id, string Action, string ActorSubjectId,
    string? FromStatus, string? ToStatus, string? Reason, string? DetailsJson, DateTimeOffset CreatedAt);

// M48: one row in the top-HR payroll approval queue. Branch runs move through
// in-review and land here; a calculated branch run waiting to be submitted also
// appears (flagged as not-yet-submitted) so the approver can see the whole
// pipeline at a glance. Control totals come straight off the run so the
// approver can eyeball the period's liability in one table.
public sealed record PayrollQueueItemDto(
    Guid RunId, string Status, string PeriodLabel, Guid? BranchId, string? BranchName,
    Guid EntityId, int EmployeeCount, decimal TotalGross, decimal TotalNet,
    decimal TotalDeductions, decimal TotalEmployerCost, int ExceptionCount,
    string? PreparedBySubjectId, DateTimeOffset? SubmittedAt, DateTimeOffset CreatedAt);

// M48: queue-level control totals for the header cards.
public sealed record PayrollQueueSummaryDto(int RunCount, int TotalEmployees,
    decimal TotalGross, decimal TotalNet, decimal TotalDeductions,
    decimal TotalEmployerCost);

/// <summary>Aggregated statutory liability for one released payroll period
/// (M23): PAYE, NAPSA and NHIMA split by employee/employer share, plus the
/// employer's registration references carried on every statutory filing.</summary>
public sealed record StatutorySummaryDto(string PeriodLabel, int WorkerCount,
    decimal TotalGross, decimal TotalPaye, decimal TotalNapsaEe, decimal TotalNapsaEr,
    decimal TotalNhimaEe, decimal TotalNhimaEr, decimal TotalNet,
    string EmployerName, string EmployerTpin, string NapsaEmployerRef,
    string NhimaEmployerRef, string Currency);
public sealed record EmployerLiabilityRow(string ComponentCode, string ComponentName, string Payer,
    decimal TotalAmount, int WorkerCount);

/// <summary>M24: per-worker statutory identity readiness for every line in a
/// payroll run. The run cannot be released while any worker has a missing ref.</summary>
public sealed record StatutoryReadinessDto(Guid RunId, string? PeriodLabel, bool IsReady,
    int WorkerCount, List<WorkerStatutoryItemDto> Workers);
public sealed record WorkerStatutoryItemDto(Guid WorkerId, string EmployeeNo, string FullName,
    bool HasNrc, bool HasTpin, bool HasNapsaNumber, bool HasNhimaNumber, bool Ready);
public sealed record EmployerLiabilityReportDto(string PeriodLabel, string TaxYear,
    List<EmployerLiabilityRow> Rows, decimal TotalStatutory, DateTimeOffset GeneratedAt);
public sealed record PayslipGenerateRequest(Guid PayslipId);

// ===================== Admin config =====================

public sealed record AdminConfigDto(
    List<LegalEntityDto> LegalEntities, List<WorkLocationDto> Locations, List<OrgUnitDto> OrgUnits,
    List<WorkCalendarDto> Calendars, List<LeaveTypeDto> LeaveTypes,
    List<CapabilityDto> Capabilities, List<PayGroupDto> PayGroups);

public sealed record LegalEntityDto(Guid Id, string Code, string RegisteredName, string? TradingName, string Currency);
public sealed record WorkLocationDto(Guid Id, string Code, string Name, Guid LegalEntityId, string? Type);
public sealed record OrgUnitDto(Guid Id, string Code, string Name, Guid? ParentId, string? UnitType, string Status, string? ManagerName);
public sealed record WorkCalendarDto(Guid Id, string Name, int StandardWeeklyHours, string WeekendDays, int HolidayCount);
public sealed record LeaveTypeDto(Guid Id, string Code, string Name, string Category, int DefaultDaysPerYear, bool IsActive);
public sealed record CapabilityDto(string FeatureKey, string Tier, bool IsEnabled);
public sealed record PayGroupDto(Guid Id, string Code, string Name, string Frequency, string Currency, int CalendarDayOfMonth);

// ===================== Recruitment (M7) =====================

public sealed record VacancyCreate(Guid OrgUnitId, string JobTitle, string Grade, string? Description, string Status = "draft", Guid? RequisitionId = null, Guid? LocationId = null, string? ClosingDate = null);
public sealed record VacancyUpdateRequest(string? JobTitle = null, string? Grade = null, string? Description = null, string? Status = null, Guid? LocationId = null, string? ClosingDate = null);
public sealed record CandidateCreate(Guid VacancyId, string FullName, string? Email = null, string? Phone = null, string? Source = null, string? Notes = null);
public sealed record CandidateAdvanceRequest(string Stage, string? Score = null, string? Notes = null);
public sealed record InterviewCreateRequest(string ScheduledAt, string InterviewType, string? InterviewerName = null, string? Notes = null);
public sealed record InterviewDecisionRequest(int OverallScore, string Recommendation, string? Notes = null);
public sealed record OfferCreate(Guid CandidateId, decimal BaseSalary, string? StartDate = null, string ContractType = "permanent", int ProbationMonths = 3, int NoticeDays = 30, string? Notes = null, string? ExpiresOn = null);
public sealed record PreboardingTaskCreateRequest(string Title, bool Required = true, string? DueDate = null, string? Owner = null);
public sealed record PreboardingTaskUpdateRequest(string Status, string? Notes = null);

// ===================== Requisitions (M38) =====================

public sealed record RequisitionCreate(string JobTitle, string Reason, Guid OrgUnitId, int Headcount = 1, string? Grade = null, Guid? LocationId = null,
    string? HiringManagerName = null, decimal? BudgetAnnual = null, string? BusinessCase = null, Guid? ReplacementWorkerId = null);
public sealed record RequisitionUpdateRequest(string? JobTitle = null, string? Reason = null, int? Headcount = null, string? Grade = null,
    Guid? LocationId = null, string? HiringManagerName = null, decimal? BudgetAnnual = null, string? BusinessCase = null);
public sealed record RequisitionDecisionRequest(string? ApproverName = null, string? Reason = null);

// ===================== Relations (M7) =====================

public sealed record RelationsCaseCreate(Guid? SubjectWorkerId, string CaseType, string Category, string Severity, string Summary, string Description,
    string Confidentiality = "restricted", string? OwnerSubjectId = null, string? DueDate = null, string? RaisedBy = null);
public sealed record RelationsAccessDeclarationRequest(string Decision, string? Notes = null);
public sealed record RelationsCaseAssignRequest(string OwnerSubjectId);
public sealed record RelationsCaseTransitionRequest(string Status, string? Notes = null, string? Findings = null, string? Outcome = null);
public sealed record RelationsActionCreateRequest(string ActionType, string Title, string? OwnerSubjectId = null, string? DueDate = null, string? Notes = null);
public sealed record RelationsActionUpdateRequest(string Status, string? Notes = null);
public sealed record ProtectedDisclosureUpdateRequest(string Status, string? TriageNotes = null, string? Outcome = null, string? AssignedToSubjectId = null);

// ===================== Documents & reports (M8) =====================

public sealed record WorkerDocumentCreate(Guid WorkerId, string Category, string Title, string FileName, string ContentType, string Classification = "internal");
public sealed record ReportQuery(string ReportType, string? FromDate = null, string? ToDate = null, Guid? OrgUnitId = null, Guid? LocationId = null);
public sealed record ReportDto(string ReportType, string GeneratedAt, Dictionary<string, object?> Summary, List<Dictionary<string, object?>> Rows);

// ===================== Organization & config (M1) =====================

// ---------- Legal entities ----------
public sealed record LegalEntityCreateRequest(
    string Code, string RegisteredName, string? TradingName = null,
    string? PacraNumber = null, string? Tpin = null, string? NapsaEmployerRef = null,
    string? NhimaEmployerRef = null, string? WcfcbEmployerRef = null,
    string Currency = "ZMW", string CountryCode = "ZM", bool IsDefault = false);
public sealed record LegalEntityUpdateRequest(
    string? RegisteredName = null, string? TradingName = null, string? PacraNumber = null,
    string? Tpin = null, string? NapsaEmployerRef = null, string? NhimaEmployerRef = null,
    string? WcfcbEmployerRef = null, string? Currency = null, bool? IsDefault = null);
public sealed record LegalEntityDtoFull(
    Guid Id, string Code, string RegisteredName, string? TradingName, string? PacraNumber,
    string? Tpin, string? NapsaEmployerRef, string? NhimaEmployerRef, string? WcfcbEmployerRef,
    string Currency, string CountryCode, bool IsDefault, DateTimeOffset CreatedAt);

// ---------- Work locations ----------
/// <summary>M45: assign a platform user (Keycloak subject) to a branch so the
/// confinement middleware narrows their work scope to it.</summary>
public sealed record UserBranchAssignmentRequest(Guid UserId, string? UserEmail, Guid LocationId);

public sealed record WorkLocationCreateRequest(
    string Code, string Name, Guid LegalEntityId, string? AddressLine = null,
    string? Province = null, string? District = null, string? City = null,
    string Type = "branch", Guid? DefaultCalendarId = null);
public sealed record WorkLocationUpdateRequest(
    string? Name = null, string? AddressLine = null, string? Province = null,
    string? District = null, string? City = null, string? Type = null,
    Guid? DefaultCalendarId = null);
public sealed record WorkLocationDtoFull(
    Guid Id, string Code, string Name, Guid LegalEntityId, string? LegalEntityName,
    string? AddressLine, string? Province, string? District, string? City,
    string Type, Guid? DefaultCalendarId, string? CalendarName, DateTimeOffset CreatedAt);

// ---------- Org units ----------
public sealed record OrgUnitCreateRequest(
    string Code, string Name, Guid LegalEntityId, Guid? ParentId = null,
    string UnitType = "department", string? CostCentreRef = null, Guid? ManagerId = null,
    string EffectiveFrom = null!, string? EffectiveTo = null);
public sealed record OrgUnitUpdateRequest(
    string? Name = null, Guid? ParentId = null, string? UnitType = null,
    string? CostCentreRef = null, Guid? ManagerId = null,
    string? EffectiveTo = null, string? Status = null);
public sealed record OrgUnitDtoFull(
    Guid Id, string Code, string Name, Guid LegalEntityId, string? LegalEntityName,
    Guid? ParentId, string? UnitType, string? CostCentreRef, Guid? ManagerId,
    string? ManagerName, string EffectiveFrom, string? EffectiveTo, string Status,
    DateTimeOffset CreatedAt);
public sealed record OrgUnitCloseRequest(string EffectiveDate, string? Reason = null);
public sealed record OrgUnitTreeDto(
    Guid Id, string Code, string Name, string? UnitType, string Status,
    Guid? ManagerId, string? ManagerName, string EffectiveFrom, string? EffectiveTo,
    List<OrgUnitTreeDto> Children);

// ---------- Work calendars ----------
public sealed record WorkCalendarCreateRequest(
    string Name, Guid LegalEntityId, string CountryCode = "ZM",
    int StandardWeeklyHours = 45, string WeekendDays = "sat,sun", bool IsDefault = false);
public sealed record WorkCalendarUpdateRequest(
    string? Name = null, int? StandardWeeklyHours = null, string? WeekendDays = null,
    bool? IsDefault = null);
public sealed record WorkCalendarDtoFull(
    Guid Id, string Name, Guid LegalEntityId, string? LegalEntityName, string CountryCode,
    int StandardWeeklyHours, string WeekendDays, bool IsDefault, int HolidayCount,
    List<PublicHolidayDto> Holidays, DateTimeOffset CreatedAt);

// ---------- Public holidays ----------
public sealed record PublicHolidayCreateRequest(
    Guid CalendarId, string Name, string HolidayDate, string? ObservedOn = null,
    bool IsRecurring = false, string? Description = null);
public sealed record PublicHolidayUpdateRequest(
    string? Name = null, string? HolidayDate = null, string? ObservedOn = null,
    bool? IsRecurring = null, string? Description = null);
public sealed record PublicHolidayDto(
    Guid Id, Guid CalendarId, string Name, string HolidayDate, string? ObservedOn,
    bool IsRecurring, string? Description);

// ---------- Leave types ----------
public sealed record LeaveTypeCreateRequest(
    string Code, string Name, string Category = "paid", int DefaultDaysPerYear = 24,
    decimal MaxConsecutiveDays = 999, bool RequiresEvidence = false, int MinNoticeDays = 0,
    bool AllowsPartialDays = false, int CarryForwardDays = 0,
    int CarryForwardExpiryMonths = 0, bool AllowNegative = false,
    string EffectiveFrom = null!, string? EffectiveTo = null);
public sealed record LeaveTypeUpdateRequest(
    string? Name = null, string? Category = null, int? DefaultDaysPerYear = null,
    decimal? MaxConsecutiveDays = null, bool? RequiresEvidence = null,
    int? MinNoticeDays = null, bool? AllowsPartialDays = null, int? CarryForwardDays = null,
    int? CarryForwardExpiryMonths = null, bool? AllowNegative = null,
    string? EffectiveTo = null, bool? IsActive = null);
public sealed record LeaveTypeDtoFull(
    Guid Id, string Code, string Name, string Category, int DefaultDaysPerYear,
    decimal MaxConsecutiveDays, bool RequiresEvidence, int MinNoticeDays,
    bool AllowsPartialDays, int CarryForwardDays, int CarryForwardExpiryMonths,
    bool AllowNegative, string EffectiveFrom, string? EffectiveTo, bool IsActive,
    DateTimeOffset CreatedAt);

// ---------- Contract types ----------
public sealed record ContractTypeCreateRequest(
    string Code, string Name, int ProbationDays = 0, int NoticeDays = 30);
public sealed record ContractTypeUpdateRequest(
    string? Name = null, int? ProbationDays = null, int? NoticeDays = null,
    bool? IsActive = null);
public sealed record ContractTypeDto(
    Guid Id, string Code, string Name, int ProbationDays, int NoticeDays,
    bool IsActive, DateTimeOffset CreatedAt);

// ---------- Capabilities ----------
public sealed record CapabilityUpdateRequest(string? Tier = null, bool? IsEnabled = null, string? Description = null);

// ===================== Worker lifecycle (M2) =====================

// ---------- Shared shapes (used by routes & tests) ----------
public sealed record EmergencyContactRequest(string Relationship, string FullName, string? Phone = null, bool IsPrimary = false);
public sealed record BankDetailRequest(string BankName, string BranchCode, string AccountNumber,
    string AccountName, string PaymentMethod = "bank", string? MobileMoneyNumber = null, bool IsPrimary = false);
public sealed record AssignmentUpdateRequest(
    Guid? OrgUnitId = null, Guid? LocationId = null, Guid? ManagerId = null,
    string? JobTitle = null, string? Grade = null, string? PositionNo = null,
    string? ContractType = null, string? WorkPattern = null, int? ProbationMonths = null,
    int? NoticeDays = null, string? EndDate = null, string? EffectiveTo = null, string? Status = null);
// AssignmentDto and MovementDto are defined in Workers/WorkerService.cs (shared surface).
// M2 lifecycle adds its own richer projections below.
public sealed record OnboardingPlanDto(Guid WorkerId, bool IsOnboarded, int TasksCompleted, int TasksTotal);
public sealed record MovementImpactDto(string Field, string From, string To);
public sealed record MovementDetailDto(
    Guid Id, Guid WorkerId, string MovementType, string Status, string EffectiveDate,
    string Reason, Guid? FromOrgUnitId, string? FromOrgUnitName, string? FromJobTitle,
    string? FromGrade, Guid? ToOrgUnitId, string? ToOrgUnitName, string? ToJobTitle,
    string? ToGrade, Guid? ToLocationId, Guid? ToManagerId, decimal? SalaryChange,
    DateTimeOffset CreatedAt);
public sealed record OffboardingResultDto(bool Cleared, string[] OpenItems);

// ===================== Time — attendance, roster, decisions (M3) =====================

/// <summary>Clock punch response with the derived worker state.</summary>
public sealed record PunchResultDto(Guid Id, Guid WorkerId, string WorkDate, string ClockIn, string ClockOut,
    string Source, string DerivedStatus, decimal TotalHours, string State);

public sealed record AttendanceRecordDto(Guid Id, Guid WorkerId, string WorkerName, string WorkDate,
    string? ClockIn, string? ClockOut, string Source, string DerivedStatus, decimal TotalHours,
    decimal ScheduledHours = 0, decimal RegularHours = 0, decimal OvertimeHours = 0,
    decimal OvertimeMultiplier = 0, Guid? ShiftId = null, Guid? ImportBatchId = null,
    string OvertimeStatus = "none", string? OvertimeDecisionReason = null,
    string? OvertimeDecidedBySubjectId = null, DateTimeOffset? OvertimeDecidedAt = null,
    Guid? OvertimePayrollRunId = null, Guid? OvertimePayrollLineId = null, string WorkerEmployeeNo = "");

public sealed record ShiftCreateRequest(string Code, string Name, string StartTime, string EndTime,
    int UnpaidBreakMinutes = 0, decimal StandardHours = 8, decimal DailyOvertimeThresholdHours = 8,
    decimal WeekdayOvertimeMultiplier = 1.5m, decimal RestDayOvertimeMultiplier = 2,
    decimal HolidayOvertimeMultiplier = 2);
public sealed record ShiftUpdateRequest(string Name, string StartTime, string EndTime,
    int UnpaidBreakMinutes = 0, decimal StandardHours = 8, decimal DailyOvertimeThresholdHours = 8,
    decimal WeekdayOvertimeMultiplier = 1.5m, decimal RestDayOvertimeMultiplier = 2,
    decimal HolidayOvertimeMultiplier = 2);
public sealed record ShiftDto(Guid Id, string Code, string Name, string StartTime, string EndTime,
    int UnpaidBreakMinutes, decimal StandardHours, decimal DailyOvertimeThresholdHours,
    decimal WeekdayOvertimeMultiplier, decimal RestDayOvertimeMultiplier,
    decimal HolidayOvertimeMultiplier, bool IsActive);
public sealed record ShiftAssignmentRequest(Guid ShiftId, Guid? CalendarId, string EffectiveFrom, string? EffectiveTo = null);
public sealed record ShiftAssignmentDto(Guid Id, Guid WorkerId, Guid ShiftId, string ShiftName,
    Guid? CalendarId, string? CalendarName, string EffectiveFrom, string? EffectiveTo);
public sealed record AttendanceImportRow(string EmployeeNo, string WorkDate, string? ClockIn, string? ClockOut);
public sealed record AttendanceImportRequest(string FileName, List<AttendanceImportRow> Rows);
public sealed record AttendanceImportResultDto(Guid BatchId, string FileName, string Status,
    int RowCount, int ImportedCount, int UpdatedCount, int RejectedCount, List<string> Errors);
public sealed record OvertimeImportRow(string EmployeeNo, string WorkDate, decimal OvertimeHours,
    decimal? OvertimeMultiplier = null, string? Reason = null, string? Status = null);
public sealed record OvertimeImportRequest(string FileName, List<OvertimeImportRow> Rows, bool MarkApproved = false);
public sealed record OvertimeDecisionRequest(string Action, string? Reason = null);
public sealed record AttendanceImportHistoryDto(Guid BatchId, string FileName, string Status,
    int RowCount, int ImportedCount, int UpdatedCount, int RejectedCount,
    string ImportedBySubjectId, DateTimeOffset CreatedAt);
public sealed record TimeAuditEntryDto(Guid Id, string EntityType, string EntityId, string Action,
    string ActorSubjectId, string? BeforeJson, string? AfterJson, DateTimeOffset CreatedAt);
public sealed record LeaveAccrualRunRequest(string Period);
public sealed record LeaveAccrualRunDto(Guid Id, string Period, string Status, int WorkerCount,
    int LedgerEntryCount, decimal TotalDaysAccrued, string RunBySubjectId, DateTimeOffset CreatedAt);
public sealed record LeaveBalanceAdjustmentRequest(Guid WorkerId, string LeaveTypeCode, decimal Days, string Reason);
public sealed record LeaveBalanceAdjustmentDto(Guid Id, Guid WorkerId, string WorkerName,
    string LeaveTypeCode, decimal Days, string Reason, string AdjustedBySubjectId, DateTimeOffset CreatedAt);
public sealed record EscalationRunDto(int Reviewed, int Escalated, DateTimeOffset RunAt);

// M41 Gap 6a: leave encashment request/response contracts.
public sealed record LeaveEncashmentCreateRequest(Guid WorkerId, string LeaveTypeCode, decimal Days, string? Note);
public sealed record LeaveEncashmentDecideRequest(string Action, string? Reason);
public sealed record LeaveEncashmentRateQuote(decimal MonthlyBasic, decimal DailyRate, decimal EstimatedGross, string Currency);
public sealed record LeaveEncashmentRequestDto(Guid Id, Guid WorkerId, string WorkerName, string? EmployeeNo,
    string LeaveTypeCode, decimal Days, decimal MonthlyRate, decimal GrossAmount, string Note,
    string Status, string CreatedBySubjectId, string? DecisionReason, DateTimeOffset CreatedAt, Guid? LocationId = null);
public sealed record LeaveEncashmentHistoryDto(Guid Id, Guid WorkerId, string WorkerName, string LeaveTypeCode,
    decimal Days, decimal GrossAmount, string Status, string CreatedBySubjectId, DateTimeOffset CreatedAt);
public sealed record TimeOperationsHistoryDto(List<AttendanceImportHistoryDto> Imports,
    List<LeaveAccrualRunDto> Accruals, List<LeaveBalanceAdjustmentDto> Adjustments,
    List<LeaveEncashmentHistoryDto> Encashments, List<TimeAuditEntryDto>? TimeAudits = null);

/// <summary>Roster day for the worker: expected shift, attendance, exceptions, cutoff.</summary>
public sealed record RosterDayDto(string Date, string DayLabel, bool IsWorkingDay, string? ClockIn, string? ClockOut,
    string? Status, string? ShiftName, string? ShiftStart, string? ShiftEnd, string? CalendarName,
    bool IsPublicHoliday, string? PublicHolidayName, string? PayrollCutoff, string? CorrectionRef);

/// <summary>Decision on a leave request or attendance correction (submitted via
/// /time/leave/{id}/decide or /time/corrections/{id}/decide).</summary>
public sealed record TimeDecisionRequest(string Action, string? Reason = null);

// ===================== M28: jobs, roles, retention =====================
public sealed record JobCreateRequest(string Code, string Title, Guid? OrgUnitId = null, string? Grade = null);
public sealed record JobUpdateRequest(string? Title = null, Guid? OrgUnitId = null, string? Grade = null);
public sealed record JobDto(Guid Id, string Code, string Title, Guid? OrgUnitId, string? OrgUnitName, string? Grade, string Status);

public sealed record TenantRoleDto(Guid Id, string RoleKey, string RoleName, string Category, bool Active, string[] Permissions);
public sealed record RoleCreateRequest(string RoleKey, string RoleName, string Category, string[]? Permissions = null, bool Active = true);
public sealed record RoleUpdateRequest(bool? Active = null, string? RoleName = null, string? Category = null, string[]? Permissions = null);

public sealed record DataRetentionCreateRequest(string RecordType, int RetentionMonths, string? Description = null);
public sealed record DataRetentionUpdateRequest(int? RetentionMonths = null, string? Description = null, bool? Active = null);
public sealed record DataRetentionDto(Guid Id, string RecordType, string? Description, int RetentionMonths, bool Active);

// ===================== M50 setup wizard step inputs =====================
// Each wizard step POSTs its own strongly-typed JSON to POST /setup/steps/{key}.
// Records use positional construction only (the .NET 10 SDK compiler is broken
// for named arguments on record ctors — CS1739).

/// Step 1 — Organisation: the legal entity the wizard provisions.
public sealed record WizardOrgInput(
    string RegisteredName, string? TradingName, string? PacraNumber, string? Tpin,
    string? NapsaEmployerRef, string? NhimaEmployerRef, string Currency = "ZMW");

/// Step 2 — Structure: one row per branch and one row per department.
public sealed record WizardBranchInput(
    string Name, string? Code, string? AddressLine, string? City,
    string? Province, string? District, string Type = "branch");
public sealed record WizardDeptInput(string Name, string UnitType = "department", string? ManagerName = null);
public sealed record WizardStructureInput(List<WizardBranchInput> Branches, List<WizardDeptInput> Departments);

/// Step 3 — Employment: grades and positions stored as JSON for dropdown use.
public sealed record WizardGradeInput(string Name);
public sealed record WizardPositionInput(string Name, string? GradeName = null);
public sealed record WizardEmploymentInput(List<WizardGradeInput> Grades, List<WizardPositionInput> Positions);

/// Step 4 — Working time (optional): weekly hours, weekend days, public holidays.
public sealed record WizardHolidayInput(string Name, string Date, bool IsRecurring = true);
public sealed record WizardWorkingTimeInput(
    int StandardWeeklyHours = 45, string WeekendDays = "sat,sun",
    List<WizardHolidayInput>? PublicHolidays = null);

/// Step 5 — Leave: leave types with Zambian defaults (Annual 24, Sick 30, …).
public sealed record WizardLeaveTypeInput(
    string Name, string? Code, string Category, int DaysPerYear,
    bool RequiresEvidence = false, int CarryForwardDays = 0);
public sealed record WizardLeaveInput(List<WizardLeaveTypeInput> LeaveTypes);

/// Step 6 — Payroll: pay cycle, payday, pay basis and statutory confirmation.
public sealed record WizardPayrollInput(
    string Frequency = "monthly", int PaydayDay = 25, string Currency = "ZMW",
    bool PayBasisTimesheet = false, bool ConfirmStatutory = false,
    decimal? BasicDefaultAmount = null);

/// Step 7 — Policies (optional): contract types with probation and notice periods.
public sealed record WizardContractTypeInput(string Name, int ProbationDays = 0, int NoticeDays = 0);
public sealed record WizardPolicyInput(List<WizardContractTypeInput> ContractTypes);

/// Step 8 — Roles: platform users to onboard as HR administrators.
public sealed record WizardRolesInput(List<string> AdminEmails);

/// Step 9 — Employees: mapped rows built client-side from spreadsheet upload.
public sealed record WizardEmployeeRow(
    string FirstName, string LastName, string? Email = null, string? Phone = null,
    string? JobTitle = null, string? Grade = null, string? StartDate = null,
    string? OrgUnitName = null, string? WorkerType = null);
public sealed record WizardEmployeesInput(List<WizardEmployeeRow> Employees);

/// Outcome returned by the employees step so the UI can summarise the import.
public sealed record WizardEmployeesResult(int Created, int Skipped, int ProfilesCreated, List<WizardEmployeeError> Errors);
public sealed record WizardEmployeeError(int Row, string Detail);

/// Pay group creation surface (M50: wizard + future admin UI).
public sealed record PayGroupCreateRequest(
    string Code, string Name, string Frequency = "monthly", string Currency = "ZMW",
    int CalendarDayOfMonth = 25, int InputCutoffDaysBeforePayday = 5, bool IsDefault = true);
