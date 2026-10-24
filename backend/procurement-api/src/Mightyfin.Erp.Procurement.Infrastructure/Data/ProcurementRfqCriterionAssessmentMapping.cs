using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqCriterionAssessmentRecord> PrincipalRfqCriterionAssessments => Set<PrincipalRfqCriterionAssessmentRecord>();
    public DbSet<PrincipalRfqCriterionAssessmentReceipt> PrincipalRfqCriterionAssessmentReceipts => Set<PrincipalRfqCriterionAssessmentReceipt>();

    private void ConfigureRfqCriterionAssessment(ModelBuilder model)
    {
        ConfigureP32Base<PrincipalRfqCriterionAssessmentRecord>(model,"principal_rfq_criterion_assessments","assess");
        ConfigureP32Base<PrincipalRfqCriterionAssessmentReceipt>(model,"principal_rfq_criterion_assessment_receipts","assess_ack");
        var row=model.Entity<PrincipalRfqCriterionAssessmentRecord>();
        var ack=model.Entity<PrincipalRfqCriterionAssessmentReceipt>();
        row.Property(x=>x.Id).ValueGeneratedNever();ack.Property(x=>x.Id).ValueGeneratedNever();
        row.Property(x=>x.Score).HasPrecision(18,4);
        row.Property(x=>x.CriterionCode).HasMaxLength(64);row.Property(x=>x.Kind).HasMaxLength(20);
        row.Property(x=>x.EvaluatorPrincipalId).HasMaxLength(36);ack.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        row.Property(x=>x.Rationale).HasMaxLength(2000);row.Property(x=>x.SnapshotJson).HasColumnType("text");
        ack.Property(x=>x.ResponseJson).HasColumnType("text");
        foreach(var name in new[]{"EvaluationDefinitionSha256","ApprovedRoundSha256","OpeningSha256","OfferSha256","OriginalDocumentSha256","RequestSha256","SnapshotSha256"})
            row.Property<string>(name).HasMaxLength(64);
        ack.Property(x=>x.RequestSha256).HasMaxLength(64);ack.Property(x=>x.ResponseSha256).HasMaxLength(64);
        model.Entity<PrincipalRfqCommercialRoundOpeningRecord>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.PreparedPlanId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .HasName("ak_p32_assess_round");
        row.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.RoundId,x.PreparedPlanId,x.EventId,x.BranchId,x.ApprovedRoundSha256})
            .HasPrincipalKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.PreparedPlanId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_round");
        model.Entity<PrincipalRfqPreparedPlan>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.SourcingEventId,x.BranchId,x.DraftRevisionId,x.EvaluationRulesSha256})
            .HasName("ak_p32_assess_plan");
        row.HasOne<PrincipalRfqPreparedPlan>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.PreparedPlanId,x.EventId,x.BranchId,x.EvaluationPlanRevisionId,x.EvaluationDefinitionSha256})
            .HasPrincipalKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.SourcingEventId,x.BranchId,x.DraftRevisionId,x.EvaluationRulesSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_plan");
        model.Entity<PrincipalRfqOfferOpeningRecord>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .HasName("ak_p32_assess_opened");
        row.HasOne<PrincipalRfqOfferOpeningRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.UnsealingId,x.RoundId,x.EventId,x.BranchId,x.OpeningSha256})
            .HasPrincipalKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_opened");
        model.Entity<PrincipalRfqCommercialOfferSubmissionRecord>().HasAlternateKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .HasName("ak_p32_assess_offer");
        row.HasOne<PrincipalRfqCommercialOfferSubmissionRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.OfferId,x.RoundId,x.EventId,x.BranchId,x.OfferSha256})
            .HasPrincipalKey(x=>new
            {x.TenantId,x.LegalEntityId,x.Id,x.OpeningId,x.SourcingEventId,x.BranchId,x.SnapshotSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_offer");
        row.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.UnsealingId,
            x.OfferId,x.EventId,x.BranchId,x.EvaluatorPrincipalId,x.CommandKey,x.RequestSha256})
            .HasName("ak_p32_assess_ack");
        ack.HasOne<PrincipalRfqCriterionAssessmentRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.AssessmentId,x.RoundId,x.UnsealingId,x.OfferId,
                x.EventId,x.BranchId,x.ActorPrincipalId,x.CommandKey,x.RequestSha256})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.UnsealingId,
                x.OfferId,x.EventId,x.BranchId,x.EvaluatorPrincipalId,x.CommandKey,x.RequestSha256})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_ack");
        row.HasAlternateKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.OfferId,
            x.CriterionCode,x.EvaluatorPrincipalId,x.Revision}).HasName("ak_p32_assess_prev");
        row.HasOne<PrincipalRfqCriterionAssessmentRecord>().WithMany().HasForeignKey(x=>new
            {x.TenantId,x.LegalEntityId,x.PreviousAssessmentId,x.RoundId,x.OfferId,
                x.CriterionCode,x.EvaluatorPrincipalId,x.PreviousRevision})
            .HasPrincipalKey(x=>new {x.TenantId,x.LegalEntityId,x.Id,x.RoundId,x.OfferId,
                x.CriterionCode,x.EvaluatorPrincipalId,x.Revision})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_assess_prev");
        row.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.RoundId,x.OfferId,x.CriterionCode,
            x.EvaluatorPrincipalId,x.Revision}).IsUnique().HasDatabaseName("ux_p32_assess_revision");
        row.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique().HasDatabaseName("ux_p32_assess_key");
        ack.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique().HasDatabaseName("ux_p32_assess_ack_key");
        ack.HasIndex(x=>new {x.TenantId,x.LegalEntityId,x.AssessmentId}).IsUnique().HasDatabaseName("ux_p32_assess_ack_target");
        const string sha=" ~ '^[a-f0-9]{64}$'";
        const string principal=" ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string nz=" <> '00000000-0000-0000-0000-000000000000'::uuid";
        row.ToTable(t=>t.HasCheckConstraint("ck_p32_assess", "schema_version=1 AND id"+nz
            +" AND round_id"+nz+" AND unsealing_id"+nz+" AND offer_id"+nz+" AND event_id"+nz
            +" AND branch_id"+nz+" AND prepared_plan_id"+nz+" AND evaluation_plan_revision_id"+nz
            +" AND original_document_version_id"+nz+" AND command_key"+nz
            +" AND evaluator_principal_id IS NOT NULL AND evaluator_principal_id"+principal
            +" AND evaluator_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND (evaluator_worker_alias_id IS NULL OR evaluator_worker_alias_id"+nz+")"
            +" AND revision BETWEEN 1 AND 100 AND ((revision=1 AND previous_assessment_id IS NULL AND previous_revision IS NULL)"
            +" OR (revision>1 AND previous_assessment_id IS NOT NULL AND previous_revision IS NOT NULL AND previous_revision=revision-1))"
            +" AND criterion_code IS NOT NULL AND length(criterion_code)>0"
            +" AND kind IS NOT NULL"
            +" AND rationale IS NOT NULL AND length(btrim(rationale))>0"
            +" AND ((kind='Scored' AND score IS NOT NULL AND score>=0 AND passed IS NULL)"
            +" OR (kind='PassFail' AND score IS NULL AND passed IS NOT NULL))"
            +" AND evaluation_definition_sha256 IS NOT NULL AND evaluation_definition_sha256"+sha
            +" AND approved_round_sha256 IS NOT NULL AND approved_round_sha256"+sha
            +" AND opening_sha256 IS NOT NULL AND opening_sha256"+sha
            +" AND offer_sha256 IS NOT NULL AND offer_sha256"+sha
            +" AND original_document_sha256 IS NOT NULL AND original_document_sha256"+sha
            +" AND request_sha256 IS NOT NULL AND request_sha256"+sha
            +" AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+sha
            +" AND snapshot_json IS NOT NULL AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex')"
            +" AND recorded_at IS NOT NULL AND isfinite(recorded_at)"));
        ack.ToTable(t=>t.HasCheckConstraint("ck_p32_assess_ack", "schema_version=1 AND id"+nz
            +" AND assessment_id"+nz+" AND round_id"+nz+" AND unsealing_id"+nz+" AND offer_id"+nz
            +" AND event_id"+nz+" AND branch_id"+nz+" AND command_key"+nz
            +" AND actor_principal_id IS NOT NULL AND actor_principal_id"+principal
            +" AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND request_sha256 IS NOT NULL AND request_sha256"+sha
            +" AND response_sha256 IS NOT NULL AND response_sha256"+sha
            +" AND response_json IS NOT NULL AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')"));
    }

    private void GuardRfqCriterionAssessment()
    {
        var entries=ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is
            PrincipalRfqCriterionAssessmentRecord or PrincipalRfqCriterionAssessmentReceipt).ToArray();
        if(entries.All(x=>x.State is EntityState.Unchanged or EntityState.Detached))
        {
            if(ChangeTracker.Entries<OutboxEvent>().Any(x=>x.State==EntityState.Added
                    && x.Entity.EventType==PrincipalRfqCriterionAssessmentStorage.EventType)
                || ChangeTracker.Entries<AuditEvent>().Any(x=>x.State==EntityState.Added
                    && x.Entity.RecordType==nameof(PrincipalRfqCriterionAssessmentRecord)))
                throw new InvalidOperationException("RfqAssessmentSameSaveEffectsRequired");
            return;
        }
        if(Database.IsNpgsql() && Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("RfqAssessmentOwnerTransactionRequired");
        if(entries.Any(x=>x.State!=EntityState.Added || x.Entity.TenantId!=scope.TenantId
            || x.Entity.LegalEntityId!=scope.LegalEntityId))
            throw new InvalidOperationException("RfqAssessmentAppendOnlyScopeRequired");
        var rows=entries.Select(x=>x.Entity).OfType<PrincipalRfqCriterionAssessmentRecord>().ToArray();
        var acks=entries.Select(x=>x.Entity).OfType<PrincipalRfqCriterionAssessmentReceipt>().ToArray();
        var audits=ChangeTracker.Entries<AuditEvent>().Where(x=>x.State==EntityState.Added
            && x.Entity.RecordType==nameof(PrincipalRfqCriterionAssessmentRecord))
            .Select(x=>x.Entity).ToArray();
        var events=ChangeTracker.Entries<OutboxEvent>().Where(x=>x.State==EntityState.Added
            && x.Entity.EventType==PrincipalRfqCriterionAssessmentStorage.EventType)
            .Select(x=>x.Entity).ToArray();
        if(rows.Length!=acks.Length)throw new InvalidOperationException("RfqAssessmentSameSaveReceiptRequired");
        if(audits.Length!=rows.Length || events.Length!=rows.Length)
            throw new InvalidOperationException("RfqAssessmentSameSaveEffectsRequired");
        foreach(var row in rows)
        {
            if(row.EvaluatorPrincipalId!=scope.SubjectId || row.EvaluatorWorkerAliasId!=scope.CoreLinkedWorkerId
                || acks.Count(x=>x.AssessmentId==row.Id)!=1)
                throw new InvalidOperationException("RfqAssessmentActorChanged");
            if(audits.Count(x=>x.RecordId==row.Id && x.RecordType==nameof(PrincipalRfqCriterionAssessmentRecord)
                    && x.Action=="RecordCriterionAssessment" && x.ActorSubjectId==scope.SubjectId
                    && x.ChangedFieldsJson==PrincipalRfqCriterionAssessmentStorage.AuditPayload(row))!=1
                || events.Count(x=>x.AggregateType==nameof(PrincipalRfqCriterionAssessmentRecord)
                    && x.AggregateId==row.Id && x.EventVersion==1
                    && x.IdempotencyKey==row.CommandKey.ToString("D")
                    && x.PayloadJson==PrincipalRfqCriterionAssessmentStorage.EventPayload(row)
                    && x.State=="Pending" && x.Attempts==0 && x.PublishedAt is null
                    && x.NextAttemptAt is null)!=1)
                throw new InvalidOperationException("RfqAssessmentSameSaveEffectsRequired");
            var round=PrincipalRfqCommercialRoundOpenings.AsNoTracking().Single(x=>x.Id==row.RoundId);
            var opened=PrincipalRfqOfferOpenings.AsNoTracking().Single(x=>x.Id==row.UnsealingId);
            var offer=PrincipalRfqCommercialOfferSubmissions.AsNoTracking().Single(x=>x.Id==row.OfferId);
            var plan=PrincipalRfqPreparedPlans.AsNoTracking().Single(x=>x.Id==row.PreparedPlanId);
            var revision=PrincipalRfqCommercialDraftRevisions.AsNoTracking().Single(x=>x.Id==row.EvaluationPlanRevisionId);
            var head=PrincipalRfqCommercialDrafts.AsNoTracking().Single(x=>x.Id==revision.DraftId);
            var draftReceipt=PrincipalRfqCommercialDraftReceipts.AsNoTracking()
                .Single(x=>x.DraftRevisionId==revision.Id);
            var input=PrincipalRfqCommercialDraft.RestoreHistorical(head,revision,draftReceipt);
            PrincipalRfqPreparedPlanEvidence.Verify(plan,head,revision);
            if(plan.DraftRevisionId!=revision.Id || plan.EvaluationRulesSha256!=row.EvaluationDefinitionSha256
                || input.EvaluationDefinitionJson is null)
                throw new InvalidOperationException("RfqAssessmentFrozenPlanChanged");
            var definition=PrincipalRfqEvaluationPlanDefinition.Parse(input.EvaluationDefinitionJson);
            PrincipalRfqCriterionAssessmentStorage.Verify(row,round,opened,offer,definition);
            if(row.Revision>1)
            {
                var prior=PrincipalRfqCriterionAssessments.AsNoTracking().Single(x=>x.Id==row.PreviousAssessmentId);
                if(prior.RoundId!=row.RoundId || prior.OfferId!=row.OfferId
                    || prior.CriterionCode!=row.CriterionCode || prior.EvaluatorPrincipalId!=row.EvaluatorPrincipalId
                    || prior.Revision!=row.PreviousRevision)
                    throw new InvalidOperationException("RfqAssessmentPreviousRevisionChanged");
            }
            PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row,acks.Single(x=>x.AssessmentId==row.Id));
        }
    }
}
