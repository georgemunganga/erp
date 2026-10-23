using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

// Actual owning Core projections; no registered payment endpoint or PostgreSQL claim.
public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Payment_people_without_employees_can_be_independent_without_creating_any_business_records()
    {
        await using var f = await Fixture.Start();
        await f.ConfigurePrincipalRequester(f.Owner, false);
        await f.ConfigurePrincipalRequester(f.Other, false);
        var request = await f.PaymentPersonEvidence();
        var before = await f.OriginalValueFootprint();
        Assert.True(await f.PaymentIndependence(request, "assign"));
        Assert.True(await f.PaymentIndependence(request, "approve"));
        Assert.True(await f.PaymentIndependence(request, "record"));
        Assert.True(await f.PaymentIndependence(request, "confirm"));
        Assert.Equal(before, await f.OriginalValueFootprint());
    }

    [Fact]
    public async Task Payment_confirmation_keeps_assigned_recorder_evidence_when_the_actual_recording_link_changed()
    {
        await using var f = await Fixture.Start();
        var request = await f.PaymentPersonEvidence();
        await f.SetFinanceWorker(f.Owner, f.Reviewer);
        var now = await f.PaymentPersonEvidence();
        request.RecordedByCoreLinkedWorkerId = now.RecordedByCoreLinkedWorkerId;
        await f.SetFinanceWorker(f.Other, f.Owner);
        Assert.False(await f.PaymentIndependence(request, "confirm"));
    }

    [Theory]
    [InlineData("assign")][InlineData("approve")][InlineData("record")][InlineData("confirm")]
    public async Task Payment_people_current_aliases_veto_distinct_accounts_even_after_HR_deactivation(string operation)
    {
        await using var f = await Fixture.Start();
        await f.ConfigurePrincipalRequester(f.Owner, true);
        await f.ConfigurePrincipalRequester(f.Other, true);
        var request = await f.PaymentPersonEvidence();
        Assert.True(await f.PaymentIndependence(request, operation));
        await f.SetFinanceWorker(f.Other, f.Owner);
        await f.DeactivatePrincipalEmployee(f.Owner);
        Assert.False(await f.PaymentIndependence(request, operation));
    }

    [Theory]
    [InlineData("approve")][InlineData("confirm")]
    public async Task Payment_people_retained_link_still_vetoes_an_alias_after_the_original_link_is_removed(string operation)
    {
        await using var f = await Fixture.Start();
        await f.ConfigurePrincipalRequester(f.Owner, true);
        await f.ConfigurePrincipalRequester(f.Other, true);
        var request = await f.PaymentPersonEvidence();
        await f.SetFinanceWorker(f.Other, f.Owner);
        await f.ConfigurePrincipalRequester(f.Owner, false);
        Assert.False(await f.PaymentIndependence(request, operation));
    }

    [Theory]
    [InlineData("approve")][InlineData("record")][InlineData("confirm")]
    public async Task Payment_people_independence_does_not_substitute_a_different_assigned_account(string operation)
    {
        await using var f = await Fixture.Start();
        var request = await f.PaymentPersonEvidence();
        Assert.False(await f.PaymentIndependence(request, operation, f.Reviewer.ToString("D")));
    }

    private sealed partial class Fixture
    {
        public async Task<ManualPaymentRequest> PaymentPersonEvidence()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var owner = await ProcurementCorePrincipals.CurrentAsync(core, "tenant-a", Owner.ToString("D"));
            var other = await ProcurementCorePrincipals.CurrentAsync(core, "tenant-a", Other.ToString("D"));
            Assert.NotNull(owner); Assert.NotNull(other);
            return new ManualPaymentRequest { TenantId = "tenant-a", LegalEntityId = company,
                CreatorPrincipalId = Owner.ToString("D"), CreatorCoreLinkedWorkerId = owner.WorkerId,
                ApproverPrincipalId = Other.ToString("D"), ApproverCoreLinkedWorkerId = other.WorkerId,
                RecorderPrincipalId = Owner.ToString("D"), RecorderCoreLinkedWorkerId = owner.WorkerId,
                ConfirmerPrincipalId = Other.ToString("D"), ConfirmerCoreLinkedWorkerId = other.WorkerId,
                RecordedByPrincipalId = Owner.ToString("D"), RecordedByCoreLinkedWorkerId = owner.WorkerId };
        }

        public async Task<bool> PaymentIndependence(ManualPaymentRequest request, string operation, string? actor = null)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            return operation switch {
                "assign" => await ProcurementManualPaymentPeople.IndependentAssignmentsNow(core, request, default),
                "approve" => await ProcurementManualPaymentPeople.IndependentApprovalNow(core, request, actor ?? Other.ToString("D"), default),
                "record" => await ProcurementManualPaymentPeople.IndependentRecordingNow(core, request, actor ?? Owner.ToString("D"), default),
                "confirm" => await ProcurementManualPaymentPeople.IndependentConfirmationNow(core, request, actor ?? Other.ToString("D"), default),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }
    }
}
