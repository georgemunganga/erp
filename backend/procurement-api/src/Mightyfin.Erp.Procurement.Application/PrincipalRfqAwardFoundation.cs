using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure, default-off prerequisites for a future P38 award submission.
/// The owning adapter must load every argument from retained evidence under one
/// transaction and independently establish a submitted P36 revision and P29
/// held capacity before it may create a Core case. No route calls this class.</summary>
public static class PrincipalRfqAwardFoundation
{
    public const string PolicyKey = "SOURCING_SUPPLIER_AWARD";
    public const string SubmitAction = "procurement-sourcing-award-submit";
    public const string ReviewAction = "procurement-sourcing-award-review";
    public const string ReadAction = "procurement-sourcing-award-read";

    public sealed record Stage(string Code, string Mode, IReadOnlyList<string> PrincipalIds);
    public sealed record Rules(string CurrencyCode, string MaximumAmountExact,
        IReadOnlyList<Stage> Stages);
    public sealed record CurrentPerson(string PrincipalId, Guid? WorkerAliasId,
        bool OrganisationWide, IReadOnlySet<Guid> AllowedBranches,
        IReadOnlySet<string> Actions);
    public sealed record ExcludedParticipant(string PrincipalId, Guid? WorkerAliasId);
    public sealed record RetainedLine(Guid OfferId, string OfferSha256,
        Guid SupplierId, Guid SourcingLineId, string OfferedQuantityExact,
        string RemainingHeldQuantityExact, string CurrencyCode,
        string RetainedQuoteTotalExact);
    public sealed record StoredReceipt(Guid SubmissionId, string TenantId,
        Guid CompanyId, Guid BranchId, string ActorPrincipalId,
        Guid CommandKey, string RequestSha256, string ResponseJson,
        string ResponseSha256);

    public static string CommandBody(
        PrincipalRfqAwardSourceQualification.VerifiedSelection selection,
        Guid policyVersionId, Guid reviewBranchId)
        =>JsonSerializer.Serialize(new {schemaVersion=1,draftId=selection.DraftId,
            recommendationRevisionId=selection.RevisionId,
            recommendationSha256=selection.RecommendationSha256,
            expectedRevision=selection.Revision,policyVersionId,reviewBranchId});

    /// <summary>Only an explicitly published, exact policy document admits the
    /// full-offer-line candidate mode. Partial quantity pricing, discounts,
    /// negotiated terms and inferred approval limits have no default.</summary>
    public static Rules ParseRules(string? json)
    {
        Need(!string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json)<=65536,
            "RfqAwardPolicyInvalid");
        using var parsed=JsonDocument.Parse(json!,new JsonDocumentOptions {MaxDepth=16});
        _=ProcurementEvidenceEncoding.PolicyJsonDigest(json!);
        var root=parsed.RootElement;
        Only(root,"schemaVersion","transactionType","allocationMode","currencyCode",
            "maximumAmountExact","stages");
        Need(root.GetProperty("schemaVersion").GetInt32()==1
            && root.GetProperty("transactionType").GetString()=="SupplierAward"
            && root.GetProperty("allocationMode").GetString()=="FullOfferLineOnly",
            "RfqAwardPolicyInvalid");
        var currency=root.GetProperty("currencyCode").GetString();
        var ceiling=root.GetProperty("maximumAmountExact").GetString();
        Need(currency is {Length:3} && currency.All(x=>x is >= 'A' and <= 'Z')
            && Money(ceiling)>0,"RfqAwardPolicyInvalid");
        var rows=root.GetProperty("stages");
        Need(rows.ValueKind==JsonValueKind.Array && rows.GetArrayLength() is >0 and <=16,
            "RfqAwardPolicyInvalid");
        var seen=new HashSet<string>(StringComparer.Ordinal);
        var codes=new HashSet<string>(StringComparer.Ordinal);
        var stages=new List<Stage>();
        foreach(var row in rows.EnumerateArray())
        {
            Only(row,"code","mode","principalIds");
            var code=row.GetProperty("code").GetString();
            var mode=row.GetProperty("mode").GetString();
            var members=row.GetProperty("principalIds");
            Need(code is {Length:>0 and <=40} && code[0] is >= 'A' and <= 'Z'
                && code.All(x=>x is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                && codes.Add(code!) && mode is "One" or "All"
                && members.ValueKind==JsonValueKind.Array
                && members.GetArrayLength() is >0 and <=32,"RfqAwardPolicyInvalid");
            var ids=members.EnumerateArray().Select(x=>x.GetString()).ToArray();
            Need(ids.All(x=>ProcurementPrincipalEvidence.Canonical(x))
                && ids.All(x=>seen.Add(x!)) && seen.Count<=128,
                "RfqAwardPolicyInvalid");
            stages.Add(new(code!,mode!,Array.AsReadOnly(ids.Select(x=>x!).Order(StringComparer.Ordinal).ToArray())));
        }
        return new(currency!,Canonical(Money(ceiling),8),stages.AsReadOnly());
    }

    public static PrincipalRfqAwardCandidate Qualify(
        PrincipalRfqAwardSourceQualification.VerifiedSelection selection,
        IReadOnlyList<RetainedLine> retainedLines, PolicyVersion policy,
        CurrentPerson maker, IReadOnlyList<CurrentPerson> reviewers,
        IReadOnlyList<ExcludedParticipant> disqualifiedParticipants,
        Guid submissionId, string tenantId, Guid companyId, Guid branchId,
        Guid commandKey, string requestJson, string requestSha256, int expectedRevision,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(retainedLines);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(maker);
        ArgumentNullException.ThrowIfNull(reviewers);
        ArgumentNullException.ThrowIfNull(disqualifiedParticipants);
        Need(submissionId!=Guid.Empty && !string.IsNullOrWhiteSpace(tenantId) && companyId!=Guid.Empty
            && branchId!=Guid.Empty && commandKey!=Guid.Empty && Hash(requestSha256)
            && selection.DraftId!=Guid.Empty && selection.RevisionId!=Guid.Empty
            && selection.RoundId!=Guid.Empty && selection.UnsealingId!=Guid.Empty
            && Hash(selection.RecommendationSha256)
            && expectedRevision==selection.Revision && expectedRevision>0
            && requestJson==CommandBody(selection,policy.Id,branchId)
            && requestSha256==ProcurementEvidenceEncoding.Digest(requestJson)
            && now!=default && policy.TenantId==tenantId
            && policy.LegalEntityId==companyId && policy.PolicyKey==PolicyKey
            && policy.State=="Published" && policy.PolicyVersionNumber>0
            && policy.EffectiveFrom<=now
            && (policy.EffectiveTo is null || policy.EffectiveTo>now),
            "RfqAwardSourceOrPolicyUnavailable");
        var rules=ParseRules(policy.RulesJson);
        var principals=rules.Stages.SelectMany(x=>x.PrincipalIds).ToHashSet(StringComparer.Ordinal);
        Need(ProcurementPrincipalEvidence.Canonical(maker.PrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(maker.WorkerAliasId)
            && maker.Actions.Contains("procurement-access")
            && maker.Actions.Contains(SubmitAction)
            && Allowed(maker,branchId)
            && reviewers.Count==principals.Count
            && reviewers.Select(x=>x.PrincipalId).ToHashSet(StringComparer.Ordinal).SetEquals(principals),
            "RfqAwardCurrentAuthorityUnavailable");
        foreach(var person in reviewers)
            Need(ProcurementPrincipalEvidence.Canonical(person.PrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(person.WorkerAliasId)
                && person.Actions.Contains("procurement-access")
                && person.Actions.Contains(ReviewAction)
                && Allowed(person,branchId)
                && Independent(maker,person),"RfqAwardReviewerUnavailable");
        for(var i=0;i<reviewers.Count;i++)
            for(var j=i+1;j<reviewers.Count;j++)
                Need(Independent(reviewers[i],reviewers[j]),"RfqAwardReviewersNotIndependent");
        Need(disqualifiedParticipants.Count<=128
            && disqualifiedParticipants.All(x=>x is not null
                && ProcurementPrincipalEvidence.Canonical(x.PrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(x.WorkerAliasId))
            && disqualifiedParticipants.All(x=>
                ProcurementPrincipalEvidence.Independent(maker.PrincipalId,maker.WorkerAliasId,
                    x.PrincipalId,x.WorkerAliasId)
                && reviewers.All(person=>
                    ProcurementPrincipalEvidence.Independent(person.PrincipalId,person.WorkerAliasId,
                        x.PrincipalId,x.WorkerAliasId))),
            "RfqAwardRetainedParticipantConflict");
        Need(selection.Lines.Count is >0 and <=500 && retainedLines.Count is >0 and <=500
            && selection.Lines.All(x=>x is not null) && retainedLines.All(x=>x is not null)
            && selection.Lines.Select(x=>(x.OfferId,x.SourcingLineId)).Distinct().Count()==selection.Lines.Count
            && retainedLines.Select(x=>(x.OfferId,x.SourcingLineId)).Distinct().Count()==retainedLines.Count,
            "RfqAwardLineSetInvalid");
        var retained=retainedLines.ToDictionary(x=>(x.OfferId,x.SourcingLineId));
        var totals=new Dictionary<Guid,BigInteger>();
        var remainingBySource=new Dictionary<Guid,BigInteger>();
        var lines=new List<PrincipalRfqAwardCandidateLine>();
        BigInteger amount=0;
        foreach(var selected in selection.Lines)
        {
            Need(retained.TryGetValue((selected.OfferId,selected.SourcingLineId),out var source)
                && source is not null && source.OfferSha256==selected.OfferSha256
                && source.SupplierId==selected.SupplierId && source.SupplierId!=Guid.Empty
                && source.CurrencyCode==selected.CurrencyCode
                && source.CurrencyCode==rules.CurrencyCode,
                "RfqAwardOfferOutsideRetainedSource");
            var selectedQuantity=Quantity(selected.QuantityExact);
            var offeredQuantity=Quantity(source.OfferedQuantityExact);
            var remaining=Quantity(source.RemainingHeldQuantityExact);
            if(remainingBySource.TryGetValue(selected.SourcingLineId,out var priorRemaining))
                Need(priorRemaining==remaining,"RfqAwardHeldCapacityChanged");
            else remainingBySource.Add(selected.SourcingLineId,remaining);
            Need(selectedQuantity>0 && selectedQuantity==offeredQuantity
                && selectedQuantity<=remaining,
                "RfqAwardFullLineOrHeldQuantityRequired");
            totals[selected.SourcingLineId]=totals.GetValueOrDefault(selected.SourcingLineId)+selectedQuantity;
            Need(totals[selected.SourcingLineId]<=remaining,
                "RfqAwardSourceOverallocated");
            var quoteTotal=Money(source.RetainedQuoteTotalExact);
            Need(quoteTotal>0,"RfqAwardRetainedAmountInvalid");
            amount+=quoteTotal;
            Need(amount<=Money(rules.MaximumAmountExact),"RfqAwardAuthorityExceeded");
            lines.Add(new(selected.OfferId,selected.OfferSha256,selected.SupplierId,
                selected.SourcingLineId,Canonical(selectedQuantity,4),source.CurrencyCode,
                Canonical(quoteTotal,8)));
        }
        return new(submissionId,tenantId,companyId,
            selection.DraftId,selection.RevisionId,selection.RecommendationSha256,
            selection.RoundId,selection.UnsealingId,policy.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            maker.PrincipalId,maker.WorkerAliasId,branchId,commandKey,requestSha256,
            expectedRevision,rules.CurrencyCode,Canonical(amount,8),
            Array.AsReadOnly(lines.OrderBy(x=>x.SourcingLineId).ThenBy(x=>x.OfferId).ToArray()));
    }

    /// <summary>Pure original-key recovery check. The host still must load this
    /// receipt by its exact actor/key/scope under current Core authorization.
    /// It returns the original stored bytes and never reconstructs a response.</summary>
    public static string RecoverOriginal(PrincipalRfqAwardCandidate candidate,
        StoredReceipt receipt, CurrentPerson currentActor,
        string tenantId, Guid companyId, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(currentActor);
        Need(receipt.SubmissionId==candidate.SubmissionId
            && receipt.TenantId==candidate.TenantId && tenantId==candidate.TenantId
            && receipt.CompanyId==candidate.CompanyId && companyId==candidate.CompanyId
            && receipt.TenantId==tenantId
            && receipt.CompanyId==companyId && receipt.BranchId==branchId
            && branchId==candidate.ReviewBranchId
            && receipt.ActorPrincipalId==candidate.MakerPrincipalId
            && currentActor.PrincipalId==receipt.ActorPrincipalId
            && currentActor.Actions.Contains("procurement-access")
            && currentActor.Actions.Contains(SubmitAction)
            && Allowed(currentActor,branchId)
            && receipt.CommandKey==candidate.CommandKey
            && receipt.RequestSha256==candidate.RequestSha256
            && receipt.ResponseJson is {Length:>0 and <=65536}
            && Hash(receipt.ResponseSha256)
            && ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)==receipt.ResponseSha256,
            "RfqAwardOriginalReceiptUnavailable");
        return receipt.ResponseJson;
    }

    private static bool Allowed(CurrentPerson person,Guid branch)
        => person.OrganisationWide || person.AllowedBranches?.Contains(branch)==true;
    private static bool Independent(CurrentPerson a,CurrentPerson b)
        => ProcurementPrincipalEvidence.Independent(a.PrincipalId,a.WorkerAliasId,
            b.PrincipalId,b.WorkerAliasId);
    private static bool Hash(string? text)=>text is {Length:64}
        && text.All(x=>x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Only(JsonElement row,params string[] keys)
        =>Need(row.ValueKind==JsonValueKind.Object
            && row.EnumerateObject().Count()==keys.Length
            && row.EnumerateObject().All(x=>keys.Contains(x.Name,StringComparer.Ordinal)),
            "RfqAwardPolicyInvalid");
    private static BigInteger Quantity(string? text)=>Units(text,4,14);
    private static BigInteger Money(string? text)=>Units(text,8,30);
    private static BigInteger Units(string? text,int scale,int integerDigits)
    {
        Need(text is {Length:>0 and <=40} && text[0]!='-' && text[0]!='+'
            && text[0]!='.' && text[^1]!='.' && text.Count(x=>x=='.')<=1
            && text.All(x=>x is >= '0' and <= '9' or '.'),"RfqAwardExactDecimalInvalid");
        var parts=text.Split('.');
        Need(parts[0].Length<=integerDigits
            && (parts[0].Length==1 || parts[0][0]!='0')
            && (parts.Length==1 || parts[1].Length is >0 and <=8 && parts[1].Length<=scale),
            "RfqAwardExactDecimalInvalid");
        return BigInteger.Parse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture)
            *BigInteger.Pow(10,scale)
            +(parts.Length==1?BigInteger.Zero:
                BigInteger.Parse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture)
                *BigInteger.Pow(10,scale-parts[1].Length));
    }
    private static string Canonical(BigInteger units,int scale)
    {
        var factor=BigInteger.Pow(10,scale);
        var whole=units/factor;
        var fraction=(units%factor).ToString(CultureInfo.InvariantCulture).PadLeft(scale,'0').TrimEnd('0');
        return fraction.Length==0?whole.ToString(CultureInfo.InvariantCulture)
            :whole.ToString(CultureInfo.InvariantCulture)+"."+fraction;
    }
    private static void Need(bool okay,string code)
    { if(!okay)throw new InvalidOperationException(code); }
}
