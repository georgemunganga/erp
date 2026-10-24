using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Only validates a human-authored Draft against the already retained
/// opened offer set. No score aggregation, winning supplier, or award decision.</summary>
public static class PrincipalRfqRecommendationDraft
{
    public const string Action = "procurement-sourcing-recommend-draft";
    public const string EventType = "sourcing.recommendation_draft_recorded.v1";
    public sealed record SourceLine(Guid SourcingLineId,string AuthorizedQuantityExact);
    public sealed record OfferLine(Guid OfferId,string OfferSha256,Guid SupplierId,
        Guid SourcingLineId,string OfferedQuantityExact,string CurrencyCode);
    public sealed record Selection(Guid OfferId,Guid SourcingLineId,string QuantityExact,
        string ProposedValueExact,string Rationale);
    public sealed record Proposed(Guid OfferId,string OfferSha256,Guid SupplierId,
        Guid SourcingLineId,string QuantityExact,string ProposedValueExact,
        string CurrencyCode,string Rationale);

    public static IReadOnlyList<Proposed> Validate(IReadOnlyList<Selection> selections,
        IReadOnlyList<OfferLine> opened,IReadOnlyList<SourceLine> sources,string currency)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(sources);
        Need(selections.Count <= 500 && opened.Count <= 500 && sources.Count is >0 and <=500
            && currency is {Length:3} && currency.All(x=>x is >= 'A' and <= 'Z'),
            "RfqRecommendationDraftSizeOrCurrencyInvalid");
        Need(opened.Select(x=>(x.OfferId,x.SourcingLineId)).Distinct().Count()==opened.Count
            && sources.Select(x=>x.SourcingLineId).Distinct().Count()==sources.Count,
            "RfqRecommendationSourceDuplicate");
        var offers=opened.ToDictionary(x=>(x.OfferId,x.SourcingLineId));
        var authorized=sources.ToDictionary(x=>x.SourcingLineId,
            x=>Units(x.AuthorizedQuantityExact,4,14));
        Need(selections.Select(x=>(x.OfferId,x.SourcingLineId)).Distinct().Count()==selections.Count,
            "RfqRecommendationDuplicateOfferLine");
        var totals=new Dictionary<Guid,BigInteger>();
        var values=new List<Proposed>();
        foreach(var selection in selections)
        {
            Need(offers.TryGetValue((selection.OfferId,selection.SourcingLineId),out var offered)
                && offered is not null && offered.SupplierId!=Guid.Empty
                && offered.OfferSha256 is {Length:64}
                && offered.OfferSha256.All(IsLowerHex)
                && offered.CurrencyCode==currency
                && authorized.ContainsKey(selection.SourcingLineId),
                "RfqRecommendationOfferOutsideOpenedSource");
            var quantity=Units(selection.QuantityExact,4,14);
            var value=Units(selection.ProposedValueExact,8,30);
            Need(quantity>0 && value>0 && quantity<=Units(offered.OfferedQuantityExact,4,14)
                && selection.Rationale is {Length:>0 and <=2000}
                && selection.Rationale==selection.Rationale.Trim(),
                "RfqRecommendationLineInvalid");
            totals[selection.SourcingLineId]=totals.GetValueOrDefault(selection.SourcingLineId)+quantity;
            Need(totals[selection.SourcingLineId]<=authorized[selection.SourcingLineId],
                "RfqRecommendationSourceQuantityExceeded");
            values.Add(new(offered.OfferId,offered.OfferSha256,offered.SupplierId,
                offered.SourcingLineId,Canonical(quantity,4),Canonical(value,8),
                currency,selection.Rationale));
        }
        return values.OrderBy(x=>x.SourcingLineId).ThenBy(x=>x.OfferId).ToArray();
    }

    public static string Snapshot(PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationDraftRevision revision,IReadOnlyList<Proposed> lines)
        =>JsonSerializer.Serialize(new {schemaVersion=1,draftId=head.Id,roundId=head.RoundId,
            unsealingId=head.UnsealingId,sealId=head.SealId,eventId=head.SourcingEventId,
            branchId=head.BranchId,approvedRoundSha256=head.ApprovedRoundSha256,
            openingSha256=head.OpeningSha256,offerSetSha256=head.OfferSetSha256,
            preparedPlanId=head.PreparedPlanId,
            evaluationPlanRevisionId=head.EvaluationPlanRevisionId,
            evaluationDefinitionSha256=head.EvaluationDefinitionSha256,
            makerPrincipalId=head.MakerPrincipalId,revisionId=revision.Id,
            revision=revision.Revision,previousRevisionId=revision.PreviousRevisionId,
            previousSnapshotSha256=revision.PreviousSnapshotSha256,
            actorPrincipalId=revision.ActorPrincipalId,recordedAt=revision.RecordedAt,
            lines=lines.Select(x=>new{x.OfferId,x.OfferSha256,x.SupplierId,
                x.SourcingLineId,x.QuantityExact,x.ProposedValueExact,x.CurrencyCode,x.Rationale})});

    public static string Response(PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationDraftRevision revision)
        =>JsonSerializer.Serialize(new {draftId=head.Id,roundId=head.RoundId,
            revisionId=revision.Id,revision=revision.Revision,state="Draft",
            snapshotSha256=revision.SnapshotSha256,
            awardCreatedByThisCommand=false,purchaseOrderCreatedByThisCommand=false});

    public static string AuditPayload(PrincipalRfqRecommendationDraftRevision revision)
        =>JsonSerializer.Serialize(new {revision.DraftId,revision.Id,revision.Revision,
            revision.RoundId,revision.SnapshotSha256});

    public static string EventPayload(PrincipalRfqRecommendationDraftRevision revision)
        =>JsonSerializer.Serialize(new {draftId=revision.DraftId,
            revisionId=revision.Id,revision=revision.Revision,
            roundId=revision.RoundId,commandKey=revision.CommandKey,
            awardCreatedByThisCommand=false,purchaseOrderCreatedByThisCommand=false});

    public static void VerifyChain(PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationDraftRevision current,
        IReadOnlyList<PrincipalRfqRecommendationDraftRevision> history)
    {
        Need(head.CurrentRevision==current.Revision && head.CurrentRevisionId==current.Id
            && head.CurrentSnapshotSha256==current.SnapshotSha256
            && history.Count==current.Revision
            && history.Select(x=>x.Revision).Order().SequenceEqual(Enumerable.Range(1,current.Revision))
            && history.All(x=>x.DraftId==head.Id && x.RoundId==head.RoundId
                && x.UnsealingId==head.UnsealingId && x.SourcingEventId==head.SourcingEventId
                && x.BranchId==head.BranchId && x.SnapshotSha256==ProcurementEvidenceEncoding.Digest(x.SnapshotJson)),
            "RfqRecommendationHistoryChanged");
        var ordered=history.OrderBy(x=>x.Revision).ToArray();
        for(var i=0;i<ordered.Length;i++)
            Need(i==0 ? ordered[i].PreviousRevisionId is null && ordered[i].PreviousSnapshotSha256 is null
                : ordered[i].PreviousRevisionId==ordered[i-1].Id
                    && ordered[i].PreviousSnapshotSha256==ordered[i-1].SnapshotSha256,
                "RfqRecommendationHistoryChainChanged");
    }

    private static BigInteger Units(string text,int scale,int integerDigits)
    {
        Need(text is {Length:>0 and <=40} && text[0]!='-' && text[0]!='+'
            && text.Count(x=>x=='.')<=1 && text.All(x=>x is >= '0' and <= '9' or '.')
            && text[0]!='.' && text[^1]!='.',"RfqRecommendationExactDecimalInvalid");
        var parts=text.Split('.');
        Need(parts[0].Length is >0 && <=30 && parts[0].Length<=integerDigits
            && (parts[0].Length==1 || parts[0][0]!='0')
            && (parts.Length==1 || parts[1].Length is >0 && <=8 && parts[1].Length<=scale),
            "RfqRecommendationExactDecimalInvalid");
        return BigInteger.Parse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture)
            *BigInteger.Pow(10,scale)
            +(parts.Length==1?BigInteger.Zero:
                BigInteger.Parse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture)
                *BigInteger.Pow(10,scale-parts[1].Length));
    }
    private static string Canonical(BigInteger units,int scale)
    {
        var factor=BigInteger.Pow(10,scale);var whole=units/factor;
        var fractional=(units%factor).ToString(CultureInfo.InvariantCulture).PadLeft(scale,'0').TrimEnd('0');
        return fractional.Length==0?whole.ToString(CultureInfo.InvariantCulture)
            :whole.ToString(CultureInfo.InvariantCulture)+"."+fractional;
    }
    private static bool IsLowerHex(char c)=>c is >= '0' and <= '9' or >= 'a' and <= 'f';
    private static void Need(bool okay,string code){if(!okay)throw new InvalidOperationException(code);}
}
