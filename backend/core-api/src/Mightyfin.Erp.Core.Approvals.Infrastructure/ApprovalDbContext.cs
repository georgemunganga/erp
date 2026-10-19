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

    public override int SaveChanges() { PrepareChanges(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    { PrepareChanges(); return base.SaveChangesAsync(cancellationToken); }

    private void PrepareChanges()
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.SubjectId))
            throw new UnauthorizedAccessException("An authenticated approval scope is required.");
        foreach (var entry in ChangeTracker.Entries<ApprovalRecord>().Where(x => x.State != EntityState.Unchanged))
        {
            if (entry.State == EntityState.Deleted ||
                entry.State == EntityState.Modified && entry.Entity is ApprovalDecision)
                throw new InvalidOperationException("Approval history cannot be changed or deleted.");
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                throw new UnauthorizedAccessException("Approval record is outside the actor scope.");
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}
