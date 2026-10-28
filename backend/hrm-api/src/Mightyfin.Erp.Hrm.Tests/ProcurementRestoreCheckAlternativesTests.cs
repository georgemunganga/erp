using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRestoreCheckAlternativesTests
{
    public static TheoryData<string, string, string> ExactObservedCases => new()
    {
        { "procurement.assignment_authority_evidence.ck_pr_assignment_authority_identity", "c696e8448b5ef3278ab815ff0df14a7dcd7d2377934b02cefcf65d4abb1620dd", "fcc6bff108627dc856af0b803cc168ea8d1e611473386ce162d6f6b296dde27a" },
        { "procurement.operational_policy_identity_evidence.ck_operational_policy_identity", "ded52b79a382969e0700bd1ab54647a94175c8d1079016c46accfe8f063a1ee0", "3d2d7eefc14608797486bb0b21dbf1d8e4e18da9f86381915f72b4537f3ef612" },
        { "procurement.principal_order_acknowledgments.ck_principal_order_acknowledgment", "85fa005c094631b53ba23d12f74a7ca531ccbc7d20ffd4ab964dcecfa5cd1b96", "66d5d16d454f1de72a8736f36834081b188caf9f1b79ea8562dcd17c27b3148a" },
        { "procurement.principal_order_decisions.ck_principal_order_decision", "b372e4bff233f0f5a64709122e693e893bfe00764e2dae2cf85a6192377dbefe", "c3bf0a11ed9af06e5aed8e1319e587b50032bf782682e40f79754db961d9790d" },
        { "procurement.principal_order_draft_receipts.ck_po_principal_receipt", "7c0fcde85a740950334d3fbc0e3ee3128f0a51262fa5c0385b39b908cad3070e", "8fd542ee485e9b0069ba13c93bfa52017356facf67f2072cfe381d47dc78fc72" },
        { "procurement.principal_order_release_requests.ck_principal_order_release_request", "bffcc2f90f0bc76892ca1461499fad4a4e734991a9e6575fbc67cbb52bac9429", "12657a808df922fa3468e7ec75d79351b46738f6aef1a5ea027e9f178b76b52f" },
        { "procurement.principal_receiving_drafts.ck_prcv_draft", "c33d6962bb71a95b74e15a3911b848993d81eb8f2cbda88cca2f4783fd2b734c", "697ead9e9e4bb99e38cdb89fb8c24ebad3720698e9bc177062fca3e14eb3d923" },
        { "procurement.principal_receiving_facts.ck_prcv_fact", "10adf718536b3e1de345aaa32b50a0491153ebcdfdde7bc9ef9fd58af26db272", "87b1dc6181ae55ef5c03ac99e8ba7d3950897566865514025de68c4b54a8969e" },
        { "procurement.principal_receiving_receipts.ck_prcv_receipt", "fe6b813ae280152663cd0013d5d62a56628b5a21b7952d522457248333b5af8b", "9e55c194357c7f7e68e87a712acf5de47f38d0cea371321b342710814ccc22b5" },
        { "procurement.principal_receiving_sources.ck_prcv_source", "0ba7828812d571d483f82f29716d0166fafaf7f8332f218724ad848aa5c8f938", "f1d572dc6be6690890ffe3de0517c63db6937050b63abd1dfbafbbdbe75f9a05" },
        { "procurement.purchase_orders.ck_po_principal_draft_version", "c5c93b16f666d283ae859d4f24a945dfc43a8e2697e8ce18908404931285dacd", "1e83fa2fcde59e7b07427bdf8307829bee1f3b81d39babca6bca1691478598e7" },
        { "procurement.sourcing_command_receipts.ck_rfq_receipt_identity", "6655f301bd4b0c80611a54c5994725bf172b07fd1fee390e9e55e5ccee363a6e", "4e58f02a6e44e8210cb2776d06c597e598d6c72026851c8690b3d03bdf7d9e0d" },
        { "procurement.manual_payment_core_decisions.ck_manual_payment_core_decision", "a17ffb0e78c3c45c5d4ab966a631c0b39d8ea7fdd2e80253dcd3011b0ace4920", "d66f2b73461af81c360c7ebd93c97788270e0bd2a1e1b3ab699d0f8051aba0d0" },
        { "procurement.manual_payment_date_observations.ck_manual_payment_company_date", "586c960a65d056b8de03404c50ab8b02f8ed0381723f8471951771f5023a9a18", "512020c4f2a66fc1649e5942010b0a2e4071dbfd9b15da965782bef75eb52bf6" },
        { "procurement.manual_payment_decisions.ck_manual_payment_action_states", "51e3568e0c354568af6333878e860f3d9701146760f40981c73edf4724759e4f", "5070b8435e794bf0922317b8abdebaebd43c3233ea447307abbdf5c5be8f5001" },
        { "procurement.manual_payment_decisions.ck_manual_payment_transition", "719827736f22b385aaac4967bff9971e2dab74ecdf205481af110ce15b4c7c46", "f68705e45a53874d6c98a7f6d74560c51025d934daaced2f5603df20bfc1d1cf" },
        { "procurement.manual_payment_receipts.ck_manual_payment_receipt", "8d65fe91c2dfb68f4f1b688982f602c099362651ad39922b7a97e5fb660dd2be", "a3f6e0c69d5f3d7c0eb0027a3630b17c62286d91cd990b8aa03af983fc1e7265" },
        { "procurement.manual_payment_requests.ck_manual_payment_state_evidence", "90fd662e5345caf22574c364423c05dee9a067cdfd875d680c15187fe5f80092", "bc8783a117ffebc061093db2afb5798d7b925eefb0080d895654275dabf57b65" },
        { "procurement.manual_payment_requests.ck_manual_payment_values", "c0ca366c9e2563bc968edd4bfb650e32b8ff20e8b784d8a86a4517fece3bfab1", "dbd9953d9c4c2e768dc68d8beaa384f20d3dbb2ebce26771e26678a799ca8564" },
        { "procurement.manual_payment_workflow_cases.ck_manual_payment_case_source", "7a73deb157158191ab8ec19ba7ebb8f22798c5169d961ecd69e7188edfc93523", "718fb77a7de5cfb54feaee6984a8bc5794a1855d1ec0e299a54060d5b571ed77" },
        { "procurement.principal_goods_return_drafts.ck_pgret_draft", "faeb8294dd01da3ba71c685aff00fe93a0e01a3426f2a8eb14cf603287f000f6", "d1d66d7fafe6fd66adbea3edd2402303e82ec69f9cc80edaeddcb1d9cdb51948" },
        { "procurement.principal_goods_return_receipts.ck_pgret_receipt", "713a7150dccf60497413782dd810a02d8bdbdd38a89f4a14332d6f7ac79bdf27", "50b2ce5c4d689babd4f5de61101217760c42b9d2404e4107c2a768f0584bcd5f" },
        { "procurement.principal_goods_return_revisions.ck_pgret_revision", "ee287e75f907d7786a0ed5ef3bdfa41ba3cb4d45a84eef8b51ff04ac62e6673b", "b54a3abe12c1041af2cacf9468b988af9dce1900e047519a3362f6b9be634a71" },
        { "procurement.invoice_unit_review_receipts.ck_inv_unit_receipt", "7723ff9e9241da66d1171ec3c3f1661ba43ba96d2bb74eb6d3f19130086d5852", "db0ba84bd4dd4a48371b90bdecffcc1e80743b9abe7b74401d44883ad2bb876e" },
    };

    [Fact]
    public void Unit_receipt_restore_preserves_actor_key_fingerprint_payload_and_time_guards()
    {
        const string key = "procurement.invoice_unit_review_receipts.ck_inv_unit_receipt";
        var pair = ProcurementRestoreCheckAlternatives.Definitions[key];
        Assert.Equal(ProcurementInvoiceUnitReviewSchemaReadiness.CheckDefinitions["ck_inv_unit_receipt"], pair.Canonical);
        foreach (var (guard, weakened) in new[]
        {
            ("'Decision'", "'UnreviewedOperation'"),
            ("command_key <>", "command_key ="),
            ("actor_worker_id <>", "actor_worker_id ="),
            ("actor_principal_id)::text <>", "actor_principal_id)::text ="),
            ("fingerprint IS NOT NULL", "fingerprint IS NULL"),
            ("octet_length(response_json) <= 262144", "octet_length(response_json) <= 262145"),
            ("octet_length(request_json) <= 65536", "octet_length(request_json) <= 65537"),
            ("isfinite(completed_at)", "TRUE"),
        })
        {
            Assert.Contains(guard, pair.Restored);
            Assert.False(ExistingComparator(key, pair.Canonical,
                pair.Restored.Replace(guard, weakened, StringComparison.Ordinal)));
        }
        Assert.False(ExistingComparator(key, pair.Canonical.Replace("isfinite(completed_at)", "TRUE", StringComparison.Ordinal), pair.Restored));
        Assert.False(ProcurementRestoreCheckAlternatives.Matches("procurement.invoice_unit_review_outcomes.ck_inv_unit_receipt", pair.Canonical, pair.Restored));
    }

    [Theory]
    [MemberData(nameof(ExactObservedCases))]
    public void Only_exact_qualified_canonical_and_restored_pair_is_admitted(string key, string canonicalSha256, string restoredSha256)
    {
        Assert.Equal(24, ProcurementRestoreCheckAlternatives.Definitions.Count);
        var pair = ProcurementRestoreCheckAlternatives.Definitions[key];
        Assert.NotEqual(pair.Canonical, pair.Restored);
        Assert.Equal(canonicalSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Canonical))).ToLowerInvariant());
        Assert.Equal(restoredSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Restored))).ToLowerInvariant());
        Assert.True(ExistingComparator(key, pair.Canonical, pair.Canonical));
        Assert.True(ExistingComparator(key, pair.Canonical, pair.Restored));
        Assert.False(ProcurementRestoreCheckAlternatives.Matches(key, pair.Canonical + " ", pair.Restored));
        Assert.False(ProcurementRestoreCheckAlternatives.Matches("procurement.unrelated." + key.Split('.')[^1], pair.Canonical, pair.Restored));
        Assert.False(ExistingComparator(key, pair.Canonical, pair.Restored.Replace("CHECK (", "CHECK (TRUE OR ", StringComparison.Ordinal)));
        Assert.False(ExistingComparator(key, pair.Canonical, pair.Restored.Replace("CHECK (", "CHECK ((tenant_id = 'other') OR ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Changed_enum_operator_and_quantity_guard_are_rejected()
    {
        var source = ProcurementRestoreCheckAlternatives.Definitions["procurement.principal_receiving_sources.ck_prcv_source"];
        Assert.Contains("'Goods'", source.Restored);
        Assert.False(ExistingComparator("procurement.principal_receiving_sources.ck_prcv_source", source.Canonical,
            source.Restored.Replace("'Goods'", "'UnreviewedKind'", StringComparison.Ordinal)));
        var decision = ProcurementRestoreCheckAlternatives.Definitions["procurement.principal_order_decisions.ck_principal_order_decision"];
        Assert.Contains("= ANY", decision.Restored);
        Assert.False(ExistingComparator("procurement.principal_order_decisions.ck_principal_order_decision", decision.Canonical,
            decision.Restored.Replace("= ANY", "<> ANY", StringComparison.Ordinal)));
        Assert.Contains("accepted_quantity >= (0)::numeric", source.Restored);
        Assert.False(ExistingComparator("procurement.principal_receiving_sources.ck_prcv_source", source.Canonical,
            source.Restored.Replace("accepted_quantity >= (0)::numeric", "accepted_quantity < (0)::numeric", StringComparison.Ordinal)));
        var receipt = ProcurementRestoreCheckAlternatives.Definitions["procurement.sourcing_command_receipts.ck_rfq_receipt_identity"];
        Assert.Contains("'Cancel'", receipt.Restored);
        Assert.False(ExistingComparator("procurement.sourcing_command_receipts.ck_rfq_receipt_identity", receipt.Canonical,
            receipt.Restored.Replace("'Cancel'", "'UnreviewedOperation'", StringComparison.Ordinal)));
        foreach (var (guard, weakened) in new[]
        {
            ("response_schema_version = 1", "response_schema_version = 0"),
            ("actor_worker_id IS NOT NULL", "actor_worker_id IS NULL"),
            ("response_status_code = 201", "response_status_code = 200"),
            ("result_version > 0", "result_version >= 0"),
        })
        {
            Assert.Contains(guard, receipt.Restored);
            Assert.False(ExistingComparator("procurement.sourcing_command_receipts.ck_rfq_receipt_identity",
                receipt.Canonical, receipt.Restored.Replace(guard, weakened, StringComparison.Ordinal)));
        }
        var operational = ProcurementRestoreCheckAlternatives.Definitions[
            "procurement.operational_policy_identity_evidence.ck_operational_policy_identity"];
        foreach (var (guard, weakened) in new[]
        {
            ("'MANUAL_PAYMENT_REVIEW'", "'UNREVIEWED_POLICY'"),
            ("((policy_key)::text <> 'MANUAL_PAYMENT_REVIEW'::text) OR (schema_version = 2)",
             "((policy_key)::text <> 'MANUAL_PAYMENT_REVIEW'::text) OR (schema_version = 1)"),
            ("created_evidence_id <> id", "created_evidence_id = id"),
            ("policy_record_version_at_action > 2", "policy_record_version_at_action >= 2"),
            ("updated_at IS NULL", "updated_at IS NOT NULL"),
            ("actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid",
             "actor_worker_id = '00000000-0000-0000-0000-000000000000'::uuid"),
        })
        {
            Assert.Contains(guard, operational.Restored);
            Assert.False(ExistingComparator("procurement.operational_policy_identity_evidence.ck_operational_policy_identity",
                operational.Canonical, operational.Restored.Replace(guard, weakened, StringComparison.Ordinal)));
        }
        var payment = ProcurementRestoreCheckAlternatives.Definitions[
            "procurement.manual_payment_requests.ck_manual_payment_values"];
        Assert.Contains("'Draft'", payment.Restored);
        Assert.False(ExistingComparator("procurement.manual_payment_requests.ck_manual_payment_values",
            payment.Canonical, payment.Restored.Replace("'Draft'", "'UnreviewedState'", StringComparison.Ordinal)));
        var goodsReturn = ProcurementRestoreCheckAlternatives.Definitions[
            "procurement.principal_goods_return_receipts.ck_pgret_receipt"];
        Assert.Contains("'Cancel'", goodsReturn.Restored);
        Assert.False(ExistingComparator("procurement.principal_goods_return_receipts.ck_pgret_receipt",
            goodsReturn.Canonical, goodsReturn.Restored.Replace("'Cancel'", "'UnreviewedOperation'", StringComparison.Ordinal)));
    }

    [Fact]
    public void Manual_payment_full_catalog_fingerprint_substitutes_only_nine_exact_observed_checks()
    {
        Assert.Equal(9, ProcurementManualPaymentGovernanceReadiness.ObservedRestoreChecks.Length);
        var sql = ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks();
        Assert.Contains("ELSE pg_get_constraintdef(c.oid) END", sql);
        foreach (var key in ProcurementManualPaymentGovernanceReadiness.ObservedRestoreChecks)
        {
            var parts = key.Split('.');
            var pair = ProcurementRestoreCheckAlternatives.Definitions[key];
            Assert.Contains("c.contype='c' AND r.nspname='" + parts[0] + "' AND r.relname='" + parts[1]
                + "' AND c.conname='" + parts[2] + "'", sql);
            Assert.Contains(pair.Restored.Replace("'", "''", StringComparison.Ordinal), sql);
            Assert.Contains(pair.Canonical.Replace("'", "''", StringComparison.Ordinal), sql);
        }
        Assert.Equal(1, sql.Split("CASE ", StringSplitOptions.None).Length - 1);
    }

    private static bool ExistingComparator(string key, string canonical, string actual) =>
        key == "procurement.sourcing_command_receipts.ck_rfq_receipt_identity"
            ? canonical == ProcurementSourcingDraftSchemaReadiness.CheckDefinitions["ck_rfq_receipt_identity"]
                && (actual == canonical || ProcurementSourcingDraftSchemaReadiness.MatchesRestoredReceipt(key, canonical, actual))
            : key == "procurement.operational_policy_identity_evidence.ck_operational_policy_identity"
                ? canonical == ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition
                    && (actual == canonical || ProcurementRestoreCheckAlternatives.Matches(key, canonical, actual))
            : key == "procurement.invoice_unit_review_receipts.ck_inv_unit_receipt"
                ? canonical == ProcurementInvoiceUnitReviewSchemaReadiness.CheckDefinitions["ck_inv_unit_receipt"]
                    && (actual == canonical || ProcurementRestoreCheckAlternatives.Matches(key, canonical, actual))
            : key.StartsWith("procurement.manual_payment_", StringComparison.Ordinal)
                ? actual == canonical || ProcurementRestoreCheckAlternatives.Matches(key, canonical, actual)
            : key.StartsWith("procurement.principal_goods_return_", StringComparison.Ordinal)
                ? canonical == ProcurementPrincipalGoodsReturnSchemaCapture.Checks[key]
                    && (actual == canonical || ProcurementRestoreCheckAlternatives.Matches(key, canonical, actual))
            : key.StartsWith("procurement.assignment_authority_evidence.", StringComparison.Ordinal)
                ? ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, canonical, actual)
            : key.StartsWith("procurement.principal_receiving_", StringComparison.Ordinal)
                ? ProcurementPrincipalReceivingPhysicalSchema.MatchesCapturedCheck(key, canonical, actual)
                : ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(key, actual);
}
