using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Reconstructs the selected P36 Draft revision against its frozen
/// opened-offer and approved-source projections. This is an input prerequisite
/// for a later, separately governed award submission, never award authority.</summary>
public static class PrincipalRfqAwardSourceQualification
{
    public sealed record SelectedLine(Guid OfferId,string OfferSha256,Guid SupplierId,
        Guid SourcingLineId,string QuantityExact,string ProposedValueExact,
        string CurrencyCode,string Rationale);

    public sealed record VerifiedSelection(Guid DraftId,Guid RevisionId,int Revision,
        Guid RoundId,Guid UnsealingId,string RecommendationSha256,
        IReadOnlyList<SelectedLine> Lines);

    public static VerifiedSelection VerifyCurrent(
        PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationDraftRevision current,
        IReadOnlyList<PrincipalRfqRecommendationDraftRevision> history,
        IReadOnlyList<PrincipalRfqRecommendationDraftLine> currentLines,
        IReadOnlyList<PrincipalRfqRecommendationDraft.OfferLine> openedOfferLines,
        IReadOnlyList<PrincipalRfqRecommendationDraft.SourceLine> approvedSourceLines,
        string currency)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(currentLines);
        ArgumentNullException.ThrowIfNull(openedOfferLines);
        ArgumentNullException.ThrowIfNull(approvedSourceLines);

        Need(head.SchemaVersion==1 && current.SchemaVersion==1
            && head.Id!=Guid.Empty && current.Id!=Guid.Empty
            && !string.IsNullOrWhiteSpace(head.TenantId)
            && head.LegalEntityId!=Guid.Empty
            && ProcurementPrincipalEvidence.Canonical(head.MakerPrincipalId)
            && head.RoundId!=Guid.Empty && head.UnsealingId!=Guid.Empty
            && head.SealId!=Guid.Empty && head.SourcingEventId!=Guid.Empty
            && head.BranchId!=Guid.Empty && head.PreparedPlanId!=Guid.Empty
            && head.EvaluationPlanRevisionId!=Guid.Empty
            && head.CurrentRevision is >0 and <=100
            && head.CurrentRevisionId==current.Id
            && head.CurrentSnapshotSha256==current.SnapshotSha256
            && Hash(head.ApprovedRoundSha256) && Hash(head.OpeningSha256)
            && Hash(head.OfferSetSha256) && Hash(head.EvaluationDefinitionSha256)
            && current.DraftId==head.Id && current.RoundId==head.RoundId
            && current.UnsealingId==head.UnsealingId
            && current.SourcingEventId==head.SourcingEventId
            && current.BranchId==head.BranchId
            && current.TenantId==head.TenantId
            && current.LegalEntityId==head.LegalEntityId
            && current.ActorPrincipalId==head.MakerPrincipalId
            && current.Revision==head.CurrentRevision
            && currentLines.Count is >0 and <=500
            && history.All(x=>x is not null)
            && currentLines.All(x=>x is not null)
            && currentLines.Select(x=>x.Id).Distinct().Count()==currentLines.Count,
            "RfqAwardRecommendationSourceChanged");

        PrincipalRfqRecommendationDraft.VerifyChain(head,current,history);
        var selections=currentLines.Select(x=>
            new PrincipalRfqRecommendationDraft.Selection(x.OfferId,x.SourcingLineId,
                x.QuantityExact,x.ProposedValueExact,x.Rationale)).ToArray();
        var proposed=PrincipalRfqRecommendationDraft.Validate(selections,
            openedOfferLines,approvedSourceLines,currency);
        Need(proposed.Count==currentLines.Count
            && currentLines.All(x=>x.DraftId==head.Id && x.RevisionId==current.Id
                && x.RoundId==head.RoundId && x.UnsealingId==head.UnsealingId
                && x.SourcingEventId==head.SourcingEventId && x.BranchId==head.BranchId
                && x.TenantId==head.TenantId && x.LegalEntityId==head.LegalEntityId)
            && currentLines.OrderBy(x=>x.SourcingLineId).ThenBy(x=>x.OfferId)
                .Zip(proposed,(line,value)=>
                    line.OfferId==value.OfferId && line.OfferSha256==value.OfferSha256
                    && line.SupplierId==value.SupplierId
                    && line.SourcingLineId==value.SourcingLineId
                    && line.QuantityExact==value.QuantityExact
                    && line.ProposedValueExact==value.ProposedValueExact
                    && line.CurrencyCode==value.CurrencyCode
                    && line.Rationale==value.Rationale).All(x=>x)
            && current.SnapshotSha256==ProcurementEvidenceEncoding.Digest(current.SnapshotJson)
            && current.SnapshotJson==PrincipalRfqRecommendationDraft.Snapshot(head,current,proposed),
            "RfqAwardRecommendationLinesChanged");

        return new(head.Id,current.Id,current.Revision,head.RoundId,head.UnsealingId,
            current.SnapshotSha256,Array.AsReadOnly(proposed.Select(x=>new SelectedLine(
                x.OfferId,x.OfferSha256,x.SupplierId,x.SourcingLineId,x.QuantityExact,
                x.ProposedValueExact,x.CurrencyCode,x.Rationale)).ToArray()));
    }

    private static bool Hash(string? value) => value is {Length:64}
        && value.All(x=>x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need(bool okay,string code)
    {
        if(!okay)throw new InvalidOperationException(code);
    }
}
