using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Load the original publication window from retained policy, identity,
    /// and audit rows in the same owner snapshot. This supplies historical evidence
    /// only; it cannot select a policy for a new comparison.</summary>
    private async Task<IReadOnlyDictionary<Guid, ScopedHistoricalPolicyWitness>>
        ReadHistoricalMatchingWindowsAsync(IReadOnlyList<InvoiceMatchResult> results,
            IReadOnlyList<PolicyVersion> policies, CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable || results.Count > 200)
            throw new InvalidOperationException("InvoiceHistoricalPolicyOwnerTransactionRequired");
        var publications = new Dictionary<Guid, ScopedHistoricalPolicyPublication>();
        async Task<ScopedHistoricalPolicyPublication> Publication(PolicyVersion policy)
        {
            if (publications.TryGetValue(policy.Id, out var cached)) return cached;
            if (policy.TenantId != scope.TenantId || policy.LegalEntityId != scope.LegalEntityId
                || policy.PolicyKey != InvoiceMatchingPolicyRules.PolicyKey)
                throw new InvalidOperationException("InvoiceHistoricalPolicyScopeChanged");
            // A policy may have been returned and resubmitted before publication.
            // The Published identity pins the one Submitted identity it accepted.
            var endpoints = await OperationalPolicyIdentityRecords.AsNoTracking()
                .Where(x => x.PolicyVersionId == policy.Id
                    && (x.Stage == "Created" || x.Stage == "Published"))
                .Take(3).ToArrayAsync(ct);
            if (endpoints.Length != 2 || endpoints.Count(x => x.Stage == "Created") != 1
                || endpoints.Count(x => x.Stage == "Published") != 1)
                throw new InvalidOperationException("InvoiceHistoricalPolicyIdentitySetChanged");
            var published = OperationalPolicyIdentityStorage.Read(endpoints.Single(x => x.Stage == "Published"));
            if (published.SubmittedEvidenceId is not { } submittedId)
                throw new InvalidOperationException("InvoiceHistoricalPolicySubmittedIdentityMissing");
            var submittedRows = await OperationalPolicyIdentityRecords.AsNoTracking()
                .Where(x => x.Id == submittedId && x.PolicyVersionId == policy.Id
                    && x.Stage == "Submitted").Take(2).ToArrayAsync(ct);
            if (submittedRows.Length != 1)
                throw new InvalidOperationException("InvoiceHistoricalPolicySubmittedIdentityMissing");
            var selected = endpoints.Append(submittedRows[0]).ToArray();
            if (selected.Any(x => x.TenantId != scope.TenantId
                    || x.LegalEntityId != scope.LegalEntityId || x.PolicyKey != policy.PolicyKey
                    || x.PolicyVersionNumber != policy.PolicyVersionNumber))
                throw new InvalidOperationException("InvoiceHistoricalPolicyIdentityScopeChanged");
            var audits = await AuditEvents.AsNoTracking().Where(x => x.RecordType == "PolicyVersion"
                && x.RecordId == policy.Id && x.Action == "Published").Take(2).ToArrayAsync(ct);
            var publication = new ScopedHistoricalPolicyPublication(policy,
                OperationalPolicyIdentityStorage.Read(endpoints.Single(x => x.Stage == "Created")),
                OperationalPolicyIdentityStorage.Read(submittedRows[0]), published, audits);
            publications.Add(policy.Id, publication);
            return publication;
        }
        var result = new Dictionary<Guid, ScopedHistoricalPolicyWitness>();
        foreach (var comparison in results)
        {
            if (comparison.PolicyVersionId is not { } policyId || comparison.Id == Guid.Empty)
                throw new InvalidOperationException("InvoiceHistoricalPolicyResultMissing");
            var policy = policies.Single(x => x.Id == policyId);
            var original = await Publication(policy);
            var superseded = await AuditEvents.AsNoTracking().Where(x =>
                x.RecordType == "PolicyVersion" && x.RecordId == policyId
                && x.Action == "Superseded").Take(2).ToArrayAsync(ct);
            ScopedHistoricalPolicyPublication? successor = null;
            if (superseded.Length != 0)
            {
                if (policy.EffectiveTo is not { } end)
                    throw new InvalidOperationException("InvoiceHistoricalPolicySupersessionEndMissing");
                var next = await PolicyVersions.AsNoTracking().Where(x =>
                    x.PolicyKey == policy.PolicyKey && x.Id != policy.Id
                    && (x.State == "Published" || x.State == "Superseded")
                    && x.EffectiveFrom == end).Take(2).ToArrayAsync(ct);
                if (next.Length != 1)
                    throw new InvalidOperationException("InvoiceHistoricalPolicySupersedingVersionMissing");
                successor = await Publication(next[0]);
            }
            result.Add(comparison.Id, InvoiceHistoricalPolicyWindow.Verify(original,
                comparison.MatchedAt, superseded, successor));
        }
        return result;
    }
}
