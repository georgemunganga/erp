using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    // Called only after the same-transaction Core insert/Save was witnessed by
    // BindSharedCoreApprovalContext. A later unsaved change to the tracked Core
    // graph must not turn a persisted CAS version 2 into claimed version 3.
    internal static (ApprovalCase? Case, ApprovalDecision? Decision,
        ApprovalAssignment? Assignment) CommittedCommercialVote(
        ApprovalDbContext core, Guid caseId, Guid decisionId, Guid assignmentId)
    {
        core.ChangeTracker.DetectChanges();
        if (core.ChangeTracker.HasChanges())
            throw new InvalidOperationException("RfqCommercialCoreVoteNotCommitted");
        var decision = core.Decisions.AsNoTracking()
            .SingleOrDefault(x => x.Id == decisionId);
        var approvalCase = core.Cases.AsNoTracking()
            .Include(x => x.Stages).ThenInclude(x => x.Assignments)
            .SingleOrDefault(x => x.Id == caseId);
        var assignment = approvalCase?.Stages.SelectMany(x => x.Assignments)
            .SingleOrDefault(x => x.Id == assignmentId);
        return (approvalCase, decision, assignment);
    }
}
