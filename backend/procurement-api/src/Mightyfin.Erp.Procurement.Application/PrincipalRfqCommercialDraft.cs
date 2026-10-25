using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalRfqCommercialDraftInput(string? EvaluationDefinitionJson,DateTimeOffset? OpensAt,
    DateTimeOffset? ClosesAt,string? ComparisonCurrency,string? CurrencyMode,string? TaxMode,
    IReadOnlyList<DocumentUseSelection> Requirements);

/// <summary>Draft controls only. Missing publication policy or document integration
/// cannot prohibit empty/incomplete Draft configuration; all workflow stays unavailable.</summary>
public static class PrincipalRfqCommercialDraft
{
    public const string Permission="procurement-sourcing-commercial-configure"; // proposed, unregistered
    private sealed record Envelope(int SchemaVersion,Guid DraftId,Guid EventId,Guid SourceEvidenceId,string TenantId,
        Guid CompanyId,Guid BranchId,string OwnerPrincipalId,Guid? OwnerWorkerAliasId,string OriginalFingerprint,
        string SourceEnvelopeSha256,long Revision,PrincipalRfqCommercialDraftInput Input);
    private static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static PrincipalRfqCommercialDraftInput ValidateInput(PrincipalRfqCommercialDraftInput input,string currency,Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.Requirements);
        if(input.EvaluationDefinitionJson is not null)_=PrincipalRfqEvaluationPlanDefinition.Parse(input.EvaluationDefinitionJson);
        if(input.ComparisonCurrency is not null&&input.ComparisonCurrency!=currency
            ||input.CurrencyMode is not(null or "SameCurrencyOnly")||input.TaxMode is not(null or "OriginalSeparateComponents")
            ||input.OpensAt is {} opens&&(opens==default||opens.Offset!=TimeSpan.Zero||opens.UtcTicks%10!=0)
            ||input.ClosesAt is {} closes&&(closes==default||closes.Offset!=TimeSpan.Zero||closes.UtcTicks%10!=0)
            ||input.OpensAt is {} from&&input.ClosesAt is {} to&&from>=to
            ||input.Requirements.Count>100||input.Requirements.Any(x=>x is null)
            ||input.Requirements.Select(x=>x.VersionId).Distinct().Count()!=input.Requirements.Count)
            throw new InvalidOperationException("RfqCommercialDraftInputInvalid");
        foreach(var selection in input.Requirements)
            if(selection.OwnerModule!="procurement"||selection.OwnerType!=PrincipalRfqPrepublicationPlan.DocumentOwnerType
                ||selection.OwnerId!=eventId||selection.DocumentId==Guid.Empty||selection.VersionId==Guid.Empty
                ||selection.DocumentRevision<1||selection.VersionRevision<1||!Hash(selection.Sha256))
                throw new InvalidOperationException("RfqCommercialDraftDocumentSelectionInvalid");
        return input with{Requirements=Array.AsReadOnly(input.Requirements.ToArray())};
    }
    public static (PrincipalRfqCommercialDraftHead Head,PrincipalRfqCommercialDraftRevision Revision) Create(
        RfqPrincipalActor actor,Guid draftId,Guid key,SourcingEvent ev,SourcingDraftEvidence evidence,
        IReadOnlyList<SourcingDemandBinding> bindings,PrincipalRfqCommercialDraftInput input)
    {
        var source=Source(actor,ev,evidence,bindings);RequireActor(actor,source);
        if(draftId==Guid.Empty||key==Guid.Empty)throw new InvalidOperationException("RfqCommercialDraftKeyRequired");
        var head=new PrincipalRfqCommercialDraftHead{Id=draftId,TenantId=source.TenantId,LegalEntityId=source.CompanyId,
            SourcingEventId=source.Id,SourceEvidenceId=evidence.Id,BranchId=source.BranchId!.Value,
            OwnerPrincipalId=source.MakerPrincipalId,OwnerWorkerAliasId=source.MakerWorkerAlias,
            OriginalFingerprint=source.OriginalFingerprint,SourceEnvelopeSha256=evidence.PayloadSha256};
        var revision=Revision(actor,head,source.CurrencyCode,input,key);head.SnapshotSha256=revision.SnapshotSha256;return(head,revision);
    }
    public static PrincipalRfqCommercialDraftRevision Revise(RfqPrincipalActor actor,PrincipalRfqCommercialDraftHead head,
        PrincipalRfqCommercialDraftRevision original,long expectedRevision,Guid key,SourcingEvent ev,SourcingDraftEvidence evidence,
        IReadOnlyList<SourcingDemandBinding> bindings,PrincipalRfqCommercialDraftInput input)
    {
        _=Restore(head,original);var source=Source(actor,ev,evidence,bindings);RequireActor(actor,source);
        if(head.Revision!=expectedRevision||head.SourcingEventId!=source.Id||head.SourceEvidenceId!=evidence.Id
            ||head.OriginalFingerprint!=source.OriginalFingerprint||head.SourceEnvelopeSha256!=evidence.PayloadSha256
            ||head.OwnerPrincipalId!=source.MakerPrincipalId||head.OwnerWorkerAliasId!=source.MakerWorkerAlias||key==Guid.Empty)
            throw new InvalidOperationException("RfqCommercialDraftVersionOrSourceChanged");
        var number=checked(head.Revision+1);var revision=Revision(actor,head,source.CurrencyCode,input,key,number);
        head.Revision=number;head.SnapshotSha256=revision.SnapshotSha256;return revision;
    }
    public static PrincipalRfqCommercialDraftInput Restore(PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row)
        =>Restore(head,row,true);
    public static PrincipalRfqCommercialDraftInput RestoreHistorical(PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,PrincipalRfqCommercialDraftReceipt receipt)
    {var restored=Restore(head,row,false);VerifyReceipt(head,row,receipt);return restored;}
    private static PrincipalRfqCommercialDraftInput Restore(PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,bool current)
    {
        ArgumentNullException.ThrowIfNull(head);ArgumentNullException.ThrowIfNull(row);
        if(row.SnapshotJson is not{Length:>0 and <=262144}||row.SnapshotSha256!=ProcurementEvidenceEncoding.Digest(row.SnapshotJson)
            ||current&&row.SnapshotSha256!=head.SnapshotSha256||ProcurementEvidenceEncoding.PolicyJsonDigest(row.SnapshotJson).Length!=64)
            throw new InvalidOperationException("RfqCommercialDraftSnapshotChanged");
        var envelope=JsonSerializer.Deserialize<Envelope>(row.SnapshotJson,Json)??throw new InvalidOperationException("RfqCommercialDraftSnapshotChanged");
        if(envelope.SchemaVersion!=1||head.SchemaVersion!=1||head.SourceIdentitySchemaVersion!=2||row.SchemaVersion!=1||envelope.DraftId!=head.Id||row.DraftId!=head.Id
            ||envelope.EventId!=head.SourcingEventId||row.SourcingEventId!=head.SourcingEventId||envelope.SourceEvidenceId!=head.SourceEvidenceId||row.SourceEvidenceId!=head.SourceEvidenceId
            ||envelope.TenantId!=head.TenantId||row.TenantId!=head.TenantId||envelope.CompanyId!=head.LegalEntityId||row.LegalEntityId!=head.LegalEntityId
            ||envelope.BranchId!=head.BranchId||row.BranchId!=head.BranchId||envelope.OwnerPrincipalId!=head.OwnerPrincipalId
            ||row.ActorPrincipalId!=head.OwnerPrincipalId||envelope.OwnerWorkerAliasId!=head.OwnerWorkerAliasId
            ||envelope.OriginalFingerprint!=head.OriginalFingerprint||envelope.SourceEnvelopeSha256!=head.SourceEnvelopeSha256
            ||envelope.Revision!=row.Revision||current&&row.Revision!=head.Revision||row.CommandKey==Guid.Empty
            ||envelope.Input is null||envelope.Input.Requirements is null)
            throw new InvalidOperationException("RfqCommercialDraftSnapshotChanged");
        // Historical read never consults today's publication policy or document availability.
        return envelope.Input with{Requirements=Array.AsReadOnly(envelope.Input.Requirements.ToArray())};
    }
    public static string Response(PrincipalRfqCommercialDraftHead head,Guid key)=>Response(head,head.Revision,head.SnapshotSha256,key);
    private static string Response(PrincipalRfqCommercialDraftHead head,long revision,string hash,Guid key)=>JsonSerializer.Serialize(new{draftId=head.Id,eventId=head.SourcingEventId,revision,status="Draft",canPublish=false,commandKey=key,snapshotSha256=hash},Json);
    public static void VerifyReceipt(PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,PrincipalRfqCommercialDraftReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(head);ArgumentNullException.ThrowIfNull(row);ArgumentNullException.ThrowIfNull(receipt);
        if(receipt.SchemaVersion!=1||receipt.DraftId!=head.Id||receipt.DraftRevisionId!=row.Id||receipt.SourcingEventId!=head.SourcingEventId
            ||receipt.SourceEvidenceId!=head.SourceEvidenceId||receipt.TenantId!=head.TenantId||receipt.LegalEntityId!=head.LegalEntityId
            ||receipt.BranchId!=head.BranchId||receipt.ActorPrincipalId!=row.ActorPrincipalId||receipt.CommandKey!=row.CommandKey
            ||receipt.SnapshotSha256!=row.SnapshotSha256||receipt.ResultRevision!=row.Revision||!Hash(receipt.RequestSha256)
            ||receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            ||receipt.ResponseJson!=Response(head,row.Revision,row.SnapshotSha256,row.CommandKey)
            ||receipt.Operation!=(row.Revision==1?"Create":"Revise")||receipt.ResponseStatus!=(row.Revision==1?201:200))
            throw new InvalidOperationException("RfqCommercialDraftOriginalReceiptChanged");
    }
    private static PrincipalRfqCommercialDraftRevision Revision(RfqPrincipalActor actor,PrincipalRfqCommercialDraftHead head,
        string currency,PrincipalRfqCommercialDraftInput input,Guid key,long? targetRevision=null)
    {
        var number=targetRevision??head.Revision;var copy=ValidateInput(input,currency,head.SourcingEventId);
        var json=JsonSerializer.Serialize(new Envelope(1,head.Id,head.SourcingEventId,head.SourceEvidenceId,head.TenantId,head.LegalEntityId,
            head.BranchId,head.OwnerPrincipalId,head.OwnerWorkerAliasId,head.OriginalFingerprint,head.SourceEnvelopeSha256,number,copy),Json);
        if(json.Length>262144)throw new InvalidOperationException("RfqCommercialDraftSnapshotBound");
        return new(){TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,DraftId=head.Id,SourcingEventId=head.SourcingEventId,
            SourceEvidenceId=head.SourceEvidenceId,BranchId=head.BranchId,Revision=number,CommandKey=key,
            ActorPrincipalId=actor.PrincipalId,ActorWorkerAliasId=actor.LinkedWorkerId,SnapshotJson=json,SnapshotSha256=ProcurementEvidenceEncoding.Digest(json)};
    }
    private static RfqPrincipalDraft Source(RfqPrincipalActor actor,SourcingEvent ev,SourcingDraftEvidence evidence,IReadOnlyList<SourcingDemandBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(actor);ArgumentNullException.ThrowIfNull(ev);ArgumentNullException.ThrowIfNull(evidence);ArgumentNullException.ThrowIfNull(bindings);
        if(ev.Status!="Draft"||ev.SourceEvidenceId!=evidence.Id||ev.BranchRef is null||actor.TenantId!=ev.TenantId||actor.CompanyId!=ev.LegalEntityId)
            throw new InvalidOperationException("RfqCommercialDraftOriginalSourceRequired");
        if(bindings.Count is <1 or >200||bindings.Any(x=>x is null||x.TargetType!="SourcingEvent"||x.IdentitySchemaVersion!=2))throw new InvalidOperationException("RfqCommercialDraftBindingsRequired");
        return GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
    }
    private static void RequireActor(RfqPrincipalActor actor,RfqPrincipalDraft draft)
    {
        if(!ProcurementPrincipalEvidence.Canonical(actor.PrincipalId)||!ProcurementPrincipalEvidence.OptionalWorker(actor.LinkedWorkerId)
            ||actor.PrincipalId!=draft.MakerPrincipalId||actor.Permissions is null||!actor.Permissions.Contains(Permission)
            ||actor.Branches is null
            ||actor.SelectedBranchId is {} selected&&selected!=draft.BranchId
            ||!actor.OrganisationWide&&!actor.Branches.Contains(draft.BranchId!.Value))throw new UnauthorizedAccessException("RfqCommercialDraftCurrentOwnerRequired");
    }
    private static bool Hash(string value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
