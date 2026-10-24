using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqCommercialOfferDocumentAccessTests
{
    [Fact]
    public async Task Retained_core_use_authorizes_empty_prelookup_then_only_the_exact_stored_version()
    {
        var principal = Guid.NewGuid();
        var company = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var ev = Guid.NewGuid();
        var bid = Guid.NewGuid();
        var version = Guid.NewGuid();
        var key = Guid.NewGuid();
        var scope = new ProcurementRequestScope();
        scope.Set("commercial-test", company, principal.ToString("D"), branch,
            null, true, "commercial-original-use");
        var offer = new PrincipalRfqCommercialOfferSubmissionRecord
        {
            TenantId = "commercial-test", LegalEntityId = company,
            BranchId = branch, SourcingEventId = ev, SupplierBidId = bid,
            DraftRevision = 2, OriginalDocumentVersionId = version,
        };
        var actor = new RfqPrincipalActor(principal.ToString("D"), null,
            "commercial-test", company, branch, new HashSet<Guid> { branch },
            false, new HashSet<string> { PrincipalRfqSealedManualOffers.CloseAction });
        IDocumentUseAccess access = new ProcurementSourcingExecutionRoutes
            .RetainedOfferUseAccess(scope, offer, actor, key);
        var owner = new DocumentOwner("procurement", "sourcing_event", ev);
        var reference = new DocumentUseReference(key, "procurement",
            "sourcing_commercial_offer", bid, 2, DocumentUseKind.Publication);
        // These are the two authorizer calls made by the real Core
        // EfDocumentUsePinStore.ReadRetainedAsync contract.
        await access.RequireAsync(owner, reference, DocumentUseOperation.Read,
            Array.Empty<Guid>(), CancellationToken.None);
        await access.RequireAsync(owner, reference, DocumentUseOperation.Read,
            [version], CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            access.RequireAsync(owner, reference, DocumentUseOperation.Read,
                [Guid.NewGuid()], CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            access.RequireAsync(owner, reference, DocumentUseOperation.Acquire,
                [version], CancellationToken.None));
    }
}
