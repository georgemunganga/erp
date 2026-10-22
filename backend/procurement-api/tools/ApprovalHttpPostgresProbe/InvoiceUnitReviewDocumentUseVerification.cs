using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
sealed partial class Probe
{
    // Private storage-only authority, never registered in the application and never
    // advertised as a real module permission. Public actor/scope checks are tested by HTTP.
    private sealed record FixtureReviewPinAccess(DocumentOwner Owner, DocumentUseReference Reference, Guid Version) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference source, DocumentUseOperation op, IReadOnlyList<Guid> ids, CancellationToken ct)
        {
            if (owner!=Owner || source!=Reference || op!=DocumentUseOperation.Acquire || ids.Count!=1 || ids[0]!=Version) throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }
    private sealed record FixtureRetirementAccess(DocumentOwner Owner, Guid Document, long Revision) : IDocumentRetirementAccess
    {
        public Task RequireAsync(DocumentOwner owner,Guid id,long revision,CancellationToken ct)
        { if(owner!=Owner || id!=Document || revision!=Revision)throw new UnauthorizedAccessException();return Task.CompletedTask; }
    }
    private async Task StageFixtureReviewDocumentUse(DocumentDbContext documents, DocumentOwnerLifecycleFence fence,
        ProcurementRequestScope scope, InvoiceUnitReviewTransitions.OpeningPlan plan, CancellationToken ct)
    {
        var owner=new DocumentOwner("procurement","supplier_invoice",plan.Source.InvoiceId);
        var reference=new DocumentUseReference(plan.Source.ReviewId,"procurement","invoice_unit_review",plan.Source.ReviewId,1,DocumentUseKind.Review);
        var line=plan.Source.Lines.Single();
        var version=await documents.Versions.SingleAsync(x=>x.Id==line.DocumentVersionId,ct);
        var document=await documents.Documents.SingleAsync(x=>x.Id==version.DocumentId,ct);
        var store=await EfDocumentUsePinStore.CreateAsync(documents,scope,new FixtureReviewPinAccess(owner,reference,version.Id),fence,[owner],ct);
        await store.AcquireAsync(owner,reference,[new(owner.Module,owner.Type,owner.Id,document.Id,version.Id,document.Version,version.Version,line.DocumentSha256)],ct);
        await documents.SaveChangesAsync(ct);
    }
    private async Task VerifyReviewDocumentRetirementRace(ProcurementRequestScope scope, InvoiceUnitReviewTransitions.OpeningPlan plan)
    {
        var baseline=await UnitReviewFootprint();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writerEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator=new ProcurementApprovalTransaction(databaseConnection,scope,scope);
        var shared=coordinator.ExecuteWithInvoiceDocumentsAsync<int>(plan.Source.InvoiceId,async(db,cases,documents,fence,ct)=>
        {
            await StageFixtureReviewDocumentUse(documents,fence,scope,plan,ct);entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);
            throw new InvalidOperationException("FixtureReviewPinsRollback");
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var exclusive=coordinator.ExecuteWithInvoiceDocumentsAsync<int>(plan.Source.InvoiceId,async(db,cases,documents,fence,ct)=>
        {
            writerEntered.SetResult();
            var version=await documents.Versions.SingleAsync(x=>x.Id==plan.Source.Lines.Single().DocumentVersionId,ct);
            var document=await documents.Documents.SingleAsync(x=>x.Id==version.DocumentId,ct);
            var owner=new DocumentOwner("procurement","supplier_invoice",plan.Source.InvoiceId);
            var access=new FixtureRetirementAccess(owner,document.Id,document.Version);
            var writer=new EfDocumentRetirement(documents,scope,(_,_)=>Task.FromResult<IDocumentRetirementAccess>(access));
            await writer.RetireEnlistedAsync(owner,document.Id,document.Version,"Synthetic private race, always rolled back",fence,access,ct);
            Require(document.IsRetired,"Managed writer did not execute its actual guarded transition after rolled-back pins.");
            throw new InvalidOperationException("FixtureRetirementRollback");
        },exclusive:true);
        try { await Task.Delay(100);Require(!writerEntered.Task.IsCompleted,"Exclusive retirement passed a held shared review-owner fence."); }
        finally { release.TrySetResult(); }
        try { await shared;throw new InvalidOperationException("Shared race fixture committed."); }
        catch(InvalidOperationException e) when(e.Message=="FixtureReviewPinsRollback") { }
        try { await exclusive;throw new InvalidOperationException("Retirement race fixture committed."); }
        catch(InvalidOperationException e) when(e.Message=="FixtureRetirementRollback") { }
        Require(baseline==await UnitReviewFootprint(),"Rolled-back retention/retirement race left pins, document mutation or other effects.");
    }
}
