using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Core.Approvals.Infrastructure;

/// <summary>Separate Core schema; no foreign keys into HRM or Procurement.</summary>
public sealed class ApprovalDbContext(DbContextOptions<ApprovalDbContext> options, IApprovalScope scope) : DbContext(options)
{
    public DbSet<ApprovalCase> Cases => Set<ApprovalCase>();
    public DbSet<ApprovalStage> Stages => Set<ApprovalStage>();
    public DbSet<ApprovalAssignment> Assignments => Set<ApprovalAssignment>();
    public DbSet<ApprovalDecision> Decisions => Set<ApprovalDecision>();
    public DbSet<ApprovalDelegation> Delegations => Set<ApprovalDelegation>();

    public void AddCase(ApprovalCase approvalCase)
    {
        ApprovalEngine.ValidateNewCase(approvalCase);
        Cases.Add(approvalCase);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("core_approval");
        Configure<ApprovalCase>(model, "cases");
        Configure<ApprovalStage>(model, "stages");
        Configure<ApprovalAssignment>(model, "assignments");
        Configure<ApprovalDecision>(model, "decisions");
        Configure<ApprovalDelegation>(model, "delegations");

        model.Entity<ApprovalCase>(b =>
        {
            b.Property(x => x.SourceModule).HasMaxLength(60);
            b.Property(x => x.SourceType).HasMaxLength(80);
            b.Property(x => x.MakerSubjectId).HasMaxLength(150);
            b.Property(x => x.State).HasMaxLength(30);
            b.Property(x => x.CompletionKey).HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceModule, x.SourceType, x.SourceId, x.SourceVersion }).IsUnique();
        });
        model.Entity<ApprovalStage>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Mode).HasMaxLength(20);
            b.Property(x => x.State).HasMaxLength(30);
            b.HasOne<ApprovalCase>().WithMany(x => x.Stages)
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CaseId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CaseId, x.Sequence }).IsUnique();
        });
        model.Entity<ApprovalAssignment>(b =>
        {
            b.Property(x => x.AssigneeSubjectId).HasMaxLength(150);
            b.Property(x => x.DelegatedFromSubjectId).HasMaxLength(150);
            b.Property(x => x.PermissionKey).HasMaxLength(150);
            b.Property(x => x.State).HasMaxLength(30);
            b.HasOne<ApprovalStage>().WithMany(x => x.Assignments)
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StageId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CaseId, x.AssigneeSubjectId, x.State });
        });
        model.Entity<ApprovalDecision>(b =>
        {
            b.Property(x => x.ActorSubjectId).HasMaxLength(150);
            b.Property(x => x.Action).HasMaxLength(20);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.IdempotencyKey).HasMaxLength(150);
            b.HasOne<ApprovalAssignment>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CaseId, x.IdempotencyKey }).IsUnique();
        });
        model.Entity<ApprovalDelegation>(b =>
        {
            b.Property(x => x.DelegatorSubjectId).HasMaxLength(150);
            b.Property(x => x.DelegateSubjectId).HasMaxLength(150);
            b.Property(x => x.PermissionKey).HasMaxLength(150);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DelegatorSubjectId, x.PermissionKey, x.EffectiveFrom });
        });
    }

    private void Configure<T>(ModelBuilder model, string table) where T : ApprovalRecord
    {
        model.Entity<T>(b =>
        {
            b.ToTable(table);
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id });
            b.Property(x => x.TenantId).HasMaxLength(150);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
    }

    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { PrepareChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { PrepareChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }

    private void PrepareChanges()
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.SubjectId))
            throw new UnauthorizedAccessException("An authenticated approval scope is required.");
        var changes = ChangeTracker.Entries<ApprovalRecord>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changes)
        {
            if (entry.State == EntityState.Deleted ||
                entry.State == EntityState.Modified && entry.Entity is ApprovalDecision)
                throw new InvalidOperationException("Approval history cannot be changed or deleted.");
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                throw new UnauthorizedAccessException("Approval record is outside the actor scope.");
        }

        // An intermediate vote may leave Case.State unchanged. Still update the aggregate
        // concurrency token so simultaneous votes cannot both commit against stale quorum.
        foreach (var decision in changes.Where(x => x.State == EntityState.Added)
                     .Select(x => x.Entity).OfType<ApprovalDecision>())
        {
            if (decision.ActorSubjectId != scope.SubjectId)
                throw new UnauthorizedAccessException("A decision must identify the authenticated actor.");
            var parent = ChangeTracker.Entries<ApprovalCase>().SingleOrDefault(x =>
                x.Entity.Id == decision.CaseId && x.Entity.TenantId == scope.TenantId
                && x.Entity.LegalEntityId == scope.LegalEntityId);
            if (parent is null || parent.State is not (EntityState.Unchanged or EntityState.Modified)
                || !parent.Collection(x => x.Stages).IsLoaded
                || parent.Entity.Stages.Any(stage => !Entry(stage).Collection(x => x.Assignments).IsLoaded))
                throw new InvalidOperationException("Load the persisted approval case with all stages and assignments before recording a decision.");
            var stage = parent.Entity.Stages.SingleOrDefault(x => x.Id == decision.StageId);
            if (stage is null || stage.CaseId != parent.Entity.Id
                || !stage.Assignments.Any(x => x.Id == decision.AssignmentId && x.CaseId == parent.Entity.Id
                    && x.StageId == stage.Id && (x.AssigneeSubjectId == decision.ActorSubjectId
                        || decision.Action == "Cancel" && decision.ActorSubjectId == parent.Entity.MakerSubjectId
                            && parent.Entity.State == "Cancelled" && stage.State == "Cancelled"
                            && x.State == "Cancelled" && parent.Entity.CompletionKey == decision.IdempotencyKey
                            && parent.Entity.CompletedAt is not null && !string.IsNullOrWhiteSpace(decision.Reason))))
                throw new InvalidOperationException("The decision must belong to the tracked approval case and assigned actor.");
            parent.Property(x => x.UpdatedAt).IsModified = true;
        }
        // Snapshot after marking cases; each tracked record receives exactly one revision per save.
        foreach (var entry in ChangeTracker.Entries<ApprovalRecord>().Where(x => x.State == EntityState.Modified).ToList())
        {
            entry.Entity.Version = checked(entry.Property(x => x.Version).OriginalValue + 1);
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
