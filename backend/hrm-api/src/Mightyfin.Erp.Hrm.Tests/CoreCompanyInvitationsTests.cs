using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyInvitationsTests
{
    private const string Email = "invited+person@example.test";
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";

    [Fact]
    public async Task Issue_protects_secret_and_recipient_and_commits_one_durable_intent_before_acceptance()
    {
        await using var f = await Fixture.Start(); var command = f.Issue();
        Assert.DoesNotContain(Email, command.ToString());
        var issued = await f.Run(command); Assert.Equal(CompanyInvitationStatus.Issued, issued.Status);
        Assert.Equal(CompanyInvitationStatus.Existing, (await f.Run(command)).Status);
        Assert.Equal(CompanyInvitationStatus.Conflict, (await f.Run(command with { Role = "administrator" })).Status);
        Assert.Equal(CompanyInvitationStatus.Conflict, (await f.Run(command with { CommandKey = Guid.NewGuid() })).Status);
        await using var db = f.Core.Context(); var row = await db.CompanyInvitations.SingleAsync();
        Assert.DoesNotContain(Email, row.ProtectedDelivery); Assert.Equal(64, row.TokenHash.Length);
        Assert.Equal(48, (row.ExpiresAt-row.CreatedAt).TotalHours);
        Assert.Single(await db.CompanyMemberships.ToArrayAsync()); Assert.Single(await db.LocalUsers.ToArrayAsync());
        Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.EventType.StartsWith("core.company.invitation.")));
        Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
        foreach (var payload in await db.OutboxMessages.Select(x => x.PayloadJson).ToArrayAsync()) Assert.DoesNotContain(Email, payload);
        await f.NoOperationalGrants();
    }

    [Fact]
    public async Task Explicit_acceptance_is_atomic_and_retry_cannot_reactivate_a_later_revoked_member()
    {
        await using var f = await Fixture.Start(); await f.Run(f.Issue()); var command = await f.AcceptCommand();
        Assert.DoesNotContain(command.Token, command.ToString());
        var accepted = await f.Accept(command); Assert.Equal(CompanyInvitationStatus.Accepted, accepted.Status);
        Assert.Equal(CompanyInvitationStatus.Existing, (await f.Accept(command)).Status);
        Assert.Equal(CompanyInvitationStatus.Conflict, (await f.Accept(command with { CommandKey = Guid.NewGuid() })).Status);
        await using (var db = f.Core.Context()) {
            Assert.Equal(2, await db.LocalUsers.CountAsync()); Assert.Equal(2, await db.ExternalIdentityLinks.CountAsync());
            Assert.Equal(2, await db.CompanyMemberships.CountAsync()); Assert.Equal(2, await db.CompanyInvitationReceipts.CountAsync());
            var member = await db.CompanyMemberships.SingleAsync(x => x.Id == accepted.MembershipId);
            Assert.Equal(CoreCompanyRoles.Member, member.Role); Assert.True(member.IsActive);
            Assert.Equal("43", (await db.CompanyInvitations.SingleAsync()).AcceptedSubject);
        }
        var revoked = await f.Core.Service.ExecuteAsync(f.Core.Owner.Proof,
            new(f.Core.Company, accepted.MembershipId!.Value, Guid.NewGuid(), 1, CompanyTeamOperation.Revoke), f.Core.License());
        Assert.Equal(CompanyTeamStatus.Changed, revoked.Status);
        Assert.Equal(CompanyInvitationStatus.Denied, (await f.Accept(command)).Status);
        await using (var db = f.Core.Context()) Assert.False((await db.CompanyMemberships.SingleAsync(x => x.Id == accepted.MembershipId)).IsActive);
        await f.NoOperationalGrants();
    }

    [Theory]
    [InlineData("confirmation")][InlineData("token")][InlineData("recipient")][InlineData("client")]
    [InlineData("proof-expired")][InlineData("license")][InlineData("revision")][InlineData("inviter")][InlineData("company")]
    public async Task Wrong_or_stale_acceptance_cannot_create_identity_links_or_membership(string fault)
    {
        await using var f = await Fixture.Start(); await f.Run(f.Issue()); var command = await f.AcceptCommand();
        var proof = f.Proof(); var license = f.Core.License();
        if (fault == "confirmation") command = command with { Confirm = false };
        if (fault == "token") command = command with { Token = new string('x', 43) };
        if (fault == "recipient") proof = f.Proof(email: "unrelated@example.test");
        if (fault == "client") proof = f.Proof(client: Guid.NewGuid().ToString("D"));
        if (fault == "proof-expired") proof = f.Proof(lifetime: -1);
        if (fault == "license") license = f.Core.License("01arz3ndektsv4rrffq69g5faw");
        if (fault == "revision") command = command with { ExpectedRevision = 2 };
        if (fault == "inviter" || fault == "company") {
            await using var db = f.Core.Context();
            if (fault == "inviter") (await db.CompanyMemberships.SingleAsync()).IsActive = false;
            else (await db.LegalEntities.SingleAsync()).IsArchived = true;
            await db.SaveChangesAsync();
        }
        var result = await f.Service.AcceptAsync(proof, command, license);
        Assert.Contains(result.Status, new[] { CompanyInvitationStatus.Denied, CompanyInvitationStatus.Invalid, CompanyInvitationStatus.Conflict });
        await using (var db = f.Core.Context()) {
            Assert.Single(await db.LocalUsers.ToArrayAsync()); Assert.Single(await db.ExternalIdentityLinks.ToArrayAsync());
            Assert.Single(await db.CompanyMemberships.ToArrayAsync()); Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
            Assert.Equal("pending", (await db.CompanyInvitations.SingleAsync()).State);
        }
    }

    [Fact]
    public async Task Revocation_is_terminal_and_an_invitation_cannot_replace_member_role_changes()
    {
        await using var f = await Fixture.Start(); var issued = await f.Run(f.Issue()); var accept = await f.AcceptCommand();
        var revoke = new RevokeCompanyInvitation(f.Core.Company, issued.InvitationId!.Value, Guid.NewGuid(), 1, true);
        Assert.Equal(CompanyInvitationStatus.Revoked, (await f.Service.RevokeAsync(f.Core.Owner.Proof, revoke, f.Core.License())).Status);
        Assert.Equal(CompanyInvitationStatus.Existing, (await f.Service.RevokeAsync(f.Core.Owner.Proof, revoke, f.Core.License())).Status);
        Assert.Equal(CompanyInvitationStatus.Conflict, (await f.Accept(accept)).Status);
        await using var db = f.Core.Context(); Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Equal(2, await db.CompanyInvitationReceipts.CountAsync());
    }

    [Fact]
    public async Task Current_admin_can_invite_only_members_and_cannot_revoke_an_administrator_invitation()
    {
        await using var f = await Fixture.Start(); var admin = await f.Core.Add("44", CoreCompanyRoles.Administrator);
        Assert.Equal(CompanyInvitationStatus.Denied, (await f.Service.IssueAsync(admin.Proof, f.Issue("administrator"), f.Core.License())).Status);
        Assert.Equal(CompanyInvitationStatus.Issued, (await f.Service.IssueAsync(admin.Proof, f.Issue(), f.Core.License())).Status);
        var issued = await f.Run(f.Issue("administrator") with { Email = "different@example.test" });
        Assert.Equal(CompanyInvitationStatus.Denied, (await f.Service.RevokeAsync(admin.Proof,
            new(f.Core.Company, issued.InvitationId!.Value, Guid.NewGuid(), 1, true), f.Core.License())).Status);
        Assert.Equal(CompanyInvitationStatus.Invalid, (await f.Run(f.Issue("owner"))).Status);
    }

    [Theory]
    [InlineData("expire")][InlineData("fail")]
    public async Task Post_save_failure_rolls_back_invitation_receipt_delivery_and_membership(string mode)
    {
        await using var f = await Fixture.Start(); await f.Run(f.Issue()); var command = await f.AcceptCommand();
        f.Core.Probe.Mode = mode;
        var result = await f.Accept(command); Assert.NotEqual(CompanyInvitationStatus.Accepted, result.Status);
        await using var db = f.Core.Context(); Assert.Single(await db.LocalUsers.ToArrayAsync());
        Assert.Single(await db.CompanyMemberships.ToArrayAsync()); Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
        Assert.Equal("pending", (await db.CompanyInvitations.SingleAsync()).State);
        Assert.Single(await db.AuditEntries.Where(x => x.EntityType == "CoreCompanyInvitation").ToArrayAsync());
    }

    [Fact]
    public async Task Binding_fields_and_receipts_are_immutable_and_foreign_company_acceptance_is_rejected_by_database()
    {
        await using var f = await Fixture.Start(); await f.Run(f.Issue());
        await using (var db = f.Core.Context()) {
            (await db.CompanyInvitations.SingleAsync()).RecipientHash = new string('a', 64);
            await Assert.ThrowsAsync<Mightyfin.Erp.Hrm.Application.DomainException>(() => db.SaveChangesAsync());
        }
        await using (var db = f.Core.Context()) {
            (await db.CompanyInvitationReceipts.SingleAsync()).RequestHash = new string('a', 64);
            await Assert.ThrowsAsync<Mightyfin.Erp.Hrm.Application.DomainException>(() => db.SaveChangesAsync());
        }
        await using (var db = f.Core.Context()) {
            var row = await db.CompanyInvitations.SingleAsync(); row.State = "accepted"; row.Revision = 2;
            row.AcceptedSubject = "43"; row.AcceptedMembershipId = Guid.NewGuid();
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Expired_invitation_cannot_accept_but_a_new_explicit_invitation_is_allowed()
    {
        await using var f = await Fixture.Start(); await f.Run(f.Issue()); var command = await f.AcceptCommand();
        for (var i = 0; i < 1441; i++) f.Core.Clock.Advance();
        Assert.Equal(CompanyInvitationStatus.Expired, (await f.Accept(command)).Status);
        Assert.Equal(CompanyInvitationStatus.Issued, (await f.Run(f.Issue())).Status);
        await using var db = f.Core.Context(); Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Equal(2, await db.CompanyInvitations.CountAsync());
    }

    [Fact]
    public async Task Existing_active_members_cannot_be_reassigned_by_accepting_an_invitation()
    {
        await using var f = await Fixture.Start(); await f.Core.Add("43", CoreCompanyRoles.Member);
        await f.Run(f.Issue("administrator")); var command = await f.AcceptCommand();
        Assert.Equal(CompanyInvitationStatus.Conflict, (await f.Accept(command)).Status);
        await using var db = f.Core.Context();
        Assert.All(await db.CompanyMemberships.Where(x => x.LocalUserId != f.Core.Owner.User).ToArrayAsync(), x => Assert.Equal("member", x.Role));
        Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task A_new_consented_invitation_can_reactivate_only_the_requested_role_without_duplicate_principals()
    {
        await using var f = await Fixture.Start(); var prior = await f.Core.Add("43", CoreCompanyRoles.Administrator);
        Assert.Equal(CompanyTeamStatus.Changed, (await f.Core.Service.ExecuteAsync(f.Core.Owner.Proof,
            new(f.Core.Company, prior.Member, Guid.NewGuid(), 1, CompanyTeamOperation.Revoke), f.Core.License())).Status);
        await f.Run(f.Issue()); var accepted = await f.Accept(await f.AcceptCommand());
        Assert.Equal(CompanyInvitationStatus.Accepted, accepted.Status); Assert.Equal(prior.Member, accepted.MembershipId);
        await using var db = f.Core.Context(); var member = await db.CompanyMemberships.SingleAsync(x => x.Id == prior.Member);
        Assert.True(member.IsActive); Assert.Equal("member", member.Role); Assert.Equal(3, member.Revision);
        Assert.Equal(2, await db.LocalUsers.CountAsync()); Assert.Equal(2, await db.ExternalIdentityLinks.CountAsync());
        var audit = await db.AuditEntries.SingleAsync(x => x.Action == "core.company.member.invitation.accepted");
        Assert.Contains("\"Revision\":2", audit.BeforeJson); Assert.Contains("\"Revision\":3", audit.AfterJson);
        await f.NoOperationalGrants();
    }

    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_concurrent_issue_and_accept_have_one_delivery_and_one_membership()
    {
        await using var f = await Fixture.Start(); var issue = f.Issue();
        var issued = await Task.WhenAll(f.Run(issue), f.Run(issue));
        Assert.Single(issued, x => x.Status == CompanyInvitationStatus.Issued);
        Assert.Single(issued, x => x.Status == CompanyInvitationStatus.Existing);
        var accept = await f.AcceptCommand();
        var accepted = await Task.WhenAll(f.Accept(accept), f.Accept(accept with { CommandKey = Guid.NewGuid() }));
        Assert.Single(accepted, x => x.Status == CompanyInvitationStatus.Accepted);
        Assert.Single(accepted, x => x.Status == CompanyInvitationStatus.Conflict);
        await using var db = f.Core.Context(); Assert.Equal(2, await db.CompanyMemberships.CountAsync());
        Assert.Equal(2, await db.CompanyInvitationReceipts.CountAsync());
        Assert.Single(await db.OutboxMessages.Where(x => x.EventType == "core.company.invitation.delivery.requested.v1").ToArrayAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "core.company.member.invitation.accepted").ToArrayAsync());
        await f.NoOperationalGrants();
    }

    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_revoke_accept_race_is_one_terminal_outcome_without_partial_membership()
    {
        await using var f = await Fixture.Start(); var issued = await f.Run(f.Issue()); var accept = await f.AcceptCommand();
        var results = await Task.WhenAll(f.Accept(accept), f.Service.RevokeAsync(f.Core.Owner.Proof,
            new(f.Core.Company, issued.InvitationId!.Value, Guid.NewGuid(), 1, true), f.Core.License()));
        Assert.Single(results, x => x.Status is CompanyInvitationStatus.Accepted or CompanyInvitationStatus.Revoked);
        Assert.Single(results, x => x.Status == CompanyInvitationStatus.Conflict);
        await using var db = f.Core.Context(); var row = await db.CompanyInvitations.SingleAsync();
        Assert.Equal(row.State == "accepted" ? 2 : 1, await db.CompanyMemberships.CountAsync());
        Assert.Equal(2, await db.CompanyInvitationReceipts.CountAsync()); await f.NoOperationalGrants();
    }

    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_binding_receipts_and_populated_rollback_are_guarded_in_PostgreSQL()
    {
        await using var f = await Fixture.Start(); var issued = await f.Run(f.Issue());
        await using var db = f.Core.Context();
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE hrm.core_company_invitations SET role='administrator' WHERE id={issued.InvitationId!.Value}"));
        Assert.Equal("23514", immutable.SqlState);
        var receipt = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE hrm.core_company_invitation_receipts SET request_hash=repeat('a',64)"));
        Assert.Equal("23514", receipt.SqlState);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261108113644_CoreCompanyTeamCommands"));
        Assert.Single(await db.CompanyInvitations.ToArrayAsync()); Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_empty_invitation_schema_can_roll_back_and_reapply_without_losing_the_original_owner()
    {
        await using var f = await Fixture.Start(); await using var db = f.Core.Context();
        await db.GetService<IMigrator>().MigrateAsync("20261108113644_CoreCompanyTeamCommands");
        Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        await db.GetService<IMigrator>().MigrateAsync(); Assert.Empty(await db.CompanyInvitations.ToArrayAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private sealed class Fixture(CoreCompanyTeamCommandsTests.Fixture core) : IAsyncDisposable
    {
        internal CoreCompanyTeamCommandsTests.Fixture Core => core;
        private readonly IDataProtector protection = new EphemeralDataProtectionProvider().CreateProtector("kuubu-company-invitation-delivery-v1");
        internal CoreCompanyInvitations Service => new(x => core.Context(x), core.Clock, protection);
        internal IssueCompanyInvitation Issue(string role = "member") => new(core.Company, Guid.NewGuid(), Email, role, true);
        internal Task<CompanyInvitationResult> Run(IssueCompanyInvitation command) => Service.IssueAsync(core.Owner.Proof, command, core.License());
        internal InvitationRecipientObservation Proof(string email = Email, string client = Client, int lifetime = 30)
            => new("43", client, email, core.Clock.GetUtcNow().AddSeconds(lifetime), core.Clock, core.Clock.GetTimestamp(), TimeSpan.FromSeconds(lifetime));
        internal Task<CompanyInvitationResult> Accept(AcceptCompanyInvitation command) => Service.AcceptAsync(Proof(), command, core.License());
        internal async Task<AcceptCompanyInvitation> AcceptCommand()
        {
            await using var db = core.Context(); var row = await db.CompanyInvitations.SingleAsync();
            using var delivery = JsonDocument.Parse(protection.Unprotect(row.ProtectedDelivery));
            var url = new Uri(delivery.RootElement.GetProperty("review_url").GetString()!);
            return new(url.Fragment[7..], Guid.NewGuid(), 1, true);
        }
        internal async Task NoOperationalGrants()
        {
            await using var db = core.Context(); Assert.Empty(await db.LocalSessions.ToArrayAsync());
            Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync()); Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
            Assert.All(await db.LocalUsers.ToArrayAsync(), x => { Assert.Null(x.PasswordHash); Assert.Equal("", x.RolesCsv); });
        }
        internal static async Task<Fixture> Start() => new(await CoreCompanyTeamCommandsTests.Fixture.Start());
        public ValueTask DisposeAsync() => core.DisposeAsync();
    }
}
