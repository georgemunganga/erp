using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalManualQuotationEvidenceTests
{
    [Fact]
    public void Actual_shared_preparation_accepts_scoped_factory_contact_with_exact_invitation_transition()
    {
        var f = new Fixture(); var contact = f.Contact(); using var db = f.Context();
        AttachPublication(db, f); db.Attach(f.Invitation);
        f.Invitation.Status = "ManualContactRecorded"; db.AddScoped(contact);
        AddEffects(db, PrincipalQuotationEffects.Contact(contact));
        Prepare(db);
        Assert.Equal(contact.InvitationVersionAtCapture, f.Invitation.Version);
        Assert.Equal(f.Actor, contact.CreatedBy);
        Assert.Contains(db.ChangeTracker.Entries<AuditEvent>(), x => x.Entity.RecordType == nameof(SourcingManualContact));
    }

    [Theory]
    [InlineData("without-contact")]
    [InlineData("changed-supplier")]
    [InlineData("changed-source")]
    [InlineData("wrong-key-scope")]
    public void Actual_invitation_preparation_rejects_legacy_or_foreign_contact_transition(string fault)
    {
        var f = new Fixture(); var contact = f.Contact(); using var db = f.Context();
        AttachPublication(db, f); db.Attach(f.Invitation);
        f.Invitation.Status = "ManualContactRecorded";
        if (fault == "changed-supplier") f.Invitation.SupplierId = Guid.NewGuid();
        if (fault == "changed-source") f.Invitation.SourcingEventId = Guid.NewGuid();
        if (fault == "wrong-key-scope") contact = Change(contact, x => x["ActorPrincipalId"] = Guid.NewGuid().ToString("D"));
        if (fault != "without-contact") db.AddScoped(contact);
        Assert.Throws<InvalidOperationException>(() => Prepare(db));
    }

    [Fact]
    public void Actual_factory_V3_header_lines_and_receipt_pass_owning_preparation_together()
    {
        var f = new Fixture(); var contact = f.Contact(); var draft = f.Draft(contact);
        f.Invitation.Version = contact.InvitationVersionAtCapture;
        var offer = Offer(); var input = OfferedLines(draft);
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, offer, input);
        var command = new PrincipalQuotationCommand("Create", f.Event.Version, f.Invitation.Version,
            null, null, null, offer, input);
        var receipt = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            draft.Bid, draft.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At, "Create", command: command);
        using var db = f.Context(); AttachPublication(db, f); db.Attach(f.Invitation); db.Attach(contact);
        foreach (var source in f.Sources) db.Attach(source);
        db.AddScoped(draft.Bid); foreach (var line in draft.Lines) db.AddScoped(line); db.AddScoped(receipt);
        AddEffects(db, PrincipalQuotationEffects.Quotation(receipt));
        Prepare(db);
        Assert.Equal(1, draft.Bid.Version); Assert.Equal(draft.Bid.Version, receipt.BidVersion);
        Assert.Equal(3, receipt.SnapshotSchemaVersion);
        Assert.Equal(4, db.ChangeTracker.Entries<AuditEvent>().Count());
    }

    [Theory]
    [InlineData("header")]
    [InlineData("line")]
    [InlineData("downgrade")]
    public async Task Actual_Save_rejects_M7_mutation_without_owning_version_before_database_write(string fault)
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact());
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, Offer(), OfferedLines(draft));
        await using var db = f.Context();
        if (fault == "line")
        {
            db.Attach(draft.Bid); db.Attach(draft.Lines[0]); draft.Lines[0].Notes = "legacy edit";
        }
        else
        {
            db.Attach(draft.Bid); draft.Bid.CorrectionReason = "legacy edit";
            if (fault == "downgrade") draft.Bid.PrincipalCaptureVersion = 0;
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        if (fault == "downgrade") Assert.Contains("part of a key", error.Message);
        else Assert.Equal("ManualQuotationOwnedCommandRequired", error.Message);
        Assert.Empty(await db.SupplierBids.AsNoTracking().ToArrayAsync());
    }

    [Theory]
    [InlineData("missing-audit")]
    [InlineData("missing-outbox")]
    [InlineData("changed-payload")]
    [InlineData("wrong-actor")]
    [InlineData("duplicate-audit")]
    public void Owning_effect_producer_requires_exact_atomic_marker_pair(string fault)
    {
        var f = new Fixture(); var contact = f.Contact();
        var expected = PrincipalQuotationEffects.Contact(contact);
        var actual = PrincipalQuotationEffects.Contact(contact);
        actual.Audit.CorrelationId = "trace";
        var audits = new List<AuditEvent> { actual.Audit }; var outbox = new List<OutboxEvent> { actual.Outbox };
        switch (fault)
        {
            case "missing-audit": audits.Clear(); break;
            case "missing-outbox": outbox.Clear(); break;
            case "changed-payload": actual.Outbox.PayloadJson = "{}"; break;
            case "wrong-actor": actual.Audit.ActorSubjectId = Guid.NewGuid().ToString("D"); break;
            case "duplicate-audit": audits.Add(actual.Audit); break;
        }
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationEffects.Verify(expected, audits, outbox, "trace"));
    }

    [Fact]
    public void Actual_prepare_rejects_contact_when_durable_owning_effects_are_missing()
    {
        var f = new Fixture(); var contact = f.Contact(); using var db = f.Context();
        AttachPublication(db, f); db.Attach(f.Invitation); f.Invitation.Status = "ManualContactRecorded";
        db.AddScoped(contact);
        Assert.Equal("QuotationOwningEffectsMissing", Assert.Throws<InvalidOperationException>(() => Prepare(db)).Message);
    }

    private static void AddEffects(ProcurementDbContext db, PrincipalQuotationEffects.Effects effects)
    {
        effects.Audit.CorrelationId = "m7-factory";
        db.AddScoped(effects.Audit); db.AddScoped(effects.Outbox);
    }

    private static void AttachPublication(ProcurementDbContext db, Fixture f)
    {
        db.Attach(f.Event); db.Attach(f.Opening); db.Attach(f.Decision); db.Attach(f.Receipt);
    }
    private static void Prepare(ProcurementDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        try
        {
            typeof(ProcurementDbContext).GetMethod("PrepareChanges", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(db, null);
        }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }
}
