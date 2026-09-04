using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>All tables live in the existing `hrm` Postgres schema (same database
/// as the rest of the ERP). A global query filter scopes every query to the
/// current tenant from the OIDC token; audit entries record before/after JSON
/// via the interceptor.</summary>
public sealed class HrmDbContext(DbContextOptions<HrmDbContext> options, ITenantAccessor tenant) : DbContext(options)
{
    /// <summary>Auto-populates <see cref="Entity.TenantId"/> on all newly added
    /// entities so every row is tenant-scoped from day one (both sync and async
    /// save paths).</summary>
    public override int SaveChanges()
    {
        FillTenantIds();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        FillTenantIds();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void FillTenantIds()
    {
        var tenantId = tenant.GetTenantId();
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                // Never trust a tenant id supplied by a client or service. The
                // authenticated request context is the only write authority.
                entry.Entity.TenantId = tenantId;
                if (entry.Entity.CreatedAt == DateTimeOffset.MinValue)
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
            }
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (!string.Equals(entry.Entity.TenantId, tenantId, StringComparison.Ordinal))
                    throw new DomainException("cross-tenant-write", "A record from another tenant cannot be changed.");
                if (entry.Entity is AuditEntry or PrivilegedActionEvent or Mightyfin.Erp.Hrm.Domain.Entities.ComplianceEvidence or GoLiveSignoff)
                    throw new DomainException("audit-immutable", "Compliance and audit evidence is append-only.");
            }
        }
    }

    // Organization
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<WorkLocation> WorkLocations => Set<WorkLocation>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<WorkCalendar> WorkCalendars => Set<WorkCalendar>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<TenantRoleAssignment> TenantRoleAssignments => Set<TenantRoleAssignment>();
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();
    public DbSet<LocalCredentialLink> LocalCredentialLinks => Set<LocalCredentialLink>();
    public DbSet<LocalSession> LocalSessions => Set<LocalSession>();
    public DbSet<HrUserBranchAssignment> UserBranchAssignments => Set<HrUserBranchAssignment>();
    public DbSet<RetentionRule> RetentionRules => Set<RetentionRule>();

    // People & lifecycle
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<WorkerBankDetail> WorkerBankDetails => Set<WorkerBankDetail>();
    public DbSet<WorkerEducation> WorkerEducations => Set<WorkerEducation>();
    public DbSet<ExternalWorkHistory> ExternalWorkHistory => Set<ExternalWorkHistory>();
    public DbSet<InternalWorkHistory> InternalWorkHistory => Set<InternalWorkHistory>();
    public DbSet<WorkerDocument> WorkerDocuments => Set<WorkerDocument>();
    public DbSet<MasterDataBatch> MasterDataBatches => Set<MasterDataBatch>();

    // Policies & time
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalanceLedger> LeaveBalanceLedgers => Set<LeaveBalanceLedger>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();
    public DbSet<ShiftDefinition> ShiftDefinitions => Set<ShiftDefinition>();
    public DbSet<WorkerShiftAssignment> WorkerShiftAssignments => Set<WorkerShiftAssignment>();
    public DbSet<AttendanceImportBatch> AttendanceImportBatches => Set<AttendanceImportBatch>();
    public DbSet<LeaveAccrualRun> LeaveAccrualRuns => Set<LeaveAccrualRun>();
    public DbSet<LeaveBalanceAdjustment> LeaveBalanceAdjustments => Set<LeaveBalanceAdjustment>();
    public DbSet<LeaveEncashmentRequest> LeaveEncashmentRequests => Set<LeaveEncashmentRequest>();

    // Workflows & experience
    public DbSet<WorkflowRequest> WorkflowRequests => Set<WorkflowRequest>();
    public DbSet<WorkflowDecision> WorkflowDecisions => Set<WorkflowDecision>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<HrRequest> HrRequests => Set<HrRequest>();
    public DbSet<HrRequestMessage> HrRequestMessages => Set<HrRequestMessage>();
    public DbSet<HrLetter> HrLetters => Set<HrLetter>();
    public DbSet<ProtectedDisclosure> ProtectedDisclosures => Set<ProtectedDisclosure>();

    // Payroll
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryStructureItem> SalaryStructureItems => Set<SalaryStructureItem>();
    public DbSet<WorkerPayrollProfile> WorkerPayrollProfiles => Set<WorkerPayrollProfile>();
    public DbSet<WorkerComponentValue> WorkerComponentValues => Set<WorkerComponentValue>();
    // Flexible benefit claims (M41 Gap 6b)
    public DbSet<BenefitType> BenefitTypes => Set<BenefitType>();
    public DbSet<WorkerBenefitAllowance> WorkerBenefitAllowances => Set<WorkerBenefitAllowance>();
    public DbSet<BenefitClaim> BenefitClaims => Set<BenefitClaim>();
    public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();
    public DbSet<PayGroup> PayGroups => Set<PayGroup>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<TaxSlab> TaxSlabs => Set<TaxSlab>();
    public DbSet<ContributionRule> ContributionRules => Set<ContributionRule>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunLine> PayrollRunLines => Set<PayrollRunLine>();
    public DbSet<PayrollRunEvent> PayrollRunEvents => Set<PayrollRunEvent>();
    public DbSet<PayrollLineComponent> PayrollLineComponents => Set<PayrollLineComponent>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipAccessLog> PayslipAccessLogs => Set<PayslipAccessLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IntegrationOperation> IntegrationOperations => Set<IntegrationOperation>();
    public DbSet<PrivilegedActionEvent> PrivilegedActionEvents => Set<PrivilegedActionEvent>();
    public DbSet<ComplianceEvidence> ComplianceEvidenceRecords => Set<ComplianceEvidence>();
    public DbSet<GoLiveSignoff> GoLiveSignoffs => Set<GoLiveSignoff>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<CompanyBranding> CompanyBrandings => Set<CompanyBranding>();

    // Config & extras
    public DbSet<CapabilityConfig> CapabilityConfigs => Set<CapabilityConfig>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<RequisitionEvent> RequisitionEvents => Set<RequisitionEvent>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<CandidateStageEvent> CandidateStageEvents => Set<CandidateStageEvent>();
    public DbSet<CandidateInterview> CandidateInterviews => Set<CandidateInterview>();
    public DbSet<CandidateDocument> CandidateDocuments => Set<CandidateDocument>();
    public DbSet<PreboardingCase> PreboardingCases => Set<PreboardingCase>();
    public DbSet<PreboardingTask> PreboardingTasks => Set<PreboardingTask>();
    public DbSet<RelationsCase> RelationsCases => Set<RelationsCase>();
    public DbSet<RelationsCaseAccess> RelationsCaseAccessDeclarations => Set<RelationsCaseAccess>();
    public DbSet<RelationsCaseEvent> RelationsCaseEvents => Set<RelationsCaseEvent>();
    public DbSet<RelationsCaseAction> RelationsCaseActions => Set<RelationsCaseAction>();
    public DbSet<RelationsEvidence> RelationsEvidence => Set<RelationsEvidence>();
    public DbSet<ProtectedDisclosureEvent> ProtectedDisclosureEvents => Set<ProtectedDisclosureEvent>();

    // M49: first-time setup state and per-step completion records
    public DbSet<SetupState> SetupStates => Set<SetupState>();
    public DbSet<SetupStepRecord> SetupStepRecords => Set<SetupStepRecord>();

    // Performance & goals (M36)
    public DbSet<PerformanceCycle> PerformanceCycles => Set<PerformanceCycle>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<PerformanceAssessment> PerformanceAssessments => Set<PerformanceAssessment>();

    // Offboarding & exit (M37)
    public DbSet<OffboardingRequest> OffboardingRequests => Set<OffboardingRequest>();
    public DbSet<OffboardingChecklistItem> OffboardingChecklistItems => Set<OffboardingChecklistItem>();
    public DbSet<ExitInterview> ExitInterviews => Set<ExitInterview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("hrm");

        // Tenant scoping on every entity
        foreach (var et in modelBuilder.Model.GetEntityTypes())
        {
            var method = typeof(HrmDbContext).GetMethod(nameof(MakeTenantFilter), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.MakeGenericMethod(et.ClrType);
            var filter = (System.Linq.Expressions.LambdaExpression)method.Invoke(null, [this])!;
            modelBuilder.Entity(et.ClrType).HasQueryFilter(filter);
        }

        ConfigureEntity<LegalEntity>(modelBuilder, "legal_entities", e => e.HasKey(x => x.Id));
        ConfigureEntity<WorkLocation>(modelBuilder, "work_locations");
        ConfigureEntity<OrgUnit>(modelBuilder, "org_units");
        ConfigureEntity<WorkCalendar>(modelBuilder, "work_calendars");
        ConfigureEntity<PublicHoliday>(modelBuilder, "public_holidays");
        ConfigureEntity<Job>(modelBuilder, "jobs", e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        ConfigureEntity<TenantRoleAssignment>(modelBuilder, "tenant_role_assignments", e => e.HasIndex(x => new { x.TenantId, x.RoleKey }).IsUnique());
        ConfigureEntity<LocalUser>(modelBuilder, "local_users", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.WorkerId }).HasFilter("worker_id IS NOT NULL");
        });
        ConfigureEntity<LocalSession>(modelBuilder, "local_sessions", e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.LocalUserId, x.ExpiresAt });
        });
        ConfigureEntity<LocalCredentialLink>(modelBuilder, "local_credential_links", e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.LocalUserId, x.ExpiresAt });
        });
        ConfigureEntity<HrUserBranchAssignment>(modelBuilder, "hr_user_branch_assignments",
            e => e.HasIndex(x => new { x.TenantId, x.UserId, x.LocationId }).IsUnique());
        ConfigureEntity<RetentionRule>(modelBuilder, "retention_rules", e => e.HasIndex(x => new { x.TenantId, x.RecordType }).IsUnique());
        ConfigureEntity<Worker>(modelBuilder, "workers", e =>
        {
            e.HasIndex(x => x.EmployeeNo).IsUnique();
            // One shared IdP identity may have records in several products,
            // but it may reference at most one worker inside an HRM tenant.
            e.HasIndex(x => new { x.TenantId, x.SubjectId })
                .IsUnique()
                .HasFilter("subject_id IS NOT NULL");
        });
        modelBuilder.Entity<Worker>().Ignore(x => x.FullName);
        ConfigureEntity<Assignment>(modelBuilder, "assignments");
        ConfigureEntity<Movement>(modelBuilder, "movements");
        ConfigureEntity<EmergencyContact>(modelBuilder, "emergency_contacts");
        ConfigureEntity<WorkerBankDetail>(modelBuilder, "worker_bank_details");
        ConfigureEntity<WorkerEducation>(modelBuilder, "education");
        ConfigureEntity<ExternalWorkHistory>(modelBuilder, "external_work_history");
        ConfigureEntity<InternalWorkHistory>(modelBuilder, "internal_work_history");
        ConfigureEntity<WorkerDocument>(modelBuilder, "worker_documents");
        ConfigureEntity<MasterDataBatch>(modelBuilder, "master_data_batches", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.SummaryJson).HasColumnType("jsonb");
            e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            e.Property(x => x.ErrorsJson).HasColumnType("jsonb");
        });
        ConfigureEntity<LeaveType>(modelBuilder, "leave_types", e => e.HasIndex(x => new { x.TenantId, x.Code }));
        ConfigureEntity<LeaveBalanceLedger>(modelBuilder, "leave_balance_ledger");
        ConfigureEntity<LeaveRequest>(modelBuilder, "leave_requests");
        ConfigureEntity<AttendanceRecord>(modelBuilder, "attendance_records", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.WorkerId, x.WorkDate });
            e.HasIndex(x => new { x.TenantId, x.OvertimeStatus, x.WorkDate });
        });
        ConfigureEntity<AttendanceCorrection>(modelBuilder, "attendance_corrections");
        ConfigureEntity<ShiftDefinition>(modelBuilder, "shift_definitions", e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        ConfigureEntity<WorkerShiftAssignment>(modelBuilder, "worker_shift_assignments");
        ConfigureEntity<AttendanceImportBatch>(modelBuilder, "attendance_import_batches");
        ConfigureEntity<LeaveAccrualRun>(modelBuilder, "leave_accrual_runs", e => e.HasIndex(x => new { x.TenantId, x.Period }).IsUnique());
        ConfigureEntity<LeaveBalanceAdjustment>(modelBuilder, "leave_balance_adjustments");
        ConfigureEntity<LeaveEncashmentRequest>(modelBuilder, "leave_encashments");
        ConfigureEntity<WorkflowRequest>(modelBuilder, "workflow_requests");
        ConfigureEntity<WorkflowDecision>(modelBuilder, "workflow_decisions");
        ConfigureEntity<ApprovalDelegation>(modelBuilder, "approval_delegations");
        ConfigureEntity<HrRequest>(modelBuilder, "hr_requests");
        ConfigureEntity<HrRequestMessage>(modelBuilder, "hr_request_messages");
        ConfigureEntity<HrLetter>(modelBuilder, "hr_letters");
        ConfigureEntity<ProtectedDisclosure>(modelBuilder, "protected_disclosures", e => e.HasIndex(x => x.CaseReference).IsUnique());
        ConfigureEntity<SalaryComponent>(modelBuilder, "salary_components", e => e.HasIndex(x => new { x.TenantId, x.Code }));
        ConfigureEntity<SalaryStructure>(modelBuilder, "salary_structures");
        ConfigureEntity<SalaryStructureItem>(modelBuilder, "salary_structure_items");
        ConfigureEntity<WorkerPayrollProfile>(modelBuilder, "worker_payroll_profiles");
        ConfigureEntity<WorkerComponentValue>(modelBuilder, "worker_component_values");
        ConfigureEntity<BenefitType>(modelBuilder, "benefit_types", e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        ConfigureEntity<WorkerBenefitAllowance>(modelBuilder, "benefit_allowances",
            e => e.HasIndex(x => new { x.TenantId, x.WorkerId, x.BenefitTypeId, x.Year }).IsUnique());
        ConfigureEntity<BenefitClaim>(modelBuilder, "benefit_claims",
            e => e.HasIndex(x => new { x.TenantId, x.WorkerId, x.Status }));
        ConfigureEntity<SalaryAdvance>(modelBuilder, "salary_advances", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.WorkerId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.DeductFromPayslip, x.DeductionStartDate });
        });
        ConfigureEntity<PayGroup>(modelBuilder, "pay_groups");
        ConfigureEntity<PayPeriod>(modelBuilder, "pay_periods", e => e.HasIndex(x => new { x.TenantId, x.PayGroupId, x.PeriodLabel }).IsUnique());
        ConfigureEntity<TaxSlab>(modelBuilder, "tax_slabs");
        ConfigureEntity<ContributionRule>(modelBuilder, "contribution_rules");
        ConfigureEntity<PayrollRun>(modelBuilder, "payroll_runs");
        ConfigureEntity<PayrollRunLine>(modelBuilder, "payroll_run_lines");
        ConfigureEntity<PayrollRunEvent>(modelBuilder, "payroll_run_events", e => e.HasIndex(x => new { x.TenantId, x.RunId, x.CreatedAt }));
        ConfigureEntity<PayrollLineComponent>(modelBuilder, "payroll_line_components");
        ConfigureEntity<Payslip>(modelBuilder, "payslips", e => e.HasIndex(x => x.PayslipNo).IsUnique());
        ConfigureEntity<PayslipAccessLog>(modelBuilder, "payslip_access_logs");
        ConfigureEntity<OutboxMessage>(modelBuilder, "outbox_messages", e =>
        {
            e.HasIndex(x => x.PublicId).IsUnique();
            e.HasIndex(x => new { x.Status, x.AvailableAt, x.CreatedAt });
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.LastError).HasMaxLength(2000);
        });
        ConfigureEntity<IntegrationOperation>(modelBuilder, "integration_operations", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.IntegrationKey, x.Status, x.CreatedAt });
            // Payloads can be JSON or provider CSV files; ContentType controls
            // interpretation, so the immutable source bytes are stored as text.
            e.Property(x => x.PayloadJson).HasColumnType("text");
            e.Property(x => x.LastError).HasMaxLength(2000);
        });
        ConfigureEntity<PrivilegedActionEvent>(modelBuilder, "privileged_action_events", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.ActorSubjectId, x.CreatedAt });
        });
        ConfigureEntity<ComplianceEvidence>(modelBuilder, "compliance_evidence", e =>
            e.HasIndex(x => new { x.TenantId, x.ControlKey, x.ExecutedAt }));
        ConfigureEntity<GoLiveSignoff>(modelBuilder, "go_live_signoffs", e =>
            e.HasIndex(x => new { x.TenantId, x.RoleKey, x.SignedAt }));
        ConfigureEntity<LegalHold>(modelBuilder, "legal_holds", e =>
            e.HasIndex(x => new { x.TenantId, x.Reference }).IsUnique().HasFilter("status = 'active'"));
        ConfigureEntity<CompanyBranding>(modelBuilder, "company_brandings", e =>
            e.HasIndex(x => x.TenantId).IsUnique());
        ConfigureEntity<CapabilityConfig>(modelBuilder, "capability_configs");
        ConfigureEntity<AuditEntry>(modelBuilder, "audit_entries", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        });
        ConfigureEntity<Vacancy>(modelBuilder, "vacancies", e => e.HasIndex(x => new { x.TenantId, x.RequisitionId }));
        ConfigureEntity<Requisition>(modelBuilder, "requisitions", e =>
        {
            e.HasIndex(x => new { x.TenantId, x.RequisitionNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
        });
        ConfigureEntity<RequisitionEvent>(modelBuilder, "requisition_events");
        ConfigureEntity<Candidate>(modelBuilder, "candidates");
        ConfigureEntity<Offer>(modelBuilder, "offers");
        ConfigureEntity<CandidateStageEvent>(modelBuilder, "candidate_stage_events");
        ConfigureEntity<CandidateInterview>(modelBuilder, "candidate_interviews");
        ConfigureEntity<CandidateDocument>(modelBuilder, "candidate_documents");
        ConfigureEntity<PreboardingCase>(modelBuilder, "preboarding_cases", e => e.HasIndex(x => new { x.TenantId, x.CandidateId }).IsUnique());
        ConfigureEntity<PreboardingTask>(modelBuilder, "preboarding_tasks");
        ConfigureEntity<RelationsCase>(modelBuilder, "relations_cases", e => e.HasIndex(x => new { x.TenantId, x.Reference }).IsUnique());
        ConfigureEntity<RelationsCaseAccess>(modelBuilder, "relations_case_access", e => e.HasIndex(x => new { x.TenantId, x.CaseId, x.ActorSubjectId }).IsUnique());
        ConfigureEntity<RelationsCaseEvent>(modelBuilder, "relations_case_events");
        ConfigureEntity<RelationsCaseAction>(modelBuilder, "relations_case_actions");
        ConfigureEntity<RelationsEvidence>(modelBuilder, "relations_evidence");
        ConfigureEntity<ProtectedDisclosureEvent>(modelBuilder, "protected_disclosure_events");
        ConfigureEntity<SetupState>(modelBuilder, "setup_states");
        ConfigureEntity<SetupStepRecord>(modelBuilder, "setup_step_records",
            e => e.HasIndex(x => new { x.TenantId, x.StepKey }).IsUnique());
        // Performance & goals (M36)
        ConfigureEntity<PerformanceCycle>(modelBuilder, "performance_cycles");
        ConfigureEntity<PerformanceGoal>(modelBuilder, "performance_goals");
        ConfigureEntity<PerformanceAssessment>(modelBuilder, "performance_assessments");

        // Offboarding & exit (M37)
        ConfigureEntity<OffboardingRequest>(modelBuilder, "offboarding_requests");
        ConfigureEntity<OffboardingChecklistItem>(modelBuilder, "offboarding_checklist_items");
        ConfigureEntity<ExitInterview>(modelBuilder, "exit_interviews");

        // Relationships
        modelBuilder.Entity<Requisition>().HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RequisitionEvent>().HasOne(x => x.Requisition).WithMany(x => x.Events).HasForeignKey(x => x.RequisitionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Vacancy>().HasOne(x => x.Requisition).WithMany().HasForeignKey(x => x.RequisitionId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<WorkLocation>().HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkLocation>().HasOne(x => x.DefaultCalendar).WithMany().HasForeignKey(x => x.DefaultCalendarId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<OrgUnit>().HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrgUnit>().HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Worker>().HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Worker>().HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Worker>().HasMany(x => x.EmergencyContacts).WithOne(x => x.Worker).HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Worker>().HasMany(x => x.BankDetails).WithOne(x => x.Worker).HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Worker>().HasMany(x => x.Education).WithOne(x => x.Worker).HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Worker>().HasMany(x => x.ExternalWorkHistory).WithOne(x => x.Worker).HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Worker>().HasMany(x => x.InternalWorkHistory).WithOne(x => x.Worker).HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Assignment>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<Assignment>().HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId);
        modelBuilder.Entity<Assignment>().HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId);
        modelBuilder.Entity<Assignment>().HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        modelBuilder.Entity<Movement>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<WorkCalendar>().HasMany(x => x.Holidays).WithOne(x => x.Calendar).HasForeignKey(x => x.CalendarId);
        modelBuilder.Entity<LeaveRequest>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<LeaveBalanceLedger>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<AttendanceRecord>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<AttendanceCorrection>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<WorkerShiftAssignment>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<WorkerShiftAssignment>().HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId);
        modelBuilder.Entity<WorkerShiftAssignment>().HasOne(x => x.Calendar).WithMany().HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<LeaveBalanceAdjustment>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<LeaveEncashmentRequest>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<WorkflowRequest>().HasMany(x => x.Decisions).WithOne(x => x.Request).HasForeignKey(x => x.RequestId);
        modelBuilder.Entity<HrRequest>().HasMany(x => x.Messages).WithOne(x => x.Request).HasForeignKey(x => x.RequestId);
        modelBuilder.Entity<HrLetter>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<SalaryStructureItem>().HasOne(x => x.Structure).WithMany(x => x.Items).HasForeignKey(x => x.StructureId);
        modelBuilder.Entity<WorkerPayrollProfile>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<WorkerPayrollProfile>().HasOne(x => x.Structure).WithMany().HasForeignKey(x => x.StructureId);
        modelBuilder.Entity<WorkerPayrollProfile>().HasOne(x => x.PayGroup).WithMany().HasForeignKey(x => x.PayGroupId);
        modelBuilder.Entity<WorkerComponentValue>().HasOne(x => x.Profile).WithMany(x => x.ComponentValues).HasForeignKey(x => x.ProfileId);
        modelBuilder.Entity<WorkerComponentValue>().HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId);
        modelBuilder.Entity<PayPeriod>().HasOne(x => x.PayGroup).WithMany().HasForeignKey(x => x.PayGroupId);
        modelBuilder.Entity<PayrollRun>().HasOne(x => x.PayPeriod).WithMany().HasForeignKey(x => x.PayPeriodId);
        modelBuilder.Entity<PayrollRunLine>().HasOne(x => x.Run).WithMany().HasForeignKey(x => x.RunId);
        modelBuilder.Entity<PayrollRunLine>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<PayrollRunLine>().HasMany(x => x.Components).WithOne(x => x.RunLine).HasForeignKey(x => x.RunLineId);
        modelBuilder.Entity<PayrollRunEvent>().HasOne(x => x.Run).WithMany().HasForeignKey(x => x.RunId);
        modelBuilder.Entity<Payslip>().HasOne(x => x.RunLine).WithMany().HasForeignKey(x => x.RunLineId);
        modelBuilder.Entity<Payslip>().HasMany(x => x.AccessLogs).WithOne().HasForeignKey(x => x.PayslipId);
        modelBuilder.Entity<Vacancy>().HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId);
        modelBuilder.Entity<Vacancy>().HasMany(x => x.Candidates).WithOne(x => x.Vacancy).HasForeignKey(x => x.VacancyId);
        modelBuilder.Entity<Candidate>().HasMany(x => x.Offers).WithOne(x => x.Candidate).HasForeignKey(x => x.CandidateId);
        modelBuilder.Entity<Candidate>().HasMany<CandidateStageEvent>().WithOne(x => x.Candidate).HasForeignKey(x => x.CandidateId);
        modelBuilder.Entity<Candidate>().HasMany<CandidateInterview>().WithOne(x => x.Candidate).HasForeignKey(x => x.CandidateId);
        modelBuilder.Entity<Candidate>().HasMany<CandidateDocument>().WithOne(x => x.Candidate).HasForeignKey(x => x.CandidateId);
        modelBuilder.Entity<PreboardingCase>().HasOne(x => x.Candidate).WithOne().HasForeignKey<PreboardingCase>(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PreboardingCase>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PreboardingCase>().HasMany(x => x.Tasks).WithOne(x => x.PreboardingCase).HasForeignKey(x => x.PreboardingCaseId);
        modelBuilder.Entity<RelationsCase>().HasOne(x => x.SubjectWorker).WithMany().HasForeignKey(x => x.SubjectWorkerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<RelationsCase>().HasMany(x => x.Actions).WithOne(x => x.Case).HasForeignKey(x => x.CaseId);
        modelBuilder.Entity<RelationsCase>().HasMany(x => x.Evidence).WithOne(x => x.Case).HasForeignKey(x => x.CaseId);
        modelBuilder.Entity<RelationsCaseAccess>().HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId);
        modelBuilder.Entity<RelationsCaseEvent>().HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId);
        modelBuilder.Entity<ProtectedDisclosureEvent>().HasOne(x => x.Disclosure).WithMany().HasForeignKey(x => x.DisclosureId);

        // Performance & goals relationships (M36)
        modelBuilder.Entity<PerformanceGoal>().HasOne(x => x.Cycle).WithMany(x => x.Goals).HasForeignKey(x => x.CycleId);
        modelBuilder.Entity<PerformanceGoal>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);
        modelBuilder.Entity<PerformanceAssessment>().HasOne(x => x.Cycle).WithMany(x => x.Assessments).HasForeignKey(x => x.CycleId);
        modelBuilder.Entity<PerformanceAssessment>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId);

        // Offboarding & exit relationships (M37)
        modelBuilder.Entity<OffboardingChecklistItem>().HasOne(x => x.Request).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.OffboardingRequestId);
        modelBuilder.Entity<OffboardingRequest>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExitInterview>().HasOne(x => x.Request).WithOne().HasForeignKey<ExitInterview>(x => x.OffboardingRequestId);
        modelBuilder.Entity<ExitInterview>().HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureEntity<T>(ModelBuilder modelBuilder, string tableName, Action<EntityTypeBuilder<T>>? extra = null) where T : class
    {
        var b = modelBuilder.Entity<T>();
        b.ToTable(tableName, "hrm");
        b.Property<string>("TenantId").HasColumnName("tenant_id");
        b.Property<DateTimeOffset>("CreatedAt").HasColumnName("created_at");
        b.Property<string>("CreatedBy").HasColumnName("created_by");
        b.Property<DateTimeOffset?>("UpdatedAt").HasColumnName("updated_at");
        b.Property<string?>("UpdatedBy").HasColumnName("updated_by");
        b.Property<bool>("IsArchived").HasColumnName("is_archived");
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.Name == nameof(Entity.TenantId) || prop.Name == nameof(Entity.CreatedAt)
                || prop.Name == nameof(Entity.CreatedBy) || prop.Name == nameof(Entity.UpdatedAt)
                || prop.Name == nameof(Entity.UpdatedBy) || prop.Name == nameof(Entity.IsArchived))
                continue;
            if (prop.PropertyType.IsGenericType && typeof(System.Collections.Generic.IEnumerable<>).IsAssignableFrom(prop.PropertyType.GetGenericTypeDefinition()))
                continue; // navigation collections
            var underlying = System.Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(decimal?)
                || underlying == typeof(int) || underlying == typeof(int?) || underlying == typeof(long) || underlying == typeof(long?)
                || underlying == typeof(bool) || underlying == typeof(bool?)
                || underlying == typeof(DateTime) || underlying == typeof(DateTime?)
                || underlying == typeof(DateTimeOffset) || underlying == typeof(DateTimeOffset?)
                || underlying == typeof(DateOnly) || underlying == typeof(DateOnly?)
                || underlying == typeof(TimeOnly) || underlying == typeof(TimeOnly?)
                || underlying == typeof(Guid) || underlying == typeof(Guid?)
                || underlying.IsEnum)
            {
                var pb = b.Property(prop.PropertyType, prop.Name);
                var snake = ToSnake(prop.Name);
                pb.Metadata.SetColumnName(snake);
            }
        }
        extra?.Invoke(b);
    }

    private static string ToSnake(string name) =>
        string.Concat(name.Select((ch, i) => i > 0 && char.IsUpper(ch) ? "_" + char.ToLower(ch) : char.ToLower(ch).ToString()));

    /// <summary>Gets the current tenant id for query filtering. Kept as an
    /// instance method so the tenant id is resolved at query evaluation time
    /// rather than baked into the model cache (which is shared across contexts).</summary>
    private string CurrentTenantId() => tenant.GetTenantId();

    private static System.Linq.Expressions.Expression<System.Func<T, bool>> MakeTenantFilter<T>(HrmDbContext self) where T : Entity
    {
        var param = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        var tenantIdProp = System.Linq.Expressions.Expression.Property(param, nameof(Entity.TenantId));
        var thisExpr = System.Linq.Expressions.Expression.Constant(self, typeof(HrmDbContext));
        var tenantValue = System.Linq.Expressions.Expression.Call(thisExpr, nameof(CurrentTenantId), null);
        var body = System.Linq.Expressions.Expression.Equal(tenantIdProp, tenantValue);
        return System.Linq.Expressions.Expression.Lambda<System.Func<T, bool>>(body, param);
    }
}

/// <summary>Current-tenant accessor populated per-request from the WorkerPrincipal.</summary>
public interface ITenantAccessor
{
    string GetTenantId();
}

/// <summary>Default implementation: tenant from the authenticated principal,
/// fallback to the configured default tenant when auth is disabled.</summary>
public sealed class PrincipalTenantAccessor(Microsoft.AspNetCore.Http.IHttpContextAccessor http, IConfiguration config) : ITenantAccessor
{
    public string GetTenantId()
    {
        var claims = http.HttpContext?.User?.Claims;
        if (claims is not null)
        {
            var t = claims.FirstOrDefault(c => c.Type == "tenant")?.Value;
            if (!string.IsNullOrEmpty(t)) return t;
        }
        return config["HRM:DefaultTenantId"] ?? "local-tenant";
    }
}

/// <summary>EF Core interceptor writing append-only audit entries for every
/// save: captures before/after JSON for modified and deleted entities.</summary>
public sealed class AuditInterceptor(IHttpContextAccessor http, ITenantAccessor tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? ctx)
    {
        if (ctx is null) return;
        var entries = ctx.ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity is not AuditEntry and not PrivilegedActionEvent
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var principal = http.HttpContext?.User;
        var actor = principal?.FindFirst("sub")?.Value
            ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "system";
        var correlation = http.HttpContext?.Request.Headers["X-Request-Id"].FirstOrDefault()
            ?? http.HttpContext?.TraceIdentifier;
        var tenantId = tenant.GetTenantId();
        foreach (var entry in entries)
        {
            var audit = new AuditEntry
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                Action = entry.State switch { EntityState.Added => "create", EntityState.Deleted => "delete", _ => "update" },
                BeforeJson = entry.State == EntityState.Added ? null : SerializeRedacted(entry.OriginalValues),
                AfterJson = entry.State == EntityState.Deleted ? null : SerializeRedacted(entry.CurrentValues),
                ActorSubjectId = actor,
                CorrelationId = correlation,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = actor,
            };
            ctx.Set<AuditEntry>().Add(audit);
        }
    }

    private static string SerializeRedacted(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        string[] sensitive = ["nrc", "tpin", "passport", "napsa", "nhima", "accountnumber",
            "mobilemoneynumber", "payloadjson", "token", "secret", "password", "beforejson", "afterjson"];
        var snapshot = values.Properties.ToDictionary(p => p.Name, p =>
        {
            var normalized = p.Name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
            return sensitive.Any(normalized.Contains) ? (object?)"[REDACTED]" : values[p];
        });
        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }
}

/// <summary>Role authorization implementation evaluated against the current principal.</summary>
public sealed class AuthzServiceImpl(Microsoft.AspNetCore.Http.IHttpContextAccessor http, IConfiguration config) : IAuthzService
{
    public string CurrentSubjectId => GetCurrent().SubjectId;

    public void RequireAnyRole(params string[] roles)
    {
        var principal = GetCurrent();
        if (principal.IsDeveloperFallback) return; // dev mode: open
        if (!principal.IsRole(roles))
            throw new DomainException("forbidden", $"Requires one of roles: {string.Join(", ", roles)}");
    }

    public bool IsRole(params string[] roles)
    {
        var principal = GetCurrent();
        if (principal.IsDeveloperFallback) return true; // dev mode: open
        return principal.IsRole(roles);
    }

    public bool CanAccessSensitive(string category) =>
        GetCurrent().IsDeveloperFallback || GetCurrent().CanPayroll || GetCurrent().CanHr;

    private WorkerPrincipal GetCurrent()
    {
        var claims = http.HttpContext?.User?.Claims ?? [];
        return WorkerPrincipal.FromClaims(claims);
    }
}

/// <summary>Correlation id provider for request tracing.</summary>
public sealed class IdProvider : IIdProvider
{
    public string NewCorrelationId() => $"hrm-{Guid.CreateVersion7():N}"[..20];
}
