using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanSchemaCaptureTests
{
    [Theory]
    [InlineData("procurement.plan_review_submissions.ck_plan_review_submission", false)]
    [InlineData("procurement.plan_review_submissions.ck_plan_review_submission", true)]
    [InlineData("procurement.plan_review_command_receipts.ck_plan_review_receipt", false)]
    [InlineData("procurement.plan_review_command_receipts.ck_plan_review_receipt", true)]
    [InlineData("procurement.operational_policy_identity_evidence.ck_operational_policy_identity", false)]
    [InlineData("procurement.operational_policy_identity_evidence.ck_operational_policy_identity", true)]
    public void Only_exact_named_direct_or_actual_restore_definition_is_admitted(string key, bool restored)
    {
        var definition = (restored ? ProcurementPlanReviewSchemaCapture.RestoredChecks
            : ProcurementPlanReviewSchemaCapture.Checks)[key];
        Assert.True(ProcurementPlanReviewSchemaCapture.CheckMatches(key, definition));
        Assert.False(ProcurementPlanReviewSchemaCapture.CheckMatches(key, definition + " "));
        Assert.False(ProcurementPlanReviewSchemaCapture.CheckMatches("procurement.other." + key.Split('.').Last(), definition));
        Assert.False(ProcurementPlanReviewSchemaCapture.CheckMatches(key, "CHECK (true)"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applied_Plan_migration_requires_Plan_capture_and_never_falls_back_to_old_policy_check(bool restored)
    {
        var current = (restored ? ProcurementPlanReviewSchemaCapture.RestoredChecks
            : ProcurementPlanReviewSchemaCapture.Checks)[ProcurementPlanReviewSchemaCapture.OperationalCheckKey];
        var applied = new[] { ProcurementInvoiceOwnerBaselineSchemaCapture.MigrationId,
            ProcurementPlanReviewSchemaCapture.MigrationId };
        bool NoFallback(string _) => throw new InvalidOperationException("Old capture must not be selected");
        Assert.True(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied, current, NoFallback));
        Assert.False(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied,
            ProcurementInvoiceOwnerBaselineSchemaCapture.OperationalCheckDefinition, NoFallback));
        Assert.False(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied,
            current.Replace("'PROCUREMENT_PLAN_REVIEW'", "'UNREVIEWED_POLICY'"), NoFallback));
    }

    [Fact]
    public void Without_Plan_history_existing_owner_capture_keeps_its_exact_prior_contract()
    {
        var applied = new[] { ProcurementInvoiceOwnerBaselineSchemaCapture.MigrationId };
        bool Previous(string value) => ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(
            ProcurementPlanReviewSchemaCapture.OperationalCheckKey, value);
        Assert.True(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied,
            ProcurementInvoiceOwnerBaselineSchemaCapture.OperationalCheckDefinition, Previous));
        Assert.True(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied,
            ProcurementInvoiceOwnerBaselineSchemaCapture.RestoredChecks[ProcurementPlanReviewSchemaCapture.OperationalCheckKey], Previous));
        Assert.False(ProcurementPlanReviewSchemaCapture.OperationalCheckMatches(applied,
            ProcurementPlanReviewSchemaCapture.Checks[ProcurementPlanReviewSchemaCapture.OperationalCheckKey], Previous));
        Assert.False(ProcurementPlanReviewSchemaCapture.Installed(new[] { "unknown-migration" }));
    }

    [Fact]
    public void Payment_PostPlan_query_uses_exact_new_restore_pair_and_preserves_whole_catalog_checks()
    {
        var sql = ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(
            ownerBaselineInstalled: true, planInstalled: true);
        static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var key = ProcurementPlanReviewSchemaCapture.OperationalCheckKey;
        Assert.Equal("2a1a42eccb1379a8039da14a59458440a286f8967db5901abe3f144e532fe759",
            ProcurementPlanReviewSchemaCapture.ManualPaymentCatalogFingerprint);
        Assert.Contains("pg_get_constraintdef(c.oid)=" + Literal(ProcurementPlanReviewSchemaCapture.RestoredChecks[key])
            + " THEN " + Literal(ProcurementPlanReviewSchemaCapture.Checks[key]), sql);
        // Every unrelated observed restore pair and all catalog dimensions remain
        // in the original fingerprint expression; no catalog region is excluded.
        foreach (var other in ProcurementManualPaymentGovernanceReadiness.ObservedRestoreChecks.Where(x => x != key))
        {
            var pair = ProcurementRestoreCheckAlternatives.Definitions[other];
            Assert.Contains("pg_get_constraintdef(c.oid)=" + Literal(pair.Restored)
                + " THEN " + Literal(pair.Canonical), sql);
        }
        foreach (var section in new[] { "'columns'", "'constraints'", "'indexes'", "'triggers'", "'functions'" })
            Assert.Contains(section, sql);
    }

    [Fact]
    public void Payment_prior_query_remains_default_without_applied_Plan_selection()
    {
        Assert.Equal(ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(true),
            ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(true, false));
        Assert.DoesNotContain("PROCUREMENT_PLAN_REVIEW",
            ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(true, false));
        Assert.Contains("PROCUREMENT_PLAN_REVIEW",
            ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(true, true));
    }

    [Fact]
    public void Plan_manifest_has_only_two_owned_checks_and_shared_capture_is_complete()
    {
        Assert.True(ProcurementPlanReviewSchemaCapture.Populated);
        Assert.True(ProcurementPlanReviewSchemaReadiness.ManifestCaptured);
        Assert.Equal("20261103211606_ProcurementPlanGovernedReview", ProcurementPlanReviewSchemaReadiness.RequiredMigrationId);
        Assert.Equal(new[] { "ck_plan_review_receipt", "ck_plan_review_submission" },
            ProcurementPlanReviewSchemaReadiness.CheckDefinitions.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("d2c33412e6b673dc6a5c0c12be7e00ed0cf7bc25e9934b63b1f3345703e8cd74",
            ProcurementPlanReviewSchemaCapture.DirectCaptureSha256);
        Assert.Equal("4877d179fee8ae5b6cacbe57fbcebfb59af8cacceb0147d5ff6841fdb2283250",
            ProcurementPlanReviewSchemaCapture.RestoredCaptureSha256);
    }
}
