using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>Read-only configuration surface consumed by the admin screens: the
/// frontend's AdminConfigClient maps 1:1 onto AdminConfigDto.</summary>
public interface IConfigService
{
    Task<AdminConfigDto> GetConfigAsync(CancellationToken ct);
    Task<Paged<LeaveTypeDto>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct);
}

/// <summary>Recruitment (M7) surface.</summary>
public interface IRecruitmentService
{
    Task<Paged<VacancyDto>> ListVacanciesAsync(string? status, CancellationToken ct);
    Task<VacancyDto> CreateVacancyAsync(VacancyCreate request, CancellationToken ct);
    Task<VacancyDto> UpdateVacancyAsync(Guid vacancyId, VacancyUpdateRequest request, CancellationToken ct);
    Task<VacancyDto> PublishVacancyAsync(Guid vacancyId, CancellationToken ct);
    Task<VacancyDto> CloseVacancyAsync(Guid vacancyId, CancellationToken ct);
    Task<Paged<CandidateDto>> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct);
    Task<CandidateDto> CreateCandidateAsync(CandidateCreate request, CancellationToken ct);
    Task<CandidateDetailDto> GetCandidateAsync(Guid candidateId, CancellationToken ct);
    Task<CandidateDto> AdvanceCandidateAsync(Guid candidateId, CandidateAdvanceRequest request, CancellationToken ct);
    Task<InterviewDto> CreateInterviewAsync(Guid candidateId, InterviewCreateRequest request, CancellationToken ct);
    Task<InterviewDto> DecideInterviewAsync(Guid interviewId, InterviewDecisionRequest request, CancellationToken ct);
    Task<OfferDto> CreateOfferAsync(OfferCreate request, CancellationToken ct);
    Task<Paged<OfferDto>> ListOffersAsync(string? status, CancellationToken ct);
    Task<OfferDto> ApproveOfferAsync(Guid offerId, CancellationToken ct);
    Task<OfferDto> IssueOfferAsync(Guid offerId, CancellationToken ct);
    Task<OfferDto> DeclineOfferAsync(Guid offerId, CancellationToken ct);
    Task<OfferAcceptResultDto> AcceptOfferAsync(Guid offerId, OfferAcceptRequest request, CancellationToken ct);
    Task<Paged<PreboardingCaseDto>> ListPreboardingAsync(string? status, CancellationToken ct);
    Task<PreboardingCaseDto> GetPreboardingAsync(Guid caseId, CancellationToken ct);
    Task<PreboardingTaskDto> AddPreboardingTaskAsync(Guid caseId, PreboardingTaskCreateRequest request, CancellationToken ct);
    Task<PreboardingTaskDto> UpdatePreboardingTaskAsync(Guid caseId, Guid taskId, PreboardingTaskUpdateRequest request, CancellationToken ct);
    Task<PreboardingCaseDto> ActivatePreboardingAsync(Guid caseId, CancellationToken ct);
    Task<CandidateDocumentDto> AddCandidateDocumentAsync(Guid candidateId, string category, string title, string fileName, string contentType, long sizeBytes, string storagePath, CancellationToken ct);
    Task<(CandidateDocument Document, Stream Stream)> GetCandidateDocumentAsync(Guid documentId, CancellationToken ct);
    // M38: pipeline funnel stats and offer letter generation
    Task<VacancyPipelineStatsDto> GetVacancyPipelineAsync(Guid vacancyId, CancellationToken ct);
    Task<OfferLetterDto> GetOfferLetterAsync(Guid offerId, CancellationToken ct);
    // M38: requisitions
    Task<Paged<RequisitionDto>> ListRequisitionsAsync(string? status, CancellationToken ct);
    Task<RequisitionDetailDto> GetRequisitionAsync(Guid requisitionId, CancellationToken ct);
    Task<RequisitionDto> CreateRequisitionAsync(RequisitionCreate request, CancellationToken ct);
    Task<RequisitionDto> UpdateRequisitionAsync(Guid requisitionId, RequisitionUpdateRequest request, CancellationToken ct);
    Task<RequisitionDto> ApproveRequisitionAsync(Guid requisitionId, RequisitionDecisionRequest request, CancellationToken ct);
    Task<RequisitionDto> ReturnRequisitionAsync(Guid requisitionId, RequisitionDecisionRequest request, CancellationToken ct);
    Task<RequisitionDto> SubmitRequisitionAsync(Guid requisitionId, CancellationToken ct);
}
public sealed record VacancyDto(Guid Id, string JobTitle, string? Grade, string Status, string OrgUnitName, DateTimeOffset CreatedAt, int CandidateCount = 0, Guid? RequisitionId = null, string? LocationName = null, string? ClosingDate = null, string? Description = null);
public sealed record CandidateDto(Guid Id, Guid VacancyId, string FullName, string? Email, string? Phone, string Stage, string? Notes, DateTimeOffset CreatedAt, Guid? WorkerId = null);
public sealed record OfferDto(Guid Id, Guid CandidateId, decimal BaseSalary, string ContractType, string Status, DateTimeOffset CreatedAt,
    string? CandidateName = null, string? JobTitle = null, string? StartDate = null, string? ExpiresOn = null,
    DateTimeOffset? ApprovedAt = null, DateTimeOffset? IssuedAt = null, DateTimeOffset? RespondedAt = null);
public sealed record InterviewDto(Guid Id, Guid CandidateId, string ScheduledAt, string InterviewType, string? InterviewerName,
    string Status, int? OverallScore, string? Recommendation, string? Notes, DateTimeOffset CreatedAt);
public sealed record CandidateStageEventDto(Guid Id, string FromStage, string ToStage, string? Score, string? Notes, DateTimeOffset CreatedAt);
public sealed record CandidateDocumentDto(Guid Id, Guid CandidateId, string Category, string Title, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record PreboardingTaskDto(Guid Id, string Code, string Title, bool Required, string Status, string? DueDate, string? Owner, string? Notes, DateTimeOffset? CompletedAt);
public sealed record PreboardingCaseDto(Guid Id, Guid CandidateId, string CandidateName, Guid WorkerId, string EmployeeNo, Guid AssignmentId,
    string JobTitle, string Status, string StartDate, int CompletedTasks, int TotalTasks, List<PreboardingTaskDto> Tasks, DateTimeOffset CreatedAt);
public sealed record CandidateDetailDto(CandidateDto Candidate, VacancyDto Vacancy, List<InterviewDto> Interviews,
    List<OfferDto> Offers, List<CandidateStageEventDto> History, List<CandidateDocumentDto> Documents, PreboardingCaseDto? Preboarding);

// M38: recruitment pipeline DTOs
public sealed record VacancyPipelineStatsDto(Guid VacancyId, string JobTitle, string Status,
    int Applied, int Screening, int Shortlisted, int Interviewing, int Interviewed,
    int Offered, int Preboarding, int Hired, int Rejected, int Total);
public sealed record OfferLetterDto(Guid OfferId, string CandidateName, string JobTitle, decimal BaseSalary,
    string Currency, string ContractType, string? StartDate, int ProbationMonths, int NoticeDays,
    string Status, string LetterBody);
public sealed record RequisitionDto(Guid Id, string RequisitionNo, string JobTitle, string Reason, Guid? ReplacementWorkerId, int Headcount,
    string? Grade, string OrgUnitName, string? LocationName, string? HiringManagerName, decimal? BudgetAnnual, string Currency,
    string? BusinessCase, string Status, string? ApproverName, DateTimeOffset? ApprovedAt, string? ReturnedReason,
    string? RaisedByName, DateTimeOffset CreatedAt, int VacancyCount = 0);
public sealed record RequisitionDetailDto(RequisitionDto Requisition, List<RequisitionEventDto> Events, List<VacancyDto> Vacancies);
public sealed record RequisitionEventDto(string Action, string ActorSubjectId, string? FromStatus, string? ToStatus, string? Notes, DateTimeOffset CreatedAt);
/// <summary>M7: result of accepting an offer — the candidate is converted into a
/// preboarding worker record with an initial assignment (ties to M2 onboarding).</summary>
public sealed record OfferAcceptResultDto(Guid OfferId, Guid WorkerId, string EmployeeNo, Guid AssignmentId, string Status);
/// <summary>M7: accept an issued offer and convert the candidate to a worker.</summary>
public sealed record OfferAcceptRequest(string? EmployeeNo = null, string? StartDate = null, Guid? LocationId = null, Guid? LegalEntityId = null);
public sealed record CandidateStageLogEntry(string Stage, string? Score, string? Notes, DateTimeOffset ChangedAt);

/// <summary>Employee relations cases (M7): restricted-access case records.</summary>
public interface IRelationsService
{
    Task<Paged<RelationsCaseDto>> ListCasesAsync(string? category, CancellationToken ct);
    Task<RelationsCaseDto> CreateCaseAsync(RelationsCaseCreate request, CancellationToken ct);
    Task<RelationsCaseDto> UpdateCaseAsync(Guid caseId, RelationsCaseUpdate request, CancellationToken ct);
    Task<RelationsCaseDetailDto> GetCaseAsync(Guid caseId, string actorSubjectId, CancellationToken ct);
    Task<RelationsAccessDto> DeclareAccessAsync(Guid caseId, RelationsAccessDeclarationRequest request, string actorSubjectId, CancellationToken ct);
    Task<RelationsCaseDto> AssignCaseAsync(Guid caseId, RelationsCaseAssignRequest request, string actorSubjectId, CancellationToken ct);
    Task<RelationsCaseDetailDto> TransitionCaseAsync(Guid caseId, RelationsCaseTransitionRequest request, string actorSubjectId, CancellationToken ct);
    Task<RelationsActionDto> AddActionAsync(Guid caseId, RelationsActionCreateRequest request, string actorSubjectId, CancellationToken ct);
    Task<RelationsActionDto> UpdateActionAsync(Guid caseId, Guid actionId, RelationsActionUpdateRequest request, string actorSubjectId, CancellationToken ct);
    Task<RelationsEvidenceDto> AddEvidenceAsync(Guid caseId, string title, string evidenceType, string fileName, string contentType, long sizeBytes, string storagePath, string actorSubjectId, CancellationToken ct);
    Task<(RelationsEvidence Evidence, Stream Stream)> GetEvidenceAsync(Guid evidenceId, string actorSubjectId, CancellationToken ct);
    Task<Paged<ProtectedDisclosureInvestigationDto>> ListProtectedDisclosuresAsync(string? status, CancellationToken ct);
    Task<ProtectedDisclosureInvestigationDto> GetProtectedDisclosureAsync(Guid id, string actorSubjectId, CancellationToken ct);
    Task<ProtectedDisclosureInvestigationDto> UpdateProtectedDisclosureAsync(Guid id, ProtectedDisclosureUpdateRequest request, string actorSubjectId, CancellationToken ct);
}
public sealed record RelationsCaseUpdate(string? Status, string? Severity, string? Summary, string? Description, string? Outcome = null);
public sealed record RelationsCaseDto(Guid Id, Guid? SubjectWorkerId, string CaseType, string Category, string Severity, string Summary, string Status, DateTimeOffset CreatedAt,
    string? Reference = null, string Confidentiality = "restricted", string? OwnerSubjectId = null, string? DueDate = null);
public sealed record RelationsAccessDto(Guid Id, Guid CaseId, string ActorSubjectId, string Decision, string? Notes, DateTimeOffset CreatedAt);
public sealed record RelationsEventDto(Guid Id, string Action, string ActorSubjectId, string? FromStatus, string? ToStatus, string? Notes, DateTimeOffset CreatedAt);
public sealed record RelationsActionDto(Guid Id, string ActionType, string Title, string Status, string? OwnerSubjectId, string? DueDate, string? Notes, DateTimeOffset? CompletedAt);
public sealed record RelationsEvidenceDto(Guid Id, string Title, string EvidenceType, string FileName, string ContentType, long SizeBytes, string Classification, string AddedBySubjectId, DateTimeOffset CreatedAt);
public sealed record RelationsCaseDetailDto(RelationsCaseDto Case, string Description, string? Findings, string? Outcome, string? RaisedBy,
    List<RelationsActionDto> Actions, List<RelationsEvidenceDto> Evidence, List<RelationsEventDto> History, List<RelationsAccessDto> AccessDeclarations);
public sealed record ProtectedDisclosureInvestigationDto(Guid Id, string CaseReference, string Category, string Severity, string Status,
    string? Description, string? TriageNotes, string? Outcome, string? AssignedToSubjectId, DateTimeOffset CreatedAt, List<RelationsEventDto> History);

/// <summary>Documents & reports (M8).</summary>
public interface IDocumentsService
{
    Task<Paged<WorkerDocumentDto>> ListDocumentsAsync(Guid workerId, CancellationToken ct);
    Task<WorkerDocumentDto> UploadDocumentAsync(Guid workerId, string category, string title, string fileName, string contentType, long sizeBytes, string storagePath, CancellationToken ct);
    Task<(WorkerDocument Document, Stream Stream)> GetDocumentStreamAsync(Guid documentId, CancellationToken ct);
    Task<MyDocumentsDto> ListMyDocumentsAsync(string subjectId, CancellationToken ct);
    Task<WorkerDocumentDto> UploadMyDocumentAsync(string subjectId, string category, string title, string fileName, string contentType, long sizeBytes, string storagePath, CancellationToken ct);
    Task<(WorkerDocument Document, Stream Stream)> GetMyDocumentStreamAsync(Guid documentId, string subjectId, CancellationToken ct);
    Task<ReportDto> GetReportAsync(ReportQuery query, CancellationToken ct);
}
public sealed record WorkerDocumentDto(Guid Id, Guid WorkerId, string Category, string Title, string FileName, string ContentType, long SizeBytes, string Classification, string? ExpiryDate, DateTimeOffset? CreatedAt = null);

/// <summary>Data-quality engine (M8): workspace rules evaluated per worker or
/// across the tenant — completeness, identity duplicates, and expiring documents.</summary>
public interface IDqService
{
    Task<List<DqResult>> RunChecksAsync(CancellationToken ct);
}
public sealed record DqResult(string Rule, string Severity, Guid WorkerId, string Detail);

/// <summary>Statutory export engine (M8): Zambian remittance files and registers
/// produced from released payroll runs.</summary>
public interface IStatutoryExportService
{
    Task<string> GenerateAsync(string exportType, Guid payPeriodId, CancellationToken ct);
    Task<StatutoryExportPreviewDto> PreviewAsync(string exportType, Guid payPeriodId, CancellationToken ct);
    /// <summary>M23: aggregate statutory liability totals for one period.</summary>
    Task<StatutorySummaryDto> SummaryAsync(Guid payPeriodId, CancellationToken ct);
}
public sealed record StatutoryExportPreviewDto(string ExportType, string PeriodLabel, string Currency,
    List<string> TemplateColumns, List<Dictionary<string, string>> Rows);

/// <summary>Persistence contracts implemented by EF Core in Infrastructure.</summary>
public interface IConfigRepository
{
    Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct);
    Task<List<WorkLocation>> ListLocationsAsync(CancellationToken ct);
    Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct);
    Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct);
    Task<List<LeaveType>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct);
    Task<List<ContractType>> ListContractTypesAsync(bool includeInactive, CancellationToken ct);
    Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct);
    Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<Worker>> ListAllWorkersAsync(string? status, CancellationToken ct);
    Task<List<LeaveRequest>> ListLeaveRequestsAllAsync(string? status, CancellationToken ct);
    Task<List<PayrollRunLine>> ListRunLinesAllAsync(string periodFrom, string periodTo, CancellationToken ct);

    // M1 CRUD contracts
    Task<LegalEntity?> GetLegalEntityAsync(Guid id, CancellationToken ct);
    Task<LegalEntity> CreateLegalEntityAsync(LegalEntity entity, CancellationToken ct);
    Task<LegalEntity> UpdateLegalEntityAsync(LegalEntity entity, CancellationToken ct);
    Task<WorkLocation?> GetLocationAsync(Guid id, CancellationToken ct);
    Task<WorkLocation> CreateLocationAsync(WorkLocation location, CancellationToken ct);
    Task<WorkLocation> UpdateLocationAsync(WorkLocation location, CancellationToken ct);
    Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct);
    Task<OrgUnit> CreateOrgUnitAsync(OrgUnit unit, CancellationToken ct);
    Task<OrgUnit> UpdateOrgUnitAsync(OrgUnit unit, CancellationToken ct);
    Task<WorkCalendar> CreateCalendarAsync(WorkCalendar calendar, CancellationToken ct);
    Task<WorkCalendar> UpdateCalendarAsync(WorkCalendar calendar, CancellationToken ct);
    Task<PublicHoliday> CreateHolidayAsync(PublicHoliday holiday, CancellationToken ct);
    Task<PublicHoliday?> GetHolidayAsync(Guid id, CancellationToken ct);
    Task<PublicHoliday> UpdateHolidayAsync(PublicHoliday holiday, CancellationToken ct);
    Task DeleteHolidayAsync(Guid id, CancellationToken ct);
    Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct);
    Task<LeaveType> CreateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct);
    Task<LeaveType> UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct);
    Task<ContractType?> GetContractTypeAsync(Guid id, CancellationToken ct);
    Task<ContractType> CreateContractTypeAsync(ContractType contractType, CancellationToken ct);
    Task<ContractType> UpdateContractTypeAsync(ContractType contractType, CancellationToken ct);
    Task<CapabilityConfig> UpdateCapabilityAsync(CapabilityConfig capability, CancellationToken ct);

    // M28: jobs, tenant roles, retention rules
    Task<List<Job>> ListJobsAsync(CancellationToken ct);
    Task<Job?> GetJobAsync(Guid id, CancellationToken ct);
    Task<Job> CreateJobAsync(Job job, CancellationToken ct);
    Task<Job> UpdateJobAsync(Job job, CancellationToken ct);
    Task<List<TenantRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct);
    Task<TenantRoleAssignment?> GetRoleAssignmentAsync(string roleKey, CancellationToken ct);
    Task<TenantRoleAssignment> UpdateRoleAssignmentAsync(TenantRoleAssignment row, CancellationToken ct);
    Task<List<RetentionRule>> ListRetentionRulesAsync(CancellationToken ct);
    Task<RetentionRule> CreateRetentionRuleAsync(RetentionRule rule, CancellationToken ct);
    Task<RetentionRule?> GetRetentionRuleAsync(Guid id, CancellationToken ct);
    Task<RetentionRule> UpdateRetentionRuleAsync(RetentionRule rule, CancellationToken ct);
    Task<TenantRoleAssignment> CreateRoleAssignmentAsync(TenantRoleAssignment row, CancellationToken ct);
    Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct);
}

public interface IRecruitmentRepository
{
    Task<(List<Vacancy> Items, int Total)> ListVacanciesAsync(string? status, CancellationToken ct);
    Task<Vacancy> CreateVacancyAsync(Vacancy vacancy, CancellationToken ct);
    Task<Vacancy?> GetVacancyAsync(Guid id, CancellationToken ct);
    Task<Vacancy> UpdateVacancyAsync(Vacancy vacancy, CancellationToken ct);
    Task<(List<Candidate> Items, int Total)> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct);
    Task<Candidate> CreateCandidateAsync(Candidate candidate, CancellationToken ct);
    Task<Candidate?> GetCandidateAsync(Guid id, CancellationToken ct);
    Task<List<CandidateStageEvent>> ListStageEventsAsync(Guid candidateId, CancellationToken ct);
    Task<(List<Requisition> Items, int Total)> ListRequisitionsAsync(string? status, CancellationToken ct);
    Task<Requisition> CreateRequisitionAsync(Requisition requisition, CancellationToken ct);
    Task<Requisition?> GetRequisitionAsync(Guid id, CancellationToken ct);
    Task<Requisition> UpdateRequisitionAsync(Requisition requisition, CancellationToken ct);
    Task<List<RequisitionEvent>> ListRequisitionEventsAsync(Guid requisitionId, CancellationToken ct);
    Task<RequisitionEvent> CreateRequisitionEventAsync(RequisitionEvent entry, CancellationToken ct);
    Task<int> CountRequisitionVacanciesAsync(Guid requisitionId, CancellationToken ct);
    Task<string> NextRequisitionNoAsync(CancellationToken ct);
    Task<List<Vacancy>> ListVacanciesForRequisitionAsync(Guid requisitionId, CancellationToken ct);
    Task<CandidateStageEvent> CreateStageEventAsync(CandidateStageEvent entry, CancellationToken ct);
    Task<List<CandidateInterview>> ListInterviewsAsync(Guid candidateId, CancellationToken ct);
    Task<CandidateInterview> CreateInterviewAsync(CandidateInterview interview, CancellationToken ct);
    Task<CandidateInterview?> GetInterviewAsync(Guid id, CancellationToken ct);
    Task<CandidateInterview> UpdateInterviewAsync(CandidateInterview interview, CancellationToken ct);
    Task<Offer> CreateOfferAsync(Offer offer, CancellationToken ct);
    Task<Offer?> GetOfferAsync(Guid id, CancellationToken ct);
    Task<Offer> UpdateOfferAsync(Offer offer, CancellationToken ct);
    Task<(List<Offer> Items, int Total)> ListOffersAsync(string? status, CancellationToken ct);
    Task<PreboardingCase?> GetPreboardingAsync(Guid id, CancellationToken ct);
    Task<PreboardingCase?> GetPreboardingForCandidateAsync(Guid candidateId, CancellationToken ct);
    Task<(List<PreboardingCase> Items, int Total)> ListPreboardingAsync(string? status, CancellationToken ct);
    Task<PreboardingCase> CreatePreboardingAsync(PreboardingCase record, CancellationToken ct);
    Task<PreboardingCase> UpdatePreboardingAsync(PreboardingCase record, CancellationToken ct);
    Task<PreboardingTask?> GetPreboardingTaskAsync(Guid id, CancellationToken ct);
    Task<PreboardingTask> CreatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct);
    Task<PreboardingTask> UpdatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct);
    Task<List<CandidateDocument>> ListCandidateDocumentsAsync(Guid candidateId, CancellationToken ct);
    Task<CandidateDocument> CreateCandidateDocumentAsync(CandidateDocument document, CancellationToken ct);
    Task<CandidateDocument?> GetCandidateDocumentAsync(Guid id, CancellationToken ct);
    Task<int> CountCandidatesForVacancyAsync(Guid vacancyId, CancellationToken ct);
}

public interface IRelationsRepository
{
    Task<(List<RelationsCase> Items, int Total)> ListCasesAsync(string? category, CancellationToken ct);
    Task<RelationsCase> CreateCaseAsync(RelationsCase caseRecord, CancellationToken ct);
    Task<RelationsCase?> GetCaseAsync(Guid id, CancellationToken ct);
    Task<RelationsCase> UpdateCaseAsync(RelationsCase caseRecord, CancellationToken ct);
    Task<int> CountCasesThisYearAsync(CancellationToken ct);
    Task<RelationsCaseAccess?> GetAccessAsync(Guid caseId, string actorSubjectId, CancellationToken ct);
    Task<RelationsCaseAccess> CreateAccessAsync(RelationsCaseAccess access, CancellationToken ct);
    Task<List<RelationsCaseAccess>> ListAccessAsync(Guid caseId, CancellationToken ct);
    Task<RelationsCaseEvent> CreateEventAsync(RelationsCaseEvent entry, CancellationToken ct);
    Task<List<RelationsCaseEvent>> ListEventsAsync(Guid caseId, CancellationToken ct);
    Task<RelationsCaseAction> CreateActionAsync(RelationsCaseAction action, CancellationToken ct);
    Task<RelationsCaseAction?> GetActionAsync(Guid id, CancellationToken ct);
    Task<RelationsCaseAction> UpdateActionAsync(RelationsCaseAction action, CancellationToken ct);
    Task<List<RelationsCaseAction>> ListActionsAsync(Guid caseId, CancellationToken ct);
    Task<RelationsEvidence> CreateEvidenceAsync(RelationsEvidence evidence, CancellationToken ct);
    Task<RelationsEvidence?> GetEvidenceAsync(Guid id, CancellationToken ct);
    Task<List<RelationsEvidence>> ListEvidenceAsync(Guid caseId, CancellationToken ct);
    Task<(List<ProtectedDisclosure> Items, int Total)> ListProtectedDisclosuresAsync(string? status, CancellationToken ct);
    Task<ProtectedDisclosure?> GetProtectedDisclosureAsync(Guid id, CancellationToken ct);
    Task<ProtectedDisclosure> UpdateProtectedDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct);
    Task<ProtectedDisclosureEvent> CreateProtectedDisclosureEventAsync(ProtectedDisclosureEvent entry, CancellationToken ct);
    Task<List<ProtectedDisclosureEvent>> ListProtectedDisclosureEventsAsync(Guid disclosureId, CancellationToken ct);
}

public interface IDocumentsRepository
{
    Task<(List<WorkerDocument> Items, int Total)> ListDocumentsAsync(Guid workerId, CancellationToken ct);
    Task<WorkerDocument> CreateDocumentAsync(WorkerDocument document, CancellationToken ct);
    Task<WorkerDocument?> GetDocumentAsync(Guid id, CancellationToken ct);
    Task<List<WorkerDocument>> ListAllDocumentsAsync(CancellationToken ct);
}

// Domain entities for recruitment/relations (kept small for M7)

public sealed class Vacancy : Entity
{
    public Guid OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string JobTitle { get; set; } = null!;
    public string? Grade { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "draft"; // draft | published | closed | cancelled
    public Guid? RequisitionId { get; set; }      // M38: requisition the posting was created from
    public Requisition? Requisition { get; set; }
    public Guid? LocationId { get; set; }         // M38: work location of the posting
    public DateOnly? ClosingDate { get; set; }    // M38: last day applications are accepted
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}

public sealed class Candidate : Entity
{
    public Guid VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public string Stage { get; set; } = "applied"; // applied | screening | shortlisted | interviewing | interviewed | offered | preboarding | hired | rejected
    public string? StageScore { get; set; }          // interview scorecard score
    public DateTimeOffset? StageChangedAt { get; set; }
    public Guid? WorkerId { get; set; }
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

public sealed class Offer : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public decimal BaseSalary { get; set; }
    public string ContractType { get; set; } = "permanent";
    public int ProbationMonths { get; set; } = 3;
    public int NoticeDays { get; set; } = 30;
    public string? StartDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "draft"; // draft | approved | issued | accepted | declined
    public string? ExpiresOn { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}

public sealed class CandidateStageEvent : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public string FromStage { get; set; } = null!;
    public string ToStage { get; set; } = null!;
    public string? Score { get; set; }
    public string? Notes { get; set; }
}

public sealed class CandidateInterview : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string InterviewType { get; set; } = "panel";
    public string? InterviewerName { get; set; }
    public string Status { get; set; } = "scheduled";
    public int? OverallScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Notes { get; set; }
}

public sealed class CandidateDocument : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public string Category { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
}

public sealed class PreboardingCase : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid AssignmentId { get; set; }
    public string Status { get; set; } = "preboarding";
    public DateOnly StartDate { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public ICollection<PreboardingTask> Tasks { get; set; } = new List<PreboardingTask>();
}

public sealed class PreboardingTask : Entity
{
    public Guid PreboardingCaseId { get; set; }
    public PreboardingCase? PreboardingCase { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool Required { get; set; } = true;
    public string Status { get; set; } = "pending";
    public DateOnly? DueDate { get; set; }
    public string? Owner { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class RelationsCase : Entity
{
    public string? Reference { get; set; }
    public Guid? SubjectWorkerId { get; set; }
    public Worker? SubjectWorker { get; set; }
    public string CaseType { get; set; } = null!;      // disciplinary | grievance | misconduct | investigation
    public string Category { get; set; } = null!;
    public string Severity { get; set; } = "medium";
    public string Summary { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "open";        // open | in-progress | resolved | closed
    public string Classification { get; set; } = "restricted";
    public string Confidentiality { get; set; } = "restricted";
    public string? OwnerSubjectId { get; set; }
    public string? RaisedBy { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Findings { get; set; }
    public string? Outcome { get; set; }               // filled on resolved/closed
    public DateTimeOffset? ClosedAt { get; set; }
    public ICollection<RelationsCaseAction> Actions { get; set; } = new List<RelationsCaseAction>();
    public ICollection<RelationsEvidence> Evidence { get; set; } = new List<RelationsEvidence>();
}

public sealed class RelationsCaseAccess : Entity
{
    public Guid CaseId { get; set; }
    public RelationsCase? Case { get; set; }
    public string ActorSubjectId { get; set; } = null!;
    public string Decision { get; set; } = null!; // no-conflict | conflict
    public string? Notes { get; set; }
}

public sealed class RelationsCaseEvent : Entity
{
    public Guid CaseId { get; set; }
    public RelationsCase? Case { get; set; }
    public string Action { get; set; } = null!;
    public string ActorSubjectId { get; set; } = null!;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Notes { get; set; }
}

public sealed class RelationsCaseAction : Entity
{
    public Guid CaseId { get; set; }
    public RelationsCase? Case { get; set; }
    public string ActionType { get; set; } = "investigation";
    public string Title { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public string? OwnerSubjectId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class RelationsEvidence : Entity
{
    public Guid CaseId { get; set; }
    public RelationsCase? Case { get; set; }
    public string Title { get; set; } = null!;
    public string EvidenceType { get; set; } = "document";
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
    public string Classification { get; set; } = "restricted";
    public string AddedBySubjectId { get; set; } = null!;
}

public sealed class ProtectedDisclosureEvent : Entity
{
    public Guid DisclosureId { get; set; }
    public ProtectedDisclosure? Disclosure { get; set; }
    public string Action { get; set; } = null!;
    public string ActorSubjectId { get; set; } = null!;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Notes { get; set; }
}
