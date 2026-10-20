using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ApprovalExecutionEvidenceTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "execution-evidence-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "execution-evidence";
    }
    private static ProcurementDbContext Db(Scope scope) => new(new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);

    [Fact]
    public async Task Durable_receipt_cannot_be_rewritten_or_deleted_through_any_SaveChanges_overload()
    {
        var scope = new Scope(); await using var db = Db(scope);
        var receipt = Receipt(scope.SubjectId); db.AddScoped(receipt); await db.SaveChangesAsync();
        db.Entry(receipt).Property(x => x.ResponseJson).CurrentValue = "{\"changed\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges(true));
        db.Entry(receipt).State = EntityState.Unchanged;
        db.ApprovalCommandReceipts.Remove(receipt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_receipt_must_name_current_actor_and_explicit_command_identity()
    {
        var scope = new Scope(); await using var db = Db(scope);
        db.AddScoped(Receipt("another-principal"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var receipt = Receipt(scope.SubjectId);
        db.AddScoped(receipt);
        db.Entry(receipt).Property(x => x.CommandKey).CurrentValue = Guid.Empty;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static ProcurementApprovalCommandReceipt Receipt(string actor) => new()
    {
        Operation = "Open", CommandKey = Guid.NewGuid(), ActorSubjectId = actor,
        PurchaseRequestId = Guid.NewGuid(), CoreApprovalCaseId = Guid.NewGuid(),
        CommandFingerprint = new string('a', 64), ResponseJson = "{}", CompletedAt = DateTimeOffset.UtcNow,
    };
}
