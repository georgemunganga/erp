using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private readonly HashSet<Guid> sharedApprovalInsertedCases = [];
    private readonly HashSet<Guid> sharedApprovalInsertedDecisions = [];
    private ApprovalDbContext? sharedApprovalCore;
    private System.Data.Common.DbTransaction? sharedApprovalTransaction;
    private System.Data.Common.DbConnection? sharedApprovalConnection;
    // Coordinator-only binding: actual EF Added inserts in the same physical transaction,
    // recorded after successful SaveChanges. No xmin/top-level xid or updated-row inference.
    internal void BindSharedCoreApprovalContext(ApprovalDbContext owner)
    {
        if (sharedApprovalCore is not null || Database.CurrentTransaction is null || owner.Database.CurrentTransaction is null
            || !ReferenceEquals(Database.GetDbConnection(), owner.Database.GetDbConnection())
            || !ReferenceEquals(Database.CurrentTransaction.GetDbTransaction(), owner.Database.CurrentTransaction.GetDbTransaction()))
            throw new InvalidOperationException("SharedCoreApprovalTransactionMismatch");
        sharedApprovalCore = owner;
        sharedApprovalTransaction = Database.CurrentTransaction.GetDbTransaction();
        sharedApprovalConnection = Database.GetDbConnection();
        Guid[] pendingCases = [], pendingDecisions = [];
        owner.SavingChanges += (_, _) =>
        {
            pendingCases = owner.ChangeTracker.Entries<ApprovalCase>().Where(x => x.State == EntityState.Added).Select(x => x.Entity.Id).ToArray();
            pendingDecisions = owner.ChangeTracker.Entries<ApprovalDecision>().Where(x => x.State == EntityState.Added).Select(x => x.Entity.Id).ToArray();
        };
        owner.SavedChanges += (_, _) =>
        {
            sharedApprovalInsertedCases.UnionWith(pendingCases); sharedApprovalInsertedDecisions.UnionWith(pendingDecisions);
            pendingCases = []; pendingDecisions = [];
        };
        owner.SaveChangesFailed += (_, _) => { pendingCases = []; pendingDecisions = []; };
    }
}
