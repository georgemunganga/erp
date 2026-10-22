using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Context authorization/history and relational model contracts; not a substitute for PostgreSQL constraint tests.</summary>
public sealed class ApprovalSubmissionPersistenceTests
{
    private sealed class Scope(string tenant, Guid company) : IProcurementScope
    {
        public string TenantId => tenant;
        public Guid LegalEntityId => company;
        public string SubjectId => "approval-test-user";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "approval-test";
    }

    private static ProcurementDbContext Context(string database, Guid company, string tenant = "tenant-a") =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options,
            new Scope(tenant, company));

    private static PurchaseRequestApprovalSubmission Evidence(Guid company) => new()
    {
        TenantId = "tenant-a", LegalEntityId = company, PurchaseRequestId = Guid.NewGuid(),
        SubmittedBusinessVersion = 4, CoreApprovalCaseId = Guid.NewGuid(), OpeningCommandKey = Guid.NewGuid(),
        MakerSubjectId = "requester", RequestSnapshotJson = "{\"restricted-purpose\":\"private\"}",
        RequestSnapshotSha256 = new string('a', 64), PolicyEvaluationId = Guid.NewGuid(),
        PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1,
        PolicyDecisionSha256 = new string('b', 64), PolicyRulesSha256 = new string('c', 64),
        FinanceSnapshotId = Guid.NewGuid(), FinanceSnapshotJson = "{\"private-finance-reference\":\"private\"}",
        FinanceSnapshotSha256 = new string('d', 64), FinanceEvidenceMode = "AutomatedFinance",
        AmountBasis = "EstimatedLineBase", EstimatedAmount = 10.12345678m,
        CurrencyCode = "ZMW", PinnedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Scoped_evidence_is_isolated_and_audit_contains_field_names_only()
    {
        var database = Guid.NewGuid().ToString();
        var company = Guid.NewGuid();
        await using var db = Context(database, company);
        var pin = Evidence(company);
        db.AddScoped(pin);
        await db.SaveChangesAsync();
        Assert.Equal("approval-test-user", pin.CreatedBy);
        var audit = await db.AuditEvents.SingleAsync();
        Assert.Equal(pin.Id, audit.RecordId);
        Assert.DoesNotContain("restricted-purpose", audit.ChangedFieldsJson ?? "");
        Assert.DoesNotContain("private-finance-reference", audit.ChangedFieldsJson ?? "");
        await using var otherTenant = Context(database, company, "tenant-b");
        await using var otherCompany = Context(database, Guid.NewGuid());
        Assert.Empty(await otherTenant.ApprovalSubmissions.ToListAsync());
        Assert.Empty(await otherCompany.ApprovalSubmissions.ToListAsync());
    }

    [Fact]
    public async Task Evidence_cannot_be_rewritten_even_through_EF_property_access()
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        var pin = Evidence(company);
        db.AddScoped(pin);
        await db.SaveChangesAsync();
        db.Entry(pin).Property(x => x.RequestSnapshotJson).CurrentValue = "{\"rewritten\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Evidence_cannot_be_physically_deleted()
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        var pin = Evidence(company);
        db.AddScoped(pin);
        await db.SaveChangesAsync();
        db.ApprovalSubmissions.Remove(pin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void AddScoped_cannot_relabel_evidence_created_for_a_different_source_scope()
    {
        var company = Guid.NewGuid();
        using var db = Context(Guid.NewGuid().ToString(), company);
        var foreign = Evidence(Guid.NewGuid());
        var originalCompany = foreign.LegalEntityId;
        Assert.Throws<InvalidOperationException>(() => db.AddScoped(foreign));
        Assert.Equal(originalCompany, foreign.LegalEntityId);
        Assert.Empty(db.ChangeTracker.Entries<PurchaseRequestApprovalSubmission>());
    }

    [Fact]
    public async Task Direct_attach_cannot_bypass_context_write_scope()
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        db.ApprovalSubmissions.Add(Evidence(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Model_enforces_scoped_unique_source_case_command_and_source_consistent_foreign_keys()
    {
        // Relational annotations must be inspected with the actual provider model.
        // Model construction never opens this deliberately invalid connection.
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_only;Username=invalid").Options,
            new Scope("tenant-a", Guid.NewGuid()));
        var model = db.Model.FindEntityType(typeof(PurchaseRequestApprovalSubmission))!;
        var unique = model.GetIndexes().Where(x => x.IsUnique)
            .Select(x => string.Join(",", x.Properties.Select(p => p.Name))).ToArray();
        Assert.Contains("TenantId,LegalEntityId,PurchaseRequestId,SubmittedBusinessVersion", unique);
        Assert.Contains("TenantId,LegalEntityId,CoreApprovalCaseId", unique);
        Assert.Contains("TenantId,LegalEntityId,OpeningCommandKey", unique);
        var fks = model.GetForeignKeys().ToArray();
        Assert.Equal(4, fks.Length);
        Assert.All(fks, fk =>
        {
            Assert.Equal(new[] { "TenantId", "LegalEntityId" }, fk.Properties.Take(2).Select(x => x.Name));
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        });
        Assert.Contains(fks, fk => fk.PrincipalEntityType.ClrType == typeof(PurchaseRequest));
        Assert.Contains(fks, fk => fk.PrincipalEntityType.ClrType == typeof(PolicyEvaluation)
            && fk.Properties.Any(x => x.Name == "PurchaseRequestId")
            && fk.Properties.Any(x => x.Name == "SubmittedBusinessVersion")
            && fk.Properties.Any(x => x.Name == "PolicyVersionId"));
        Assert.Contains(fks, fk => fk.PrincipalEntityType.ClrType == typeof(BudgetCheckSnapshot)
            && fk.Properties.Any(x => x.Name == "PurchaseRequestId")
            && fk.Properties.Any(x => x.Name == "SubmittedBusinessVersion"));
        Assert.DoesNotContain(fks, fk => fk.Properties.Any(x => x.Name == "CoreApprovalCaseId"));
        Assert.Equal("text", model.FindProperty("RequestSnapshotJson")!.GetColumnType());
        Assert.Equal(38, model.FindProperty("EstimatedAmount")!.GetPrecision());
        Assert.Equal(8, model.FindProperty("EstimatedAmount")!.GetScale());
        Assert.True(model.FindProperty("Version")!.IsConcurrencyToken);
    }
}
