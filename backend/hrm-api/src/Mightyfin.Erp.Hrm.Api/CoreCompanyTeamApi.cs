using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum CompanyTeamReadStatus { Ok, Denied, Unavailable }
internal sealed record CompanyTeamCompany(Guid CompanyId, Guid LinkId, string CompanyName, string Role);
internal sealed record CompanyTeamMember(Guid MembershipId, string DisplayName, string Role,
    bool MembershipActive, bool IdentityAvailable, int Revision);
internal sealed record CompanyTeamPage<T>(CompanyTeamReadStatus Status, T[]? Items = null,
    string? Next = null, string? ActorRole = null);

/// <summary>Company governance orchestration; neither payer authority nor an ERP session.</summary>
internal sealed class CoreCompanyTeamApi(Func<string, HrmDbContext> contextFactory,
    Func<string, CancellationToken, Task<CommercialLicenseResult>> readLicense, TimeProvider clock)
{
    internal async Task<CompanyTeamPage<CompanyTeamCompany>> CompaniesAsync(string issuer, string subject,
        Guid? after, CancellationToken cancellation = default)
    {
        try
        {
            await using var db = contextFactory("untrusted-team-directory");
            var page = await new CoreCompanyAccess(db, clock).DirectoryAsync(issuer, subject, after, cancellation);
            // Discovery metadata only. Each member read/command independently
            // resolves the immutable enrollment and checks current licensing.
            return new(CompanyTeamReadStatus.Ok, page.Rows.Select(row => new CompanyTeamCompany(
                row.CompanyId, row.LinkId, row.CompanyName, row.Role)).ToArray(), page.Next);
        }
        catch (DbException) { return new(CompanyTeamReadStatus.Unavailable); }
    }

    internal async Task<CompanyTeamPage<CompanyTeamMember>> MembersAsync(CompanyTeamActor actor,
        Guid companyId, Guid? after, CancellationToken cancellation = default)
    {
        try
        {
            var ready = await Resolve(actor, companyId, cancellation);
            if (ready.Status != CompanyTeamReadStatus.Ok) return new(ready.Status);
            await using var db = contextFactory(ready.Scope!.TenantId);
            var query = from member in db.CompanyMemberships.AsNoTracking()
                join user in db.LocalUsers.AsNoTracking()
                    on new { member.TenantId, Id = member.LocalUserId } equals new { user.TenantId, user.Id }
                where member.LegalEntityId == companyId && !member.IsArchived
                select new { member.Id, user.DisplayName, member.Role, member.IsActive, member.Revision,
                    UserActive = user.IsActive && !user.IsArchived && user.CredentialMode == LocalCredentialModes.Federated,
                    user.LockedUntil, HasLink = db.ExternalIdentityLinks.Any(link => link.LocalUserId == user.Id
                        && link.Issuer == actor.Issuer && link.IsActive && !link.IsArchived) };
            if (after is { } cursor)
            {
                var text = cursor.ToString("D");
                query = query.Where(x => string.Compare(x.Id.ToString().ToLower(), text) > 0);
            }
            var scanned = await query.OrderBy(x => x.Id.ToString().ToLower()).Take(21).ToArrayAsync(cancellation);
            var current = await new CoreCompanyAccess(db, clock).ReadAsync(actor.Issuer, actor.Subject,
                actor.LinkId, companyId, ready.License!, cancellation);
            if (current is null || !current.MayManageTeam) return new(CompanyTeamReadStatus.Denied);
            var page = scanned.Take(20).ToArray();
            return new(CompanyTeamReadStatus.Ok, page.Select(row => new CompanyTeamMember(row.Id, row.DisplayName,
                row.Role, row.IsActive, row.UserActive && row.HasLink
                    && (row.LockedUntil is null || row.LockedUntil <= clock.GetUtcNow()), row.Revision)).ToArray(),
                scanned.Length > 20 ? page[^1].Id.ToString("D") : null, current.Role);
        }
        catch (DbException) { return new(CompanyTeamReadStatus.Unavailable); }
    }

    internal async Task<CompanyTeamResult> ExecuteAsync(CompanyTeamActor actor, CompanyTeamCommand command,
        CancellationToken cancellation = default)
    {
        try
        {
            var ready = await Resolve(actor, command.CompanyId, cancellation);
            if (ready.Status != CompanyTeamReadStatus.Ok)
                return new(ready.Status == CompanyTeamReadStatus.Denied ? CompanyTeamStatus.Denied : CompanyTeamStatus.Unavailable);
            return await new CoreCompanyTeamCommands(contextFactory, clock).ExecuteAsync(actor, command, ready.License!, cancellation);
        }
        catch (DbException) { return new(CompanyTeamStatus.Unavailable); }
    }

    internal sealed record Ready(CompanyTeamReadStatus Status, CoreCompanyAccessDecision? Scope = null, CommercialLicenseLease? License = null);
    internal async Task<Ready> Resolve(CompanyTeamActor actor, Guid companyId, CancellationToken cancellation)
    {
        await using var db = contextFactory("untrusted-team-lookup");
        var reader = new CoreCompanyAccess(db, clock);
        var candidate = await reader.FindCandidateAsync(actor.Issuer, actor.Subject, actor.LinkId, companyId, cancellation);
        if (candidate is null || candidate.Role is not (CoreCompanyRoles.Owner or CoreCompanyRoles.Administrator))
            return new(CompanyTeamReadStatus.Denied);
        // The enrollment comes only from the verified Core company binding.
        var license = await readLicense(candidate.EnrollmentId, cancellation);
        if (license.Status != CommercialLicenseStatus.Authorized || license.Lease is null)
            return new(license.Status == CommercialLicenseStatus.Denied ? CompanyTeamReadStatus.Denied : CompanyTeamReadStatus.Unavailable);
        var scope = await reader.ReadAsync(actor.Issuer, actor.Subject, actor.LinkId, companyId, license.Lease, cancellation);
        return scope is not null && scope.MayManageTeam
            ? new(CompanyTeamReadStatus.Ok, scope, license.Lease) : new(CompanyTeamReadStatus.Denied);
    }
}

internal static class CoreCompanyTeamApiRegistration
{
    internal static IServiceCollection AddCoreCompanyTeamApi(this IServiceCollection services,
        IConfiguration config, string? reviewedConnection = null,bool runInvitationDelivery=true)
    {
        if(config.GetValue<bool>("ERP:CompanyInvitationDelivery:Enabled")
            && (!ProductCompanyInvitationsFlow.Enabled(config) || !ProductInvitationAcceptanceFlow.Enabled(config)))
            throw new InvalidOperationException("Invitation delivery requires the reviewed product/team/invitation configuration.");
        if (!ProductCompanyTeamFlow.Enabled(config)) return services;
        var connection = reviewedConnection ?? config.GetConnectionString("Hrm");
        try
        {
            if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connection).Database))
                throw new ArgumentException();
        }
        catch (ArgumentException) { throw new InvalidOperationException("Company team access requires a configured Core database."); }
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,
            pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options;
        services.AddSingleton(provider => new CoreCompanyTeamApi(
            tenant => new HrmDbContext(options, new TeamTenant(tenant)),
            provider.GetRequiredService<ProductCommercialLicense>().ReadAsync, TimeProvider.System));
        if (ProductCompanyInvitationsFlow.Enabled(config))
            services.AddSingleton(provider => new CoreCompanyInvitationsApi(
                tenant => new HrmDbContext(options, new TeamTenant(tenant)),
                provider.GetRequiredService<CoreCompanyTeamApi>(), TimeProvider.System,
                provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose)));
        if (ProductInvitationAcceptanceFlow.Enabled(config))
            services.AddSingleton(provider => new CoreCompanyInvitationAcceptanceApi(
                tenant => new HrmDbContext(options,new TeamTenant(tenant)),provider.GetRequiredService<CoreCompanyTeamApi>(),TimeProvider.System,
                provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose)));
        if(runInvitationDelivery && CoreCompanyInvitationDeliveryWorker.Enabled(config)) {
            // Validate settings eagerly. Merely setting an enabled flag must not
            // start a worker with an accidental HRM sender or plaintext SMTP.
            var mail=new SmtpCompanyInvitationMail(config,TimeProvider.System);
            services.AddSingleton<ICompanyInvitationMail>(mail);
            services.AddSingleton(provider=>new CoreCompanyInvitationMail(
                tenant=>new HrmDbContext(options,new TeamTenant(tenant)),provider.GetRequiredService<CoreCompanyTeamApi>(),
                provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose),
                provider.GetRequiredService<ICompanyInvitationMail>(),TimeProvider.System,config["ERP:ProductIdentity:ClientId"]!));
            services.AddSingleton(provider=>new CoreCompanyInvitationDeliveryWorker(
                tenant=>new HrmDbContext(options,tenant),provider.GetRequiredService<CoreCompanyInvitationMail>(),
                provider.GetRequiredService<ILogger<CoreCompanyInvitationDeliveryWorker>>()));
        }
        return services;
    }

    private sealed class TeamTenant(string tenant) : ITenantAccessor { public string GetTenantId() => tenant; }
}
