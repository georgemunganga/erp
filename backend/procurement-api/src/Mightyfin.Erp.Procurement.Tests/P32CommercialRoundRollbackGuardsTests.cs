using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class P32CommercialRoundRollbackGuardsTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "fixture";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Every_modeled_P32_family_is_locked_and_vetoed_before_downgrade()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only_not_opened;Username=none;Password=none")
            .Options, new Scope());
        var owned = db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null && (x.StartsWith("rfq_commercial_", StringComparison.Ordinal)
                || x.StartsWith("rfq_prepared_", StringComparison.Ordinal)
                || x.StartsWith("rfq_preparation_", StringComparison.Ordinal)
                || x.StartsWith("principal_rfq_commercial_", StringComparison.Ordinal)
                || x.StartsWith("principal_rfq_criterion_", StringComparison.Ordinal)))
            .Cast<string>().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(15, owned.Count);
        var sql = P32CommercialRoundRollbackGuards.BeforeDownSql;
        var locked = Regex.Matches(sql[..sql.IndexOf(" IN ACCESS EXCLUSIVE MODE", StringComparison.Ordinal)],
                @"procurement\.([a-z_]+)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var vetoed = Regex.Matches(sql, @"EXISTS \(SELECT 1 FROM procurement\.([a-z_]+)\)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(owned.IsSubsetOf(locked));
        Assert.True(owned.SetEquals(vetoed));
        Assert.Contains("ERRCODE = '55000'", sql, StringComparison.Ordinal);
        Assert.Contains("procurement.audit_events", sql);
        Assert.Contains("procurement.outbox_events", sql);
        Assert.Contains("'PrincipalRfqOfferOpeningRecord'", sql);
        Assert.Contains("'PrincipalRfqOfferOpeningReceipt'", sql);
        Assert.Contains("'PrincipalRfqCriterionAssessmentRecord'", sql);
        Assert.Contains("'sourcing.commercial_offers_opened.v1'", sql);
        Assert.Contains("'sourcing.criterion_assessment_recorded.v1'", sql);
    }

    [Fact]
    public void Down_veto_enumerates_every_owned_record_type_and_event_type_in_shared_history()
    {
        var sql=P32CommercialRoundRollbackGuards.BeforeDownSql;
        var auditTypes=new[]{
            nameof(PrincipalRfqCommercialDraftHead),nameof(PrincipalRfqCommercialDraftRevision),
            nameof(PrincipalRfqCommercialDraftReceipt),nameof(PrincipalRfqPreparedPlan),
            nameof(PrincipalRfqPreparationReceipt),nameof(PrincipalRfqCommercialRoundOpeningRecord),
            nameof(PrincipalRfqCommercialRoundVoteRecord),nameof(PrincipalRfqCommercialRoundDecisionRecord),
            nameof(PrincipalRfqCommercialOfferSubmissionRecord),nameof(PrincipalRfqCommercialRoundSealRecord),
            nameof(PrincipalRfqCommercialCommandReceipt),nameof(PrincipalRfqOfferOpeningRecord),
            nameof(PrincipalRfqOfferOpeningReceipt),nameof(PrincipalRfqCriterionAssessmentRecord),
            nameof(PrincipalRfqCriterionAssessmentReceipt),
            "RfqCommercialDraft","RfqPreparedPlan","RfqCommercialRound"};
        var auditArm=Regex.Match(sql,@"OR EXISTS \(SELECT 1 FROM procurement\.audit_events\s+WHERE record_type IN \((.*?)\)\)",RegexOptions.Singleline);
        Assert.True(auditArm.Success);
        var protectedAuditTypes=Regex.Matches(auditArm.Groups[1].Value,@"'([^']+)'")
            .Select(x=>x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(auditTypes.Length,protectedAuditTypes.Count);
        Assert.True(auditTypes.ToHashSet(StringComparer.Ordinal).SetEquals(protectedAuditTypes));
        var outboxArm=Regex.Match(sql,@"OR EXISTS \(SELECT 1 FROM procurement\.outbox_events\s+WHERE event_type IN \((.*?)\)\)",RegexOptions.Singleline);
        Assert.True(outboxArm.Success);
        var protectedEvents=Regex.Matches(outboxArm.Groups[1].Value,@"'([^']+)'")
            .Select(x=>x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(new[]{"sourcing.commercial_draft_changed.v1",
            "sourcing.prepared_candidate_recorded.v1","sourcing.commercial_round_recorded.v1",
            "sourcing.commercial_offers_opened.v1","sourcing.criterion_assessment_recorded.v1"}
            .ToHashSet(StringComparer.Ordinal).SetEquals(protectedEvents));
        Assert.Equal(5,protectedEvents.Count);
        Assert.Contains("ERRCODE = '55000'",sql,StringComparison.Ordinal);
    }
    [Fact]
    public void Genuine_migration_installs_exact_ownership_after_indexes_and_vetoes_before_any_drop()
    {
        var migration = new P32PrincipalCommercialEvidence();
        var up = migration.UpOperations.ToArray();
        var down = migration.DownOperations.ToArray();
        Assert.Equal(15, up.OfType<CreateTableOperation>().Count());
        Assert.Equal(P32CommercialRoundRollbackGuards.BeforeDownSql,
            Assert.IsType<SqlOperation>(down[0]).Sql);
        var installs = new[] { RfqCommercialDraftOwnershipConstraintSql.Install,
            RfqPreparationOwnershipConstraintSql.Install,
            PrincipalRfqCommercialOfferInvitationOwnershipSql.Install };
        Assert.Equal(installs, up.TakeLast(3).Select(x => Assert.IsType<SqlOperation>(x).Sql));
        var uniqueInvitation = Array.FindIndex(up, x => x is CreateIndexOperation index
            && index.Name == "ux_p32_bid_exact_invitation" && index.IsUnique && index.Filter is null);
        Assert.True(uniqueInvitation >= 0 && uniqueInvitation < up.Length - installs.Length);
        Assert.Equal(new[] { PrincipalRfqCommercialOfferInvitationOwnershipSql.Remove,
            RfqPreparationOwnershipConstraintSql.Remove,
            RfqCommercialDraftOwnershipConstraintSql.Remove },
            down.Skip(1).Take(3).Select(x => Assert.IsType<SqlOperation>(x).Sql));
        var installedOwners = Regex.Matches(string.Join("\n", installs),
                @"ALTER TABLE procurement\.([a-z0-9_]+)\s+ADD CONSTRAINT ([a-z0-9_]+)")
            .Select(x => (x.Groups[1].Value, x.Groups[2].Value)).ToHashSet();
        var removedOwners = Regex.Matches(string.Join("\n", down.Skip(1).Take(3)
                .Cast<SqlOperation>().Select(x => x.Sql)),
                @"ALTER TABLE procurement\.([a-z0-9_]+) DROP CONSTRAINT ([a-z0-9_]+);")
            .Select(x => (x.Groups[1].Value, x.Groups[2].Value)).ToHashSet();
        Assert.Equal(9, installedOwners.Count);
        Assert.True(installedOwners.SetEquals(removedOwners));
        Assert.IsType<DropTableOperation>(down[4]);
        // Nullable historical invitations must survive this additive migration.
        Assert.DoesNotContain(up, x => x is AlterColumnOperation column
            && column.Table == "supplier_bids" && column.Name == "invitation_id");
    }

}
