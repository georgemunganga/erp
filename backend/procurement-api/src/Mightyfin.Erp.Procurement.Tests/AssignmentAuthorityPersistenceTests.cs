using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class AssignmentAuthorityPersistenceTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("case")]
    [InlineData("principal")]
    [InlineData("grant-revision")]
    [InlineData("authority-version")]
    [InlineData("hash")]
    [InlineData("json")]
    [InlineData("stage")]
    [InlineData("assignee-kind")]
    [InlineData("source-amount")]
    [InlineData("worker")]
    [InlineData("legacy-worker")]
    public async Task Added_assignment_evidence_must_match_exact_local_source_and_current_grant(string mutation)
    {
        var scope = new Scope();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var time = DateTimeOffset.UtcNow;
        var actor = new SpendingAuthorityConfigurationActor(scope.TenantId, scope.LegalEntityId, scope.SubjectId, true,
            new HashSet<string>([SpendingAuthorityConfiguration.ConfigurePermission]));
        var grant = SpendingAuthorityConfiguration.CreateDraft("FINITE", "approver", new("ZMW", "EstimatedLineBase", 1000,
            time.AddDays(-1), null, true, null, true, null, true), actor, time, []);
        db.AddScoped(grant); await db.SaveChangesAsync();
        var subject = new SpendingAuthoritySubject(scope.TenantId, scope.LegalEntityId, "approver", true, true);
        SpendingAuthorityConfiguration.Submit(grant, grant.Version, actor, subject, time);
        await db.SaveChangesAsync();
        scope.SubjectId = "independent-reviewer";
        SpendingAuthorityConfiguration.Activate(grant, grant.Version,
            actor with { PrincipalId = scope.SubjectId, Permissions = new HashSet<string>([SpendingAuthorityConfiguration.ReviewPermission]) },
            subject, "Independently reviewed", time, [grant]);
        await db.SaveChangesAsync();
        var request = new PurchaseRequest { RequesterWorkerId = Guid.NewGuid(), Number = "PR-1", Purpose = "Synthetic need", CurrencyCode = "ZMW" };
        db.AddScoped(request);
        var line = new PurchaseRequestLine { PurchaseRequestId = request.Id, LineNumber = 1,
            Description = "Synthetic item", UnitCode = "each", RequestedQuantity = 1, EstimatedUnitPrice = 100,
            EstimatedCurrencyCode = "ZMW", CategoryCode = "IT" };
        db.AddScoped(line); request.Lines.Add(line);
        var caseId = Guid.NewGuid(); var makerWorker = Guid.NewGuid();
        var pin = new PurchaseRequestApprovalSubmission
        {
            PurchaseRequestId = request.Id, SubmittedBusinessVersion = 1, CoreApprovalCaseId = caseId, SnapshotSchemaVersion = 2,
            MakerWorkerId = makerWorker, RequesterWorkerId = request.RequesterWorkerId,
            WorkerIdentitySha256 = PurchaseRequestApprovalPinning.WorkerIdentityDigest(scope.TenantId, scope.LegalEntityId,
                request.Id, 1, caseId, scope.SubjectId, makerWorker, request.RequesterWorkerId),
            OpeningCommandKey = Guid.NewGuid(), MakerSubjectId = scope.SubjectId, RequestSnapshotJson = "{}",
            RequestSnapshotSha256 = new string('a', 64), PolicyEvaluationId = Guid.NewGuid(), PolicyVersionId = Guid.NewGuid(),
            PolicyVersionNumber = 1, PolicyDecisionSha256 = new string('b', 64), PolicyRulesSha256 = new string('c', 64),
            FinanceSnapshotId = Guid.NewGuid(), FinanceSnapshotJson = "{}", FinanceSnapshotSha256 = new string('d', 64),
            FinanceEvidenceMode = "AutomatedFinance", AmountBasis = "EstimatedLineBase", EstimatedAmount = 100,
            CurrencyCode = "ZMW", PinnedAt = time,
        };
        db.AddScoped(pin); await db.SaveChangesAsync();
        var evaluated = PurchaseRequestSpendingAuthority.Evaluate(request, "approver", SpendingAuthorityConfiguration.ApprovalPermission,
            SpendingAuthorityConfiguration.ApprovalAction, "EstimatedLineBase", time, [grant]);
        Assert.True(evaluated.Eligible);
        var workerId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new PurchaseRequestAssignmentIdentityEvidence(workerId, evaluated.Evidence!));
        var evidence = new PurchaseRequestAssignmentAuthorityEvidence
        {
            SubmissionId = pin.Id, CoreApprovalCaseId = pin.CoreApprovalCaseId, CoreStageId = Guid.NewGuid(),
            CoreAssignmentId = Guid.NewGuid(), PrincipalId = "approver", AssigneeWorkerId = workerId, AssigneeKind = "LineManager",
            GrantId = grant.Id, GrantRevision = grant.Version, AuthorityVersion = grant.AuthorityVersion,
            EvidenceJson = json, EvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
        };
        db.AddScoped(evidence);
        switch (mutation)
        {
            case "case": db.Entry(evidence).Property(x => x.CoreApprovalCaseId).CurrentValue = Guid.NewGuid(); break;
            case "principal": db.Entry(evidence).Property(x => x.PrincipalId).CurrentValue = "other"; break;
            case "grant-revision": db.Entry(evidence).Property(x => x.GrantRevision).CurrentValue = grant.Version + 1; break;
            case "authority-version": db.Entry(evidence).Property(x => x.AuthorityVersion).CurrentValue = grant.AuthorityVersion + 1; break;
            case "hash": db.Entry(evidence).Property(x => x.EvidenceSha256).CurrentValue = new string('f', 64); break;
            case "json": db.Entry(evidence).Property(x => x.EvidenceJson).CurrentValue = "invalid json"; break;
            case "stage": db.Entry(evidence).Property(x => x.CoreStageId).CurrentValue = Guid.Empty; break;
            case "assignee-kind": db.Entry(evidence).Property(x => x.AssigneeKind).CurrentValue = "BudgetOwner"; break;
            case "worker": db.Entry(evidence).Property(x => x.AssigneeWorkerId).CurrentValue = Guid.NewGuid(); break;
            case "legacy-worker": db.Entry(evidence).Property(x => x.AssigneeWorkerId).CurrentValue = null; break;
            case "source-amount": line.EstimatedUnitPrice = 101; break;
        }
        if (mutation == "valid") Assert.True(await db.SaveChangesAsync() > 0);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Local_foreign_keys_bind_submission_case_and_immutable_grant_identity()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_only;Username=invalid").Options, new Scope());
        var evidence = db.Model.FindEntityType(typeof(PurchaseRequestAssignmentAuthorityEvidence))!;
        var keys = evidence.GetForeignKeys().Select(x => string.Join(",", x.Properties.Select(p => p.Name))).ToArray();
        Assert.Contains("TenantId,LegalEntityId,SubmissionId,CoreApprovalCaseId", keys);
        Assert.Contains("TenantId,LegalEntityId,GrantId,PrincipalId,AuthorityVersion", keys);
        Assert.DoesNotContain(evidence.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "GrantRevision"));
    }

    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "assignment-evidence";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; set; } = "configuration-owner";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "test";
    }
}
