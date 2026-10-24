using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqCommercialRoundStorageTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "fixture";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId { get; init; }
        public Guid? OrgUnitId => null;
        public bool IsConfined => WorkLocationId is not null;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Principal_round_uses_stable_named_scope_keys_and_exact_source_owners()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only_not_opened;Username=none;Password=none")
            .Options, new Scope());
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(PrincipalRfqCommercialRoundOpeningRecord),
                     typeof(PrincipalRfqCommercialRoundDecisionRecord),
                     typeof(PrincipalRfqCommercialOfferSubmissionRecord),
                     typeof(PrincipalRfqCommercialRoundSealRecord),
                     typeof(PrincipalRfqCommercialCommandReceipt) })
        {
            var entity = model.FindEntityType(type)!;
            Assert.All(entity.GetKeys(), key => Assert.True(key.GetName()!.Length <= 63));
            Assert.All(entity.GetIndexes(), index => Assert.True(index.GetDatabaseName()!.Length <= 63));
            Assert.All(entity.GetForeignKeys(), fk => Assert.True(fk.GetConstraintName()!.Length <= 63));
        }
        var opening = model.FindEntityType(typeof(PrincipalRfqCommercialRoundOpeningRecord))!;
        Assert.Contains(opening.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(PrincipalRfqPreparedPlan)
            && fk.Properties.Select(x => x.Name).Contains(nameof(PrincipalRfqCommercialRoundOpeningRecord.PreparedPlanSha256)));
        Assert.Contains(opening.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(OperationalPolicyIdentityRecord)
            && fk.Properties.Select(x => x.Name).Contains(nameof(PrincipalRfqCommercialRoundOpeningRecord.PolicyRulesSha256)));
        var offer = model.FindEntityType(typeof(PrincipalRfqCommercialOfferSubmissionRecord))!;
        Assert.Contains(offer.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SupplierBid)
            && fk.Properties.Select(x => x.Name).Contains(nameof(PrincipalRfqCommercialOfferSubmissionRecord.SupplierId)));
        Assert.Contains(offer.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SourcingInvitation)
            && fk.Properties.Select(x => x.Name).Contains(nameof(PrincipalRfqCommercialOfferSubmissionRecord.SupplierId)));
        var retainedBid = model.FindEntityType(typeof(SupplierBid))!;
        var nullableExact = retainedBid.GetIndexes().Single(x =>
            x.GetDatabaseName() == "ux_p32_bid_exact_invitation");
        Assert.True(nullableExact.IsUnique);
        Assert.Null(nullableExact.GetFilter());
        Assert.Contains(nullableExact.Properties, x => x.Name == nameof(SupplierBid.InvitationId)
            && x.IsNullable);
    }

    [Fact]
    public void Retained_round_evidence_is_append_only_even_with_same_scope()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = Guid.NewGuid(),
        };
        db.Attach(opening);
        db.Entry(opening).Property(x => x.SnapshotSha256).CurrentValue = new string('a', 64);
        Assert.Equal("RfqCommercialEvidenceAppendOnly",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Bare_opening_or_receipt_cannot_be_saved_as_complete_business_evidence()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = Guid.NewGuid(),
        };
        db.Add(opening);
        Assert.Equal("RfqCommercialSameSaveReceiptRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        db.ChangeTracker.Clear();
        db.Add(new PrincipalRfqCommercialCommandReceipt
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = opening.BranchId,
            Operation = "Open", CommandKey = Guid.NewGuid(), TargetId = opening.Id,
            RequestSha256 = new string('a', 64), ResponseJson = "{}",
            ResponseSha256 = ProcurementEvidenceEncoding.Digest("{}"),
        });
        Assert.Equal("RfqCommercialReceiptTargetRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Same_branch_receipt_for_another_opening_cannot_ack_a_decision()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var branch = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var expectedRound = Guid.NewGuid();
        var unrelatedRound = Guid.NewGuid();
        var decision = new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = expectedRound, SourcingEventId = eventId, BranchId = branch,
            ActorPrincipalId = scope.SubjectId,
        };
        db.Add(decision);
        var response = "{}";
        db.Add(new PrincipalRfqCommercialCommandReceipt
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = unrelatedRound, SourcingEventId = eventId, BranchId = branch,
            Operation = "Decide", DecisionTargetId = decision.Id, TargetId = decision.Id,
            CommandKey = Guid.NewGuid(), ActorPrincipalId = scope.SubjectId,
            RequestSha256 = new string('a', 64), ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
        });
        Assert.Equal("RfqCommercialReceiptTargetRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Decision_without_its_own_same_save_receipt_cannot_persist()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        db.Add(new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = Guid.NewGuid(), SourcingEventId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(), ActorPrincipalId = scope.SubjectId,
        });
        Assert.Equal("RfqCommercialSameSaveReceiptRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void A_same_save_sealed_offer_cannot_be_followed_by_a_quote_draft_edit()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var bid = new SupplierBid
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            SourcingEventId = Guid.NewGuid(), SupplierId = Guid.NewGuid(),
            Status = "Draft", SourceChannel = "Manual", SurrogateEnteredBy = scope.SubjectId,
        };
        db.Attach(bid);
        bid.Status = "Cancelled";
        db.Add(new PrincipalRfqCommercialOfferSubmissionRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            SupplierBidId = bid.Id, OpeningId = Guid.NewGuid(),
            SourcingEventId = bid.SourcingEventId, BranchId = Guid.NewGuid(),
            RecorderPrincipalId = scope.SubjectId,
        });
        Assert.Equal("RfqCommercialSealedQuoteImmutable",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Decision_with_exact_same_save_receipt_passes_the_append_guard()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            SourcingEventId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            CoreCaseId = Guid.NewGuid(), SnapshotSha256 = new string('b', 64),
            ActorPrincipalId = scope.SubjectId,
        };
        // This source-only guard test models an already retained opening. The
        // physical owner/FK graph is verified by the separate PostgreSQL gate.
        db.Attach(opening);
        var decision = new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = opening.Id, SourcingEventId = opening.SourcingEventId,
            BranchId = opening.BranchId, CoreCaseId = opening.CoreCaseId,
            CoreDecisionId = Guid.NewGuid(), CoreAssignmentId = Guid.NewGuid(),
            State = "Approved", ActorPrincipalId = scope.SubjectId,
            OpeningSha256 = opening.SnapshotSha256,
            CoreOutcomeSha256 = new string('c', 64),
        };
        db.Add(decision);
        var key = Guid.NewGuid();
        var response = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, operation = "Decide", commandKey = key,
            openingId = opening.Id, eventId = opening.SourcingEventId,
            branchId = opening.BranchId, targetId = decision.Id,
            responseStatus = 200,
            result = new { roundId = opening.Id, decisionId = decision.Id,
                coreDecisionId = decision.CoreDecisionId,
                outcomeSha256 = decision.CoreOutcomeSha256, state = decision.State },
        });
        db.Add(new PrincipalRfqCommercialCommandReceipt
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = opening.Id, SourcingEventId = opening.SourcingEventId,
            BranchId = opening.BranchId, Operation = "Decide",
            DecisionTargetId = decision.Id, TargetId = decision.Id,
            CommandKey = key, ActorPrincipalId = scope.SubjectId,
            RequestSha256 = new string('a', 64), ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
            ResponseStatus = 200,
        });
        Assert.Equal(4, db.SaveChanges());
        var audits = db.AuditEvents.AsNoTracking()
            .Where(x => x.RecordId == decision.Id || x.RecordId == db.PrincipalRfqCommercialCommandReceipts.Single().Id)
            .ToArray();
        Assert.Equal(2, audits.Length);
        Assert.All(audits, audit => Assert.Equal(scope.SubjectId, audit.ActorSubjectId));
        Assert.Contains(audits, audit => audit.RecordId == decision.Id && audit.RecordType == nameof(PrincipalRfqCommercialRoundDecisionRecord));
        Assert.Contains(audits, audit => audit.RecordId == db.PrincipalRfqCommercialCommandReceipts.Single().Id
            && audit.RecordType == nameof(PrincipalRfqCommercialCommandReceipt));
    }

    [Fact]
    public void Self_hashed_response_without_original_decision_facts_cannot_ack_it()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            BranchId = Guid.NewGuid(), ActorPrincipalId = scope.SubjectId,
            CoreCaseId = Guid.NewGuid(), SnapshotSha256 = new string('b', 64),
        };
        db.Attach(opening);
        var decision = new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = opening.Id, SourcingEventId = opening.SourcingEventId,
            BranchId = opening.BranchId, CoreCaseId = opening.CoreCaseId,
            OpeningSha256 = opening.SnapshotSha256, ActorPrincipalId = scope.SubjectId,
        };
        db.Add(decision);
        db.Add(new PrincipalRfqCommercialCommandReceipt
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OpeningId = opening.Id, SourcingEventId = opening.SourcingEventId,
            BranchId = opening.BranchId, Operation = "Decide",
            DecisionTargetId = decision.Id, TargetId = decision.Id,
            CommandKey = Guid.NewGuid(), ActorPrincipalId = scope.SubjectId,
            RequestSha256 = new string('a', 64), ResponseJson = "{}",
            ResponseSha256 = ProcurementEvidenceEncoding.Digest("{}"),
        });
        Assert.Equal("RfqCommercialReceiptTargetRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Close_cannot_commit_a_new_offer_outside_its_retained_set_in_either_call_order()
    {
        foreach (var closeFirst in new[] { false, true })
        {
            var scope = new Scope();
            using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
            var round = Guid.NewGuid();
            var branch = Guid.NewGuid();
            var offer = new PrincipalRfqCommercialOfferSubmissionRecord
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                OpeningId = round, BranchId = branch, RecorderPrincipalId = scope.SubjectId,
            };
            var seal = new PrincipalRfqCommercialRoundSealRecord
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                OpeningId = round, BranchId = branch, OfficerPrincipalId = scope.SubjectId,
            };
            if (closeFirst) { db.Add(seal); db.Add(offer); }
            else { db.Add(offer); db.Add(seal); }
            Assert.Equal("RfqCommercialOfferSetNotCommitted",
                Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        }
    }
}
