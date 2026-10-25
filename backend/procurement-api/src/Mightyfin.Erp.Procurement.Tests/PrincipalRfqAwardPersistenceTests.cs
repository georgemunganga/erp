using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Source ownership checks. Genuine FK, Core transaction and migration
/// verification are separate gates; the award route remains unavailable.</summary>
public sealed class PrincipalRfqAwardPersistenceTests
{
    private const string Actor = "85ac46ab-2ed3-4f1f-a7ec-62027961b783";
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null;
        public bool IsConfined => false; public string? CorrelationId => null; }

    private static ProcurementDbContext Db(Guid company) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase("p38-" + Guid.NewGuid()).Options,
        new Scope("synthetic-award", company, Actor));

    private static (PrincipalRfqRecommendationSubmission Frozen,
        PrincipalRfqAwardReviewSubmission Review, PrincipalRfqAwardReviewLine Line,
        PrincipalRfqAwardReviewReceipt Receipt) Batch(Guid company)
    {
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        var draft = Guid.NewGuid(); var revision = Guid.NewGuid();
        var round = Guid.NewGuid(); var unsealing = Guid.NewGuid();
        var eventId = Guid.NewGuid(); var branch = Guid.NewGuid();
        var key = Guid.NewGuid(); var requestSha = new string('a', 64);
        var reviewId = Guid.NewGuid();
        var snapshot = "{\"source\":\"synthetic\"}";
        var revisionSha = ProcurementEvidenceEncoding.Digest(snapshot);
        var frozen = new PrincipalRfqRecommendationSubmission
        {
            TenantId = "synthetic-award", LegalEntityId = company,
            DraftId = draft, AwardReviewSubmissionId = reviewId,
            RevisionId = revision, Revision = 1, RevisionSha256 = revisionSha,
            RoundId = round, UnsealingId = unsealing, SourcingEventId = eventId, BranchId = branch,
            MakerPrincipalId = Actor, CommandKey = key, RequestSha256 = requestSha,
            SubmittedAt = at, CreatedAt = at, CreatedBy = Actor,
            SnapshotJson = snapshot, SnapshotSha256 = revisionSha,
        };
        var review = new PrincipalRfqAwardReviewSubmission
        {
            Id = reviewId, TenantId = frozen.TenantId, LegalEntityId = company,
            RecommendationSubmissionId = frozen.Id, DraftId = draft,
            RecommendationRevisionId = revision, RecommendationRevision = 1,
            RecommendationSha256 = revisionSha, RoundId = round, UnsealingId = unsealing,
            SourcingEventId = eventId, BranchId = branch, CoreCaseId = Guid.NewGuid(),
            PolicyVersionId = Guid.NewGuid(), PolicyKey = PrincipalRfqAwardFoundation.PolicyKey,
            PolicyVersionNumber = 1, PolicyRulesSha256 = new string('b', 64),
            MakerPrincipalId = Actor, CommandKey = key, RequestSha256 = requestSha,
            CurrencyCode = "ZMW", ProposedTotalExact = "5.00000000",
            SubmittedAt = at, CreatedAt = at, CreatedBy = Actor,
            AssignmentPinsJson = "{}", AssignmentPinsSha256 = ProcurementEvidenceEncoding.Digest("{}"),
        };
        var line = new PrincipalRfqAwardReviewLine
        {
            TenantId = frozen.TenantId, LegalEntityId = company, SubmissionId = review.Id,
            RecommendationSubmissionId = frozen.Id, RoundId = round, UnsealingId = unsealing,
            SourcingEventId = eventId, BranchId = branch, OfferId = Guid.NewGuid(),
            OfferSha256 = new string('c', 64), SupplierId = Guid.NewGuid(), SourcingLineId = Guid.NewGuid(),
            QuantityExact = "1", CurrencyCode = "ZMW", RetainedQuoteTotalExact = "5.00000000",
            CreatedAt = at, CreatedBy = Actor,
        };
        var response = PrincipalRfqAwardReviewWorkflow.ResponseFor(review);
        var receipt = new PrincipalRfqAwardReviewReceipt
        {
            TenantId = frozen.TenantId, LegalEntityId = company, SubmissionId = review.Id,
            RecommendationSubmissionId = frozen.Id, RoundId = round, SourcingEventId = eventId,
            BranchId = branch, ActorPrincipalId = Actor, CommandKey = key,
            RequestSha256 = requestSha, ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
            CreatedAt = at, CreatedBy = Actor,
        };
        return (frozen, review, line, receipt);
    }

    [Fact]
    public void Original_batch_is_atomic_and_cross_company_evidence_is_never_relabelled()
    {
        var company = Guid.NewGuid(); var (frozen, review, line, receipt) = Batch(company);
        using var db = Db(company);
        var alien = new PrincipalRfqAwardReviewReceipt { TenantId = frozen.TenantId,
            LegalEntityId = Guid.NewGuid() };
        Assert.Equal("Pinned approval evidence is outside the authenticated scope.",
            Assert.Throws<InvalidOperationException>(() => db.AddScoped(alien)).Message);
        TrackCurrentSource(db, frozen);
        db.AddScoped(frozen); db.AddScoped(review); db.AddScoped(line);
        Assert.Equal("RfqAwardSubmissionBatchIncomplete",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        Assert.Empty(db.PrincipalRfqAwardReviewSubmissions);
        db.AddScoped(receipt);
        Assert.Equal("RfqAwardSameSaveEffectsRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        AddEffects(db, review);
        db.SaveChanges();
        Assert.Single(db.PrincipalRfqRecommendationSubmissions);
        Assert.Single(db.PrincipalRfqAwardReviewReceipts);
    }

    [Fact]
    public void Substituted_source_and_immutable_update_are_denied()
    {
        var company = Guid.NewGuid(); var (frozen, review, line, receipt) = Batch(company);
        using var db = Db(company);
        TrackCurrentSource(db, frozen);
        db.AddScoped(frozen); db.AddScoped(review); db.AddScoped(line); db.AddScoped(receipt);
        AddEffects(db, review);
        db.Entry(line).Property(x => x.SourcingEventId).CurrentValue = Guid.NewGuid();
        Assert.Equal("RfqAwardLineSourceMismatch",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        db.Entry(line).Property(x => x.SourcingEventId).CurrentValue = review.SourcingEventId;
        db.SaveChanges();
        db.Entry(receipt).Property(x => x.ResponseJson).CurrentValue = "{}";
        Assert.Equal("RfqAwardEvidenceAppendOnly",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Stale_draft_cursor_cannot_be_frozen_even_with_a_self_hashed_revision()
    {
        var company = Guid.NewGuid(); var (frozen, review, line, receipt) = Batch(company);
        using var db = Db(company);
        TrackCurrentSource(db, frozen, stale: true);
        db.AddScoped(frozen); db.AddScoped(review); db.AddScoped(line); db.AddScoped(receipt);
        AddEffects(db, review);
        Assert.Equal("RfqAwardCurrentRecommendationRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        Assert.Empty(db.PrincipalRfqRecommendationSubmissions);
    }

    private static void TrackCurrentSource(ProcurementDbContext db,
        PrincipalRfqRecommendationSubmission frozen, bool stale = false)
    {
        db.Attach(new PrincipalRfqRecommendationDraftHead
        {
            Id = frozen.DraftId, TenantId = frozen.TenantId, LegalEntityId = frozen.LegalEntityId,
            RoundId = frozen.RoundId, UnsealingId = frozen.UnsealingId,
            SourcingEventId = frozen.SourcingEventId, BranchId = frozen.BranchId,
            MakerPrincipalId = frozen.MakerPrincipalId, CurrentRevision = frozen.Revision,
            CurrentRevisionId = stale ? Guid.NewGuid() : frozen.RevisionId,
            CurrentSnapshotSha256 = frozen.RevisionSha256,
        });
        db.Attach(new PrincipalRfqRecommendationDraftRevision
        {
            Id = frozen.RevisionId, TenantId = frozen.TenantId, LegalEntityId = frozen.LegalEntityId,
            DraftId = frozen.DraftId, Revision = frozen.Revision,
            RoundId = frozen.RoundId, UnsealingId = frozen.UnsealingId,
            SourcingEventId = frozen.SourcingEventId, BranchId = frozen.BranchId,
            SnapshotJson = frozen.SnapshotJson, SnapshotSha256 = frozen.SnapshotSha256,
        });
    }

    private static void AddEffects(ProcurementDbContext db, PrincipalRfqAwardReviewSubmission review)
    {
        var payload = PrincipalRfqAwardReviewEffects.Payload(review);
        Assert.DoesNotContain(review.ProposedTotalExact, payload, StringComparison.Ordinal);
        db.AddScoped(new AuditEvent { RecordType = nameof(PrincipalRfqAwardReviewSubmission),
            RecordId = review.Id, Action = PrincipalRfqAwardReviewEffects.AuditAction,
            ActorSubjectId = Actor, ChangedFieldsJson = payload });
        db.AddScoped(new OutboxEvent { EventType = PrincipalRfqAwardReviewEffects.EventType,
            AggregateType = nameof(PrincipalRfqAwardReviewSubmission), AggregateId = review.Id,
            EventVersion = 1, IdempotencyKey = review.CommandKey.ToString("D"), PayloadJson = payload });
    }

    [Fact]
    public void Physical_model_binds_source_scope_and_original_replay_key()
    {
        using var db = Db(Guid.NewGuid());
        var line = db.Model.FindEntityType(typeof(PrincipalRfqAwardReviewLine))!;
        Assert.Contains(line.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(PrincipalRfqCommercialOfferSubmissionRecord)
            && fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId",
                "OfferId", "RoundId", "SourcingEventId", "BranchId", "SupplierId", "OfferSha256" }));
        var ack = db.Model.FindEntityType(typeof(PrincipalRfqAwardReviewReceipt))!;
        Assert.Contains(ack.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(PrincipalRfqAwardReviewSubmission)
            && fk.Properties.Select(p => p.Name).Contains("CommandKey")
            && fk.Properties.Select(p => p.Name).Contains("RequestSha256"));
    }

    [Fact]
    public void Every_new_owner_family_has_short_stable_keys_indexes_and_foreign_keys()
    {
        using var db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope("synthetic-award", Guid.NewGuid(), Actor));
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(PrincipalRfqRecommendationSubmission),
            typeof(PrincipalRfqAwardReviewSubmission), typeof(PrincipalRfqAwardReviewLine),
            typeof(PrincipalRfqAwardReviewReceipt), typeof(PrincipalRfqAwardReviewOutcome) })
        {
            var entity = model.FindEntityType(type)!;
            Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(ProcurementRecord.Id))!.ValueGenerated);
            Assert.All(entity.GetKeys(), key => Assert.InRange(key.GetName()!.Length, 1, 63));
            Assert.All(entity.GetIndexes(), index => Assert.InRange(index.GetDatabaseName()!.Length, 1, 63));
            Assert.All(entity.GetForeignKeys(), foreign =>
            {
                Assert.InRange(foreign.GetConstraintName()!.Length, 1, 63);
                Assert.Equal(DeleteBehavior.Restrict, foreign.DeleteBehavior);
            });
            Assert.DoesNotContain(entity.GetKeys().SelectMany(key => key.Properties),
                property => property.Name == nameof(ProcurementRecord.Version));
        }
    }
}
