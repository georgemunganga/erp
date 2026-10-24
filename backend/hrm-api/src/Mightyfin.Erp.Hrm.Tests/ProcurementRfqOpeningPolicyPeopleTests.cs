using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual Core resolution over isolated InMemory accounts/roles/branches.
/// No HTTP, PostgreSQL FK, employee or policy publication acceptance is implied.</summary>
public sealed class ProcurementRfqOpeningPolicyPeopleTests
{
    private sealed record Tenant : ITenantAccessor { public string GetTenantId() => "p32-targets"; }
    private sealed class Fixture : IDisposable
    {
        internal readonly HrmDbContext Core = new(new DbContextOptionsBuilder<HrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Tenant());
        internal readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), OtherBranch = Guid.NewGuid();
        internal readonly Guid Reviewer = Guid.NewGuid(), Closer = Guid.NewGuid(), Officer = Guid.NewGuid(), Reader = Guid.NewGuid();
        internal readonly ProcurementRequestScope Scope = new();
        internal PrincipalRfqCommercialRoundPolicy Rules = null!;
        internal async Task Start()
        {
            Core.LegalEntities.Add(new LegalEntity { Id = Company, TenantId = "p32-targets", Code = "P32", RegisteredName = "Synthetic company", Currency = "ZMW" });
            Core.WorkLocations.AddRange(new WorkLocation { Id = Branch, TenantId = "p32-targets", LegalEntityId = Company, Code = "A", Name = "A" },
                new WorkLocation { Id = OtherBranch, TenantId = "p32-targets", LegalEntityId = Company, Code = "B", Name = "B" });
            Add(Reviewer, "review", PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
            Add(Closer, "close", PrincipalRfqSealedManualOffers.CloseAction);
            Add(Officer, "open", PrincipalRfqOfferOpeningControls.OpenAction);
            Add(Reader, "read", PrincipalRfqOfferOpeningControls.ReadAction);
            Scope.Set("p32-targets", Company, Guid.NewGuid().ToString("D"), Branch, null, true, "source-unit");
            await Core.SaveChangesAsync();
            Rules = PrincipalRfqCommercialRoundPolicyRules.Parse(JsonSerializer.Serialize(new
            {
                schemaVersion = 2, identitySchemaVersion = 2, policyKey = "SOURCING_COMMERCIAL_ROUND",
                transactionType = "RfqCommercialRound", submissionMode = "ManualSurrogateOnly",
                requireOriginalOfferDocument = true, requireIndependentReview = true,
                closingOfficerPrincipalId = Closer.ToString("D"),
                stages = new[] { new { code = "R", mode = "One", principalIds = new[] { Reviewer.ToString("D") } } },
                offerOpening = new { officerPrincipalId = Officer.ToString("D"), readerPrincipalIds = new[] { Reader.ToString("D") },
                    allowClosingOfficer = false, delayAfterCloseSeconds = 0, reasonRequired = false, referenceRequired = false },
            }));
        }
        private void Add(Guid id, string role, string action)
        {
            Core.TenantRoleAssignments.Add(new TenantRoleAssignment { TenantId = "p32-targets", RoleKey = role,
                RoleName = role, Category = "system", Active = true, PermissionsCsv = "procurement-access," + action });
            Core.LocalUsers.Add(new LocalUser { Id = id, TenantId = "p32-targets", Email = id + "@example.test",
                NormalizedEmail = (id + "@example.test").ToUpperInvariant(), DisplayName = role,
                PasswordHash = "synthetic-not-a-login", IsActive = true, RolesCsv = role });
            Core.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = "p32-targets", UserId = id, LocationId = Branch });
        }
        internal Task<bool> Check() => ProcurementPolicyDraftRoutes.CommercialPolicyPeopleAvailable(Core, Scope, Rules, CancellationToken.None);
        public void Dispose() => Core.Dispose();
    }

    [Fact]
    public async Task Named_current_Core_targets_without_employees_can_be_adopted()
    {
        using var f = new Fixture(); await f.Start(); Assert.True(await f.Check());
        Assert.Empty(await f.Core.Workers.ToArrayAsync());
    }

    [Theory]
    [InlineData("open")]
    [InlineData("read")]
    public async Task Omitted_open_or_reader_current_grant_cannot_publish_controls(string role)
    {
        using var f = new Fixture(); await f.Start();
        (await f.Core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == role)).PermissionsCsv = "procurement-access";
        await f.Core.SaveChangesAsync(); Assert.False(await f.Check());
    }

    [Fact]
    public async Task Other_selected_branch_and_inactive_reader_are_not_available_targets()
    {
        using var f = new Fixture(); await f.Start();
        var assignment = await f.Core.UserBranchAssignments.SingleAsync(x => x.UserId == f.Reader);
        assignment.LocationId = f.OtherBranch; await f.Core.SaveChangesAsync(); Assert.False(await f.Check());
        assignment.LocationId = f.Branch;
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == f.Reader)).IsActive = false;
        await f.Core.SaveChangesAsync(); Assert.False(await f.Check());
    }

    [Fact]
    public async Task Two_current_accounts_sharing_known_worker_alias_are_not_independent_targets()
    {
        using var f = new Fixture(); await f.Start(); var alias = Guid.NewGuid();
        // Synthetic denial-only link inputs, not a persisted PostgreSQL worker FK proof.
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == f.Officer)).WorkerId = alias;
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == f.Reader)).WorkerId = alias;
        await f.Core.SaveChangesAsync(); Assert.False(await f.Check());
    }
}
