using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqAwardSourceQualificationTests
{
    private static readonly Guid Company=Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Round=Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Opening=Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Source=Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Offer=Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Supplier=Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly string Maker="77777777-7777-4777-8777-777777777777";

    private sealed record Fixture(PrincipalRfqRecommendationDraftHead Head,
        PrincipalRfqRecommendationDraftRevision Revision,
        PrincipalRfqRecommendationDraftLine Line,
        PrincipalRfqRecommendationDraft.OfferLine[] Opened,
        PrincipalRfqRecommendationDraft.SourceLine[] Sources);

    private static Fixture Valid()
    {
        var head=new PrincipalRfqRecommendationDraftHead
        {
            TenantId="tenant-a",LegalEntityId=Company,RoundId=Round,UnsealingId=Opening,
            SealId=Guid.CreateVersion7(),SourcingEventId=Guid.CreateVersion7(),
            BranchId=Guid.CreateVersion7(),PreparedPlanId=Guid.CreateVersion7(),
            EvaluationPlanRevisionId=Guid.CreateVersion7(),
            ApprovedRoundSha256=new string('a',64),OpeningSha256=new string('b',64),
            OfferSetSha256=new string('c',64),EvaluationDefinitionSha256=new string('d',64),
            MakerPrincipalId=Maker,CurrentRevision=1,
        };
        var revision=new PrincipalRfqRecommendationDraftRevision
        {
            TenantId=head.TenantId,LegalEntityId=Company,DraftId=head.Id,
            RoundId=Round,UnsealingId=Opening,SourcingEventId=head.SourcingEventId,
            BranchId=head.BranchId,Revision=1,ActorPrincipalId=Maker,
            CommandKey=Guid.CreateVersion7(),RequestSha256=new string('e',64),
            RecordedAt=new DateTimeOffset(2026,10,25,2,0,0,TimeSpan.Zero),
        };
        var opened=new PrincipalRfqRecommendationDraft.OfferLine[]
            {new(Offer,new string('f',64),Supplier,Source,"0.6","ZMW")};
        var sources=new PrincipalRfqRecommendationDraft.SourceLine[] {new(Source,"1")};
        var selected=new PrincipalRfqRecommendationDraft.Selection(Offer,Source,"0.5","50.00000001","Reviewed source");
        var proposal=PrincipalRfqRecommendationDraft.Validate([selected],opened,sources,"ZMW");
        revision.SnapshotJson=PrincipalRfqRecommendationDraft.Snapshot(head,revision,proposal);
        revision.SnapshotSha256=ProcurementEvidenceEncoding.Digest(revision.SnapshotJson);
        head.CurrentRevisionId=revision.Id;head.CurrentSnapshotSha256=revision.SnapshotSha256;
        var line=new PrincipalRfqRecommendationDraftLine
        {
            TenantId=head.TenantId,LegalEntityId=Company,DraftId=head.Id,
            RevisionId=revision.Id,RoundId=Round,UnsealingId=Opening,
            SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,
            OfferId=Offer,OfferSha256=new string('f',64),SupplierId=Supplier,
            SourcingLineId=Source,QuantityExact="0.5",ProposedValueExact="50.00000001",
            CurrencyCode="ZMW",Rationale="Reviewed source",
        };
        return new(head,revision,line,opened,sources);
    }

    private static PrincipalRfqAwardSourceQualification.VerifiedSelection Verify(Fixture x,
        IReadOnlyList<PrincipalRfqRecommendationDraftLine>? lines=null,
        IReadOnlyList<PrincipalRfqRecommendationDraft.OfferLine>? opened=null,
        IReadOnlyList<PrincipalRfqRecommendationDraft.SourceLine>? sources=null)
        =>PrincipalRfqAwardSourceQualification.VerifyCurrent(x.Head,x.Revision,[x.Revision],
            lines??[x.Line],opened??x.Opened,sources??x.Sources,"ZMW");

    [Fact]
    public void Exact_current_recommendation_is_a_verified_source_not_an_award()
    {
        var f=Valid();var verified=Verify(f);
        Assert.Equal(f.Revision.Id,verified.RevisionId);
        Assert.Equal(f.Revision.SnapshotSha256,verified.RecommendationSha256);
        Assert.Equal("0.5",Assert.Single(verified.Lines).QuantityExact);
        Assert.Equal("50.00000001",verified.Lines[0].ProposedValueExact);
    }

    [Fact]
    public void Missing_or_duplicate_selected_line_cannot_verify_original_snapshot()
    {
        var f=Valid();
        Assert.Throws<InvalidOperationException>(()=>Verify(f,[]));
        Assert.Throws<InvalidOperationException>(()=>Verify(f,[f.Line,f.Line]));
    }

    [Fact]
    public void Substituted_supplier_or_offer_digest_denies_even_with_same_source_and_quantity()
    {
        var f=Valid();
        Assert.Throws<InvalidOperationException>(()=>Verify(f,opened:[new(Offer,new string('e',64),
            Supplier,Source,"0.6","ZMW")]));
        Assert.Throws<InvalidOperationException>(()=>Verify(f,opened:[new(Offer,new string('f',64),
            Guid.CreateVersion7(),Source,"0.6","ZMW")]));
    }

    [Fact]
    public void Smaller_opened_or_original_authorized_quantity_denies_selection()
    {
        var f=Valid();
        Assert.Throws<InvalidOperationException>(()=>Verify(f,opened:[new(Offer,new string('f',64),
            Supplier,Source,"0.4","ZMW")]));
        Assert.Throws<InvalidOperationException>(()=>Verify(f,sources:[new(Source,"0.4")]));
    }

    [Fact]
    public void Self_hashed_substitute_snapshot_and_changed_company_deny()
    {
        var f=Valid();f.Revision.SnapshotJson="{}";
        f.Revision.SnapshotSha256=ProcurementEvidenceEncoding.Digest(f.Revision.SnapshotJson);
        f.Head.CurrentSnapshotSha256=f.Revision.SnapshotSha256;
        Assert.Throws<InvalidOperationException>(()=>Verify(f));
        var other=Valid();other.Line.LegalEntityId=Guid.CreateVersion7();
        Assert.Throws<InvalidOperationException>(()=>Verify(other));
    }
}
