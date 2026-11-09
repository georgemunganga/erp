using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

// Server-side binding material for the session registry. Never accept this object
// or a tenant/principal/grant snapshot from browser JSON or request headers.
internal sealed record CoreErpSessionBinding
{
    internal int PolicyVersion { get; init; } = 1;
    internal string Subject { get; init; }
    internal string ClientId { get; init; }
    internal string TenantId { get; init; }
    internal Guid PrincipalId { get; init; }
    internal Guid LinkId { get; init; }
    internal Guid CompanyId { get; init; }
    internal string Module { get; init; }
    internal Guid GrantVersionId { get; init; }
    internal int GrantRevision { get; init; }
    internal bool AllCompanyLocations { get; init; }
    internal IReadOnlyList<Guid> LocationIds { get; init; }

    internal CoreErpSessionBinding(string subject, string client, CompanyModuleAccessDecision grant,
        Guid link, bool allCompany, Guid[] locations)
    {
        Subject = subject; ClientId = client; TenantId = grant.TenantId; PrincipalId = grant.PrincipalId;
        LinkId = link; CompanyId = grant.CompanyId; Module = grant.Module;
        GrantVersionId = grant.VersionId; GrantRevision = grant.Revision;
        AllCompanyLocations = allCompany; LocationIds = Array.AsReadOnly(locations.ToArray());
    }
    public override string ToString() => nameof(CoreErpSessionBinding);
}

internal sealed record CoreErpSessionSelection(Guid CompanyId, Guid LinkId, string Module,
    Guid ExpectedGrantVersionId, int ExpectedGrantRevision, bool AllCompanyLocations,
    Guid[] LocationIds, bool ConfirmCompanyWide);
internal sealed record CoreErpScopeRequest(Guid CompanyId, string Module, string Action,
    bool AllCompanyLocations, Guid[] LocationIds);
internal sealed record CoreErpSessionSelectionResult(ModuleAccessStatus Status, CoreErpSessionBinding? Binding = null);
internal sealed record CoreErpScopeResult(ModuleAccessStatus Status, CoreErpRequestScope? Scope = null);

/// <summary>Fresh authority for exactly this request, with no tenant or branch widening.</summary>
internal sealed class CoreErpRequestScope(CompanyModuleAccessDecision grant, bool allCompany, Guid[] locations)
{
    internal string TenantId => grant.TenantId;
    internal Guid CompanyId => grant.CompanyId;
    internal Guid PrincipalId => grant.PrincipalId;
    internal string Module => grant.Module;
    internal bool AllCompanyLocations { get; } = allCompany;
    internal IReadOnlyList<Guid> LocationIds { get; } = Array.AsReadOnly(locations.ToArray());
    internal IReadOnlyList<string> Roles => grant.Roles;
    internal bool IsCurrent => grant.IsCurrent;
    internal bool AllowsAction(string action) => grant.AllowsAction(action);
    public override string ToString() => nameof(CoreErpRequestScope);
}

/// <summary>
/// Bind an explicit selection to the current grant revision; revalidate current
/// person, license, membership and owned branches for each operational request.
/// This layer creates no cookie, session row, principal or legacy role assignment.
/// </summary>
internal sealed class CoreErpSessionScopeAuthority(HrmDbContext db, TimeProvider clock)
{
    internal async Task<CoreErpSessionSelectionResult> SelectAsync(OperationalSubjectObservation person,
        CommercialLicenseLease license, CoreErpSessionSelection selection, CancellationToken ct = default)
    {
        var locations = selection.LocationIds?.ToArray();
        if (selection.CompanyId == Guid.Empty || selection.LinkId == Guid.Empty
            || selection.Module is not ("hrm" or "procurement") || selection.ExpectedGrantVersionId == Guid.Empty
            || selection.ExpectedGrantRevision < 1 || !Locations(locations, selection.AllCompanyLocations)
            || selection.ConfirmCompanyWide != selection.AllCompanyLocations)
            return new(ModuleAccessStatus.Invalid);
        Array.Sort(locations!);
        try
        {
            var current = await Read(person, license, selection.LinkId, selection.CompanyId, selection.Module, ct);
            if (current.Status != ModuleAccessStatus.Authorized || current.Decision is not { } grant)
                return new(current.Status);
            if (grant.VersionId != selection.ExpectedGrantVersionId || grant.Revision != selection.ExpectedGrantRevision)
                return new(ModuleAccessStatus.Conflict);
            if (!await WithinGrant(grant, selection.AllCompanyLocations, locations!, ct)) return new(ModuleAccessStatus.Denied);
            var final = await Read(person, license, selection.LinkId, selection.CompanyId, selection.Module, ct);
            if (!SameGrant(grant, final.Decision)) return new(FinalStatus(final));
            if (!await WithinGrant(final.Decision!, selection.AllCompanyLocations, locations!, ct) || !final.Decision!.IsCurrent) return new(ModuleAccessStatus.Denied);
            return new(ModuleAccessStatus.Authorized, new(person.Subject, person.ClientId, grant,
                selection.LinkId, selection.AllCompanyLocations, locations!));
        }
        catch (DbException) { return new(ModuleAccessStatus.Unavailable); }
    }

    internal async Task<CoreErpScopeResult> AuthorizeAsync(CoreErpSessionBinding binding,
        OperationalSubjectObservation person, CommercialLicenseLease license, CoreErpScopeRequest request,
        CancellationToken ct = default)
    {
        var boundLocations = binding.LocationIds?.ToArray();
        var locations = request.LocationIds?.ToArray();
        // These comparisons are before database discovery: a cookie/binding for
        // one person, client, company or module cannot be used as a directory.
        if (binding.PolicyVersion != 1 || binding.Subject != person.Subject || binding.ClientId != person.ClientId
            || binding.CompanyId == Guid.Empty || binding.LinkId == Guid.Empty || binding.PrincipalId == Guid.Empty
            || string.IsNullOrWhiteSpace(binding.TenantId) || binding.GrantVersionId == Guid.Empty || binding.GrantRevision < 1
            || binding.Module is not ("hrm" or "procurement") || request.CompanyId != binding.CompanyId
            || request.Module != binding.Module || !CoreModuleAccessCatalog.Actions(binding.Module).Contains(request.Action, StringComparer.Ordinal)
            || !Locations(boundLocations, binding.AllCompanyLocations)
            || !Locations(locations, request.AllCompanyLocations)
            || request.AllCompanyLocations && !binding.AllCompanyLocations
            || !binding.AllCompanyLocations && locations!.Except(boundLocations!).Any())
            return new(ModuleAccessStatus.Denied);
        Array.Sort(locations!);
        try
        {
            var current = await Read(person, license, binding.LinkId, binding.CompanyId, binding.Module, ct);
            if (current.Status != ModuleAccessStatus.Authorized || current.Decision is not { } grant) return new(current.Status);
            if (grant.TenantId != binding.TenantId || grant.PrincipalId != binding.PrincipalId
                || grant.VersionId != binding.GrantVersionId || grant.Revision != binding.GrantRevision
                || !grant.AllowsAction(request.Action)
                || !await WithinGrant(grant, binding.AllCompanyLocations, boundLocations!, ct))
                return new(ModuleAccessStatus.Denied);
            if (!await WithinGrant(grant, request.AllCompanyLocations, locations!, ct)) return new(ModuleAccessStatus.Denied);
            var final = await Read(person, license, binding.LinkId, binding.CompanyId, binding.Module, ct);
            if (!SameGrant(grant, final.Decision)) return new(FinalStatus(final));
            if (!await WithinGrant(final.Decision!, binding.AllCompanyLocations, boundLocations!, ct)
                || !await WithinGrant(final.Decision!, request.AllCompanyLocations, locations!, ct) || !final.Decision!.IsCurrent) return new(ModuleAccessStatus.Denied);
            return new(ModuleAccessStatus.Authorized, new(final.Decision!, request.AllCompanyLocations, locations!));
        }
        catch (DbException) { return new(ModuleAccessStatus.Unavailable); }
    }

    private Task<ModuleAccessReadResult> Read(OperationalSubjectObservation person, CommercialLicenseLease license,
        Guid link, Guid company, string module, CancellationToken ct) => new CoreModuleAccessReader(db, clock).ReadAsync(person, link, company, module, license, ct);
    private static bool Locations(IReadOnlyList<Guid>? locations, bool allCompany) => locations is not null
        && (allCompany ? locations.Count == 0 : locations.Count is > 0 and <= 128)
        && !locations.Contains(Guid.Empty) && locations.Distinct().Count() == locations.Count;
    private async Task<bool> WithinGrant(CompanyModuleAccessDecision grant, bool allCompany, IReadOnlyList<Guid> locations, CancellationToken ct)
    {
        if (!grant.IsCurrent || allCompany && !grant.AllCompanyLocations
            || !grant.AllCompanyLocations && locations.Except(grant.LocationIds).Any()) return false;
        if (allCompany) return true;
        var selected = locations.ToArray();
        return await db.WorkLocations.IgnoreQueryFilters().AsNoTracking().CountAsync(x => x.TenantId == grant.TenantId
            && x.LegalEntityId == grant.CompanyId && !x.IsArchived && selected.Contains(x.Id), ct) == selected.Length;
    }
    private static bool SameGrant(CompanyModuleAccessDecision original, CompanyModuleAccessDecision? final) => original.IsCurrent
        && final is { IsCurrent: true } && original.TenantId == final.TenantId && original.PrincipalId == final.PrincipalId
        && original.CompanyId == final.CompanyId && original.Module == final.Module && original.VersionId == final.VersionId
        && original.Revision == final.Revision;
    private static ModuleAccessStatus FinalStatus(ModuleAccessReadResult result) => result.Status == ModuleAccessStatus.Unavailable
        ? ModuleAccessStatus.Unavailable : ModuleAccessStatus.Denied;
}
