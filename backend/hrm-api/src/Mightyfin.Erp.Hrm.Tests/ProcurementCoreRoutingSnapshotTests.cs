using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCoreRoutingSnapshotTests
{
    private static string Rules => JsonSerializer.Serialize(new
    {
        schemaVersion = 2, transactionType = "PurchaseRequest", currencyCode = "ZMW", amountBasis = "EstimatedLineBase",
        rules = new[] { new { code = "DEFAULT", priority = 0,
            when = new { minimumAmountInclusive = (decimal?)null, maximumAmountExclusive = (decimal?)null,
                categoryCodes = Array.Empty<string>(), purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>(), requesterRoleKeys = Array.Empty<string>() },
            result = new { budgetReservation = "Required", sourcingMethodCode = (string?)null, minimumQuoteCount = (int?)null,
                approvalStages = new[] { new { code = "EXECUTIVE", assignee = "ProcurementManager", mode = "One", binding = new { kind = "CoreRole", key = "executive" } } } } } }
    });

    private static (ProcurementRequestScope Scope, PurchaseRequest Request) NewRequest()
    {
        var maker = Guid.NewGuid(); var scope = new ProcurementRequestScope();
        scope.Set("routing-fixture", Guid.NewGuid(), maker.ToString("D"), null, null, false, "routing-test");
        scope.AssignPrincipal(maker);
        var request = new PurchaseRequest { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            Number = "PR-FIXTURE", CurrencyCode = "ZMW", Purpose = "Routing fixture", SourceChannel = "ERP",
            RequesterPrincipalId = maker, CreatedBy = scope.SubjectId };
        PurchaseRequestPrincipalIdentity.CaptureNew(request, scope.SubjectId, null);
        request.Status = "Submitted"; request.Version = 2; request.SubmittedVersion = 2; request.SubmittedAt = DateTimeOffset.UtcNow;
        request.Lines.Add(new PurchaseRequestLine { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            PurchaseRequestId = request.Id, LineNumber = 1, Description = "Fixture service", UnitCode = "EA",
            RequestedQuantity = 1, EstimatedUnitPrice = 100 });
        return (scope, request);
    }

    [Theory]
    [InlineData("Submission", false, false)]
    [InlineData("Submission", true, true)]
    [InlineData("ManualCheck", true, false)]
    public async Task Only_matching_server_submission_proof_can_capture_roles(string trigger, bool proof, bool wrongPerson)
    {
        var (scope, request) = NewRequest();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        db.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", State = "Published", PolicyVersionNumber = 1,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), RulesJson = Rules });
        await db.SaveChangesAsync();
        var principal = proof ? new ProcurementCorePrincipal(wrongPerson ? Guid.NewGuid() : Guid.Parse(scope.SubjectId),
            null, ["supervisor"], ["procurement-access"]) : null;
        var result = await ProcurementPolicyEvaluation.Assess(request, db, scope, trigger, default, principal);
        Assert.Equal("RequesterRoleSnapshotRequired", result.Status);
        Assert.Null(result.Evaluation);
        Assert.Empty(db.ChangeTracker.Entries<PolicyEvaluation>());
    }

    [Fact]
    public async Task Workerless_submission_pins_roles_and_retry_keeps_original_snapshot()
    {
        var (scope, request) = NewRequest();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        db.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", State = "Published", PolicyVersionNumber = 1,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), RulesJson = Rules });
        await db.SaveChangesAsync();
        var principal = new ProcurementCorePrincipal(Guid.Parse(scope.SubjectId), null, ["supervisor"], ["procurement-access"]);
        var result = await ProcurementPolicyEvaluation.Assess(request, db, scope, "Submission", default, principal);
        Assert.Equal("Matched", result.Status);
        Assert.False(result.IsAuthority);
        var retained = Assert.IsType<PolicyEvaluation>(result.Evaluation);
        using var json = JsonDocument.Parse(retained.DecisionJson);
        Assert.True(PurchaseRequestRoutingSnapshot.Valid(json.RootElement, scope.SubjectId, request.SubmittedAt));
        Assert.Equal("supervisor", json.RootElement.GetProperty("requesterRoleKeys")[0].GetString());
        Assert.True(ApprovalRoutePreview.FromEvaluation(retained).HasSingleRoute);
        Assert.Null(request.RequesterCoreWorkerId);
        await db.SaveChangesAsync();
        var retry = await ProcurementPolicyEvaluation.Assess(request, db, scope, "ManualCheck", default,
            principal with { Roles = ["executive"] });
        Assert.Equal(retained.DecisionJson, retry.Evaluation!.DecisionJson);
        Assert.Single(await db.PolicyEvaluations.ToArrayAsync());
    }
}
