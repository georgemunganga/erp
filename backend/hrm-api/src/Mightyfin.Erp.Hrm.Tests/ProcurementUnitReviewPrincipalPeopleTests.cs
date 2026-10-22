using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Hrm.Tests;
// Current owning Core projection/known-alias tests, not PostgreSQL workflow authority.
public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Named_reviewer_without_employee_uses_current_stored_grants_and_account_revocation()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-invoice-unit-review");
        var person = await fixture.UnitReviewPerson(fixture.Owner);
        Assert.NotNull(person); Assert.Null(person.WorkerId);
        Assert.Equal(fixture.Owner.ToString("D"), person.PrincipalId);
        Assert.Contains(InvoiceRetainedUnitReview.ReviewPermission, person.Permissions);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access");
        person = await fixture.UnitReviewPerson(fixture.Owner);
        Assert.NotNull(person); Assert.DoesNotContain(InvoiceRetainedUnitReview.ReviewPermission, person.Permissions);
        await fixture.DeactivatePrincipalAccount(fixture.Owner);
        Assert.Null(await fixture.UnitReviewPerson(fixture.Owner));
    }
    [Fact]
    public async Task Terminated_employee_does_not_remove_Core_access_or_make_a_known_alias_independent()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, true);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-invoice-unit-review");
        await fixture.SetFinanceWorker(fixture.Other, fixture.Owner);
        await fixture.DeactivatePrincipalEmployee(fixture.Owner);
        var person = await fixture.UnitReviewPerson(fixture.Owner);
        Assert.NotNull(person); Assert.Null(person.WorkerId);
        Assert.Contains(InvoiceRetainedUnitReview.ReviewPermission, person.Permissions);
        Assert.False(await fixture.UnitReviewIndependent(fixture.Owner, fixture.Other));
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigurePrincipalRequester(fixture.Other, false);
        Assert.True(await fixture.UnitReviewIndependent(fixture.Owner, fixture.Other));
        Assert.False(await fixture.UnitReviewIndependent(fixture.Owner, fixture.Owner));
    }
    private sealed partial class Fixture
    {
        public async Task<InvoiceUnitReviewPolicyRules.Person?> UnitReviewPerson(Guid user)
        {
            using var services = app.Services.CreateScope();
            return await ProcurementInvoiceUnitReviewPeople.Resolve(services.ServiceProvider.GetRequiredService<HrmDbContext>(),
                "tenant-a", company, user.ToString("D"), default);
        }
        public async Task<bool> UnitReviewIndependent(Guid maker, Guid reviewer)
        {
            using var services = app.Services.CreateScope();
            return await ProcurementPrincipalPeople.IndependentNow(services.ServiceProvider.GetRequiredService<HrmDbContext>(),
                "tenant-a", maker.ToString("D"), null, reviewer.ToString("D"), null, default);
        }
    }
}
