using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementFinanceWorkerIndependenceTests
{
    [Fact]
    public void Retained_distinct_maker_and_requester_both_exclude_alias_workers_without_current_maker_login()
    {
        var (request, pin, scope) = Evidence();
        foreach (var worker in new[] { pin.MakerWorkerId!.Value, pin.RequesterWorkerId!.Value, Guid.Empty })
            Assert.Equal("FinanceEvidenceVerifierInvalid", Assert.Throws<InvalidOperationException>(() =>
                ProcurementFinanceWorkerIndependence.RequireRetainedMaker(request, pin, worker, scope)).Message);
        // No Core account lookup is needed for an offboarded historical maker.
        ProcurementFinanceWorkerIndependence.RequireRetainedMaker(request, pin, Guid.NewGuid(), scope);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("company")]
    [InlineData("case")]
    [InlineData("request")]
    [InlineData("worker")]
    [InlineData("legacy-schema")]
    public void Unbound_or_tampered_worker_pin_is_not_retained_maker_authority(string changed)
    {
        var (request, pin, scope) = Evidence();
        void Tamper(string property, object value) => typeof(PurchaseRequestApprovalSubmission).GetProperty(property)!.SetValue(pin, value);
        switch (changed)
        {
            case "version": request.SubmittedVersion++; break;
            case "company": pin.LegalEntityId = Guid.NewGuid(); break;
            case "case": request.ApprovalInstanceRef = Guid.NewGuid().ToString("D"); break;
            case "request": Tamper(nameof(pin.PurchaseRequestId), Guid.NewGuid()); break;
            case "worker": Tamper(nameof(pin.MakerWorkerId), Guid.NewGuid()); break;
            case "legacy-schema": Tamper(nameof(pin.SnapshotSchemaVersion), 1); break;
        }
        Assert.Equal("FinanceEvidenceMakerIdentityUnavailable", Assert.Throws<InvalidOperationException>(() =>
            ProcurementFinanceWorkerIndependence.RequireRetainedMaker(request, pin, Guid.NewGuid(), scope)).Message);
    }

    private static (PurchaseRequest Request, PurchaseRequestApprovalSubmission Pin, ProcurementRequestScope Scope) Evidence()
    {
        var company = Guid.NewGuid(); var caseId = Guid.NewGuid(); var maker = Guid.NewGuid().ToString("D"); var makerWorker = Guid.NewGuid();
        var request = new PurchaseRequest { TenantId = "test", LegalEntityId = company, CreatedBy = maker,
            RequesterWorkerId = Guid.NewGuid(), SubmittedVersion = 4, ApprovalInstanceRef = caseId.ToString("D") };
        var pin = new PurchaseRequestApprovalSubmission { TenantId = "test", LegalEntityId = company,
            PurchaseRequestId = request.Id, CoreApprovalCaseId = caseId, SubmittedBusinessVersion = 4,
            MakerSubjectId = maker, MakerWorkerId = makerWorker, RequesterWorkerId = request.RequesterWorkerId,
            SnapshotSchemaVersion = 2, WorkerIdentitySha256 = PurchaseRequestApprovalPinning.WorkerIdentityDigest("test", company,
                request.Id, 4, caseId, maker, makerWorker, request.RequesterWorkerId) };
        var scope = new ProcurementRequestScope();
        scope.Set("test", company, Guid.NewGuid().ToString("D"), null, null, false, "retained-worker-test");
        return (request, pin, scope);
    }
}
