using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit successor bridge. No DI registration or new grant is supplied here.
/// Callers establish schema readiness, command permission and the owning transaction first.</summary>
internal static class ProcurementSpendingAuthorityPrincipalIdentity
{
    internal sealed record Actors(ProcurementPrincipalPeople.Person Actor, ProcurementPrincipalPeople.Person Recipient);
    internal sealed record AuthorProof(SpendingAuthorityIdentityEvidence Created, Actors People);
    internal sealed record ReviewProof(SpendingAuthorityIdentityEvidence Created,
        SpendingAuthorityIdentityEvidence Submitted, Actors People);

    internal static async Task<Actors> ResolveCreationActorsAsync(HrmDbContext core,
        ProcurementRequestScope scope, string recipientPrincipal, CancellationToken ct)
        => new(await CurrentActor(core,scope,SpendingAuthorityConfiguration.ConfigurePermission,ct),await Resolve(core,scope,recipientPrincipal,ct));

    internal static async Task<AuthorProof> RequireAuthorAsync(HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        var created=await ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(db,scope,grant,ct);
        if(created.SchemaVersion!=2)throw new InvalidOperationException("AuthorityPrincipalEvidenceRequired");
        var people=await ResolveCreationActorsAsync(core,scope,grant.PrincipalId,ct);
        SpendingAuthorityIdentity.RequireAuthor(grant,created,people.Actor.PrincipalId,people.Actor.CoreLinkedWorkerId);
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant,created,people.Recipient.PrincipalId,people.Recipient.CoreLinkedWorkerId);
        return new(created,people);
    }

    internal static async Task<ReviewProof> RequireReviewerAsync(HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        var author=await RequireCurrentPeopleAsync(core,db,scope,grant,ct);
        // Existing owning reader requires one exact pending revision, not an earlier round.
        var submitted=await ProcurementSpendingAuthorityWorkerIdentity.LoadSubmittedAsync(db,scope,grant,ct);
        SpendingAuthorityIdentity.RequireIndependentReviewer(grant,author.Created,submitted,
            author.People.Actor.PrincipalId,author.People.Actor.CoreLinkedWorkerId);
        foreach(var original in new[]
        {
            (author.Created.ActorPrincipalId,author.Created.ActorWorkerId),
            (submitted.ActorPrincipalId,submitted.ActorWorkerId),
            (author.Created.RecipientPrincipalId,author.Created.RecipientWorkerId)
        })
            if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                original.Item1,original.Item2,author.People.Actor.PrincipalId,author.People.Actor.CoreLinkedWorkerId,ct))
                throw new InvalidOperationException("AuthorityIndependentPrincipalRequired");
        return new(author.Created,submitted,author.People);
    }

    internal static async Task RequireEligibleRecipientAsync(HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant selected,
        ProcurementPrincipalPeople.Person expected,CancellationToken ct)
    {
        var proof=await ProcurementSpendingAuthorityWorkerIdentity.RequireRetainedActivationAsync(db,scope,selected,ct);
        if(proof.Created.SchemaVersion!=2)throw new InvalidOperationException("AuthorityPrincipalEvidenceRequired");
        var current=await Resolve(core,scope,expected.PrincipalId,ct);
        if(current.CoreLinkedWorkerId!=expected.CoreLinkedWorkerId
            || !current.Permissions.Contains(selected.Permission))
            throw new InvalidOperationException("AuthorityRecipientIdentityChanged");
        SpendingAuthorityIdentity.RequireCurrentRecipient(selected,proof.Created,current.PrincipalId,current.CoreLinkedWorkerId);
        if(!current.OrganisationWide && (selected.AllBranches || selected.BranchRef is not { } branch || !current.AllowedBranches.Contains(branch)))
            throw new InvalidOperationException("AuthorityRecipientScopeChanged");
    }

    private static async Task<AuthorProof> RequireCurrentPeopleAsync(HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        var created=await ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(db,scope,grant,ct);
        if(created.SchemaVersion!=2)throw new InvalidOperationException("AuthorityPrincipalEvidenceRequired");
        var people=new Actors(await CurrentActor(core,scope,SpendingAuthorityConfiguration.ReviewPermission,ct),
            await Resolve(core,scope,grant.PrincipalId,ct));
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant,created,people.Recipient.PrincipalId,people.Recipient.CoreLinkedWorkerId);
        return new(created,people);
    }

    private static async Task<ProcurementPrincipalPeople.Person> CurrentActor(HrmDbContext core,
        ProcurementRequestScope scope,string permission,CancellationToken ct)
    {
        var actor=await Resolve(core,scope,scope.SubjectId,ct);
        if(actor.CoreLinkedWorkerId!=scope.CoreLinkedWorkerId
            || !actor.Permissions.Contains(permission)
            || !actor.OrganisationWide && (scope.WorkLocationId is not { } branch || !actor.AllowedBranches.Contains(branch)))
            throw new InvalidOperationException("AuthorityCurrentActorIdentityChanged");
        return actor;
    }

    private static async Task<ProcurementPrincipalPeople.Person> Resolve(HrmDbContext core,
        ProcurementRequestScope scope,string principal,CancellationToken ct)
        => await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,ct)
            ?? throw new InvalidOperationException("AuthorityCanonicalPrincipalRequired");
}
