using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqRecommendationDraftHead> PrincipalRfqRecommendationDrafts => Set<PrincipalRfqRecommendationDraftHead>();
    public DbSet<PrincipalRfqRecommendationDraftRevision> PrincipalRfqRecommendationDraftRevisions => Set<PrincipalRfqRecommendationDraftRevision>();
    public DbSet<PrincipalRfqRecommendationDraftLine> PrincipalRfqRecommendationDraftLines => Set<PrincipalRfqRecommendationDraftLine>();
    public DbSet<PrincipalRfqRecommendationDraftReceipt> PrincipalRfqRecommendationDraftReceipts => Set<PrincipalRfqRecommendationDraftReceipt>();

    private void ConfigureRfqRecommendationDraft(ModelBuilder model)
    {
        ConfigureP32Base<PrincipalRfqRecommendationDraftHead>(model,"principal_rfq_recommendation_drafts","rec_draft");
        ConfigureP32Base<PrincipalRfqRecommendationDraftRevision>(model,"principal_rfq_recommendation_revisions","rec_rev");
        ConfigureP32Base<PrincipalRfqRecommendationDraftLine>(model,"principal_rfq_recommendation_lines","rec_line");
        ConfigureP32Base<PrincipalRfqRecommendationDraftReceipt>(model,"principal_rfq_recommendation_receipts","rec_ack");
        var head=model.Entity<PrincipalRfqRecommendationDraftHead>();
        var revision=model.Entity<PrincipalRfqRecommendationDraftRevision>();
        var line=model.Entity<PrincipalRfqRecommendationDraftLine>();
        var ack=model.Entity<PrincipalRfqRecommendationDraftReceipt>();
        head.Property(x=>x.Id).ValueGeneratedNever();revision.Property(x=>x.Id).ValueGeneratedNever();
        line.Property(x=>x.Id).ValueGeneratedNever();ack.Property(x=>x.Id).ValueGeneratedNever();
        head.Property(x=>x.MakerPrincipalId).HasMaxLength(36);
        revision.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        ack.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        line.Property(x=>x.QuantityExact).HasMaxLength(20);
        line.Property(x=>x.ProposedValueExact).HasMaxLength(40);
        line.Property(x=>x.CurrencyCode).HasMaxLength(3);
        line.Property(x=>x.Rationale).HasMaxLength(2000);
        revision.Property(x=>x.SnapshotJson).HasColumnType("text");
        ack.Property(x=>x.ResponseJson).HasColumnType("text");
        foreach(var name in new[]{nameof(PrincipalRfqRecommendationDraftHead.ApprovedRoundSha256),
            nameof(PrincipalRfqRecommendationDraftHead.OpeningSha256),
            nameof(PrincipalRfqRecommendationDraftHead.OfferSetSha256),
            nameof(PrincipalRfqRecommendationDraftHead.EvaluationDefinitionSha256),
            nameof(PrincipalRfqRecommendationDraftHead.CurrentSnapshotSha256)})
            head.Property<string>(name).HasMaxLength(64);
        foreach(var name in new[]{nameof(PrincipalRfqRecommendationDraftRevision.PreviousSnapshotSha256),
            nameof(PrincipalRfqRecommendationDraftRevision.RequestSha256),
            nameof(PrincipalRfqRecommendationDraftRevision.SnapshotSha256)})
            revision.Property<string>(name).HasMaxLength(64);
        line.Property(x=>x.OfferSha256).HasMaxLength(64);
        ack.Property(x=>x.RequestSha256).HasMaxLength(64);ack.Property(x=>x.ResponseSha256).HasMaxLength(64);

        // These P32 keys already pin actual retained round/opened offer evidence.
        head.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.RoundId,x.PreparedPlanId,x.SourcingEventId,x.BranchId,x.ApprovedRoundSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.PreparedPlanId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_round");
        head.HasOne<PrincipalRfqPreparedPlan>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.PreparedPlanId,x.SourcingEventId,x.BranchId,x.EvaluationPlanRevisionId,x.EvaluationDefinitionSha256})
            .HasPrincipalKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.SourcingEventId,x.BranchId,x.DraftRevisionId,x.EvaluationRulesSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_plan");
        head.HasOne<PrincipalRfqOfferOpeningRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.UnsealingId,x.RoundId,x.SourcingEventId,x.BranchId,x.OpeningSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_opened");
        model.Entity<PrincipalRfqCommercialRoundSealRecord>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SubmissionSetSha256})
            .HasName("ak_p36_rec_seal");
        head.HasOne<PrincipalRfqCommercialRoundSealRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.SealId,x.RoundId,x.SourcingEventId,x.BranchId,x.OfferSetSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SubmissionSetSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_seal");

        head.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.SourcingEventId,x.BranchId})
            .HasName("ak_p36_rec_head");
        revision.HasOne<PrincipalRfqRecommendationDraftHead>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.DraftId,x.RoundId,x.SourcingEventId,x.BranchId})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.SourcingEventId,x.BranchId})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_revision_head");
        revision.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.RoundId,
            x.UnsealingId,x.SourcingEventId,x.BranchId,x.ActorPrincipalId,x.CommandKey,x.RequestSha256})
            .HasName("ak_p36_rec_revision");
        revision.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.RoundId,
            x.UnsealingId,x.SourcingEventId,x.BranchId}).HasName("ak_p36_rec_line_rev");
        revision.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.SnapshotSha256})
            .HasName("ak_p36_rec_prev");
        revision.HasOne<PrincipalRfqRecommendationDraftRevision>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.PreviousRevisionId,x.DraftId,x.PreviousSnapshotSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_prev");
        line.HasOne<PrincipalRfqRecommendationDraftRevision>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.RevisionId,x.DraftId,x.RoundId,x.UnsealingId,x.SourcingEventId,x.BranchId})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.RoundId,x.UnsealingId,x.SourcingEventId,x.BranchId})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_line_revision");
        model.Entity<PrincipalRfqCommercialOfferSubmissionRecord>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SupplierId,x.SnapshotSha256})
            .HasName("ak_p36_rec_offer");
        line.HasOne<PrincipalRfqCommercialOfferSubmissionRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.OfferId,x.RoundId,x.SourcingEventId,x.BranchId,x.SupplierId,x.OfferSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SupplierId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_line_offer");
        ack.HasOne<PrincipalRfqRecommendationDraftRevision>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.RevisionId,x.DraftId,x.RoundId,x.SourcingEventId,x.BranchId,
                x.ActorPrincipalId,x.CommandKey,x.RequestSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.RoundId,x.SourcingEventId,x.BranchId,
                x.ActorPrincipalId,x.CommandKey,x.RequestSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p36_rec_ack_revision");
        head.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.RoundId,x.MakerPrincipalId})
            .IsUnique().HasDatabaseName("ux_p36_rec_round_maker");
        revision.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.DraftId,x.Revision})
            .IsUnique().HasDatabaseName("ux_p36_rec_revision_no");
        line.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.RevisionId,x.OfferId,x.SourcingLineId})
            .IsUnique().HasDatabaseName("ux_p36_rec_line_choice");
        ack.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.ActorPrincipalId,x.CommandKey})
            .IsUnique().HasDatabaseName("ux_p36_rec_actor_key");
        const string digest=" ~ '^[a-f0-9]{64}$'";
        const string nonzero=" <> '00000000-0000-0000-0000-000000000000'::uuid";
        const string principal=" ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string exact4=" ~ '^(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$'";
        const string exact8=" ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'";
        head.ToTable(t=>t.HasCheckConstraint("ck_p36_rec_head",
            "schema_version=1 AND id"+nonzero+" AND round_id"+nonzero
            +" AND unsealing_id"+nonzero+" AND seal_id"+nonzero
            +" AND sourcing_event_id"+nonzero+" AND branch_id"+nonzero
            +" AND prepared_plan_id"+nonzero+" AND evaluation_plan_revision_id"+nonzero
            +" AND current_revision_id"+nonzero
            +" AND maker_principal_id IS NOT NULL AND maker_principal_id"+principal
            +" AND maker_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id"+nonzero+")"
            +" AND current_revision BETWEEN 1 AND 100 AND approved_round_sha256"+digest
            +" AND opening_sha256"+digest+" AND offer_set_sha256"+digest
            +" AND evaluation_definition_sha256"+digest+" AND current_snapshot_sha256"+digest));
        revision.ToTable(t=>t.HasCheckConstraint("ck_p36_rec_revision",
            "schema_version=1 AND id"+nonzero+" AND draft_id"+nonzero
            +" AND round_id"+nonzero+" AND unsealing_id"+nonzero
            +" AND sourcing_event_id"+nonzero+" AND branch_id"+nonzero
            +" AND command_key"+nonzero
            +" AND actor_principal_id IS NOT NULL AND actor_principal_id"+principal
            +" AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nonzero+")"
            +" AND revision BETWEEN 1 AND 100 AND request_sha256"+digest
            +" AND snapshot_sha256"+digest+" AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex')"
            +" AND ((revision=1 AND previous_revision_id IS NULL AND previous_snapshot_sha256 IS NULL)"
            +" OR (revision>1 AND previous_revision_id IS NOT NULL AND previous_snapshot_sha256"+digest+"))"));
        line.ToTable(t=>t.HasCheckConstraint("ck_p36_rec_line",
            "id"+nonzero+" AND draft_id"+nonzero+" AND revision_id"+nonzero
            +" AND offer_id"+nonzero+" AND supplier_id"+nonzero
            +" AND sourcing_line_id"+nonzero
            +" AND quantity_exact IS NOT NULL AND quantity_exact"+exact4
            +" AND quantity_exact !~ '^0(\\.0+)?$'"
            +" AND proposed_value_exact IS NOT NULL AND proposed_value_exact"+exact8
            +" AND proposed_value_exact !~ '^0(\\.0+)?$'"
            +" AND offer_sha256"+digest+" AND currency_code ~ '^[A-Z]{3}$'"
            +" AND rationale IS NOT NULL AND length(btrim(rationale))>0"));
        ack.ToTable(t=>t.HasCheckConstraint("ck_p36_rec_ack",
            "id"+nonzero+" AND draft_id"+nonzero+" AND revision_id"+nonzero
            +" AND round_id"+nonzero+" AND command_key"+nonzero
            +" AND actor_principal_id IS NOT NULL AND actor_principal_id"+principal
            +" AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND request_sha256"+digest+" AND response_sha256"+digest
            +" AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')"));
    }
}
