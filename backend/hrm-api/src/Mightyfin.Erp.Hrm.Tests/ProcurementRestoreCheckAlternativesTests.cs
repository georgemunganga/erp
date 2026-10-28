using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRestoreCheckAlternativesTests
{
    public static TheoryData<string, string, string> ExactObservedCases => new()
    {
        { "procurement.assignment_authority_evidence.ck_pr_assignment_authority_identity", "c696e8448b5ef3278ab815ff0df14a7dcd7d2377934b02cefcf65d4abb1620dd", "fcc6bff108627dc856af0b803cc168ea8d1e611473386ce162d6f6b296dde27a" },
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
    };

    [Theory]
    [MemberData(nameof(ExactObservedCases))]
    public void Only_exact_qualified_canonical_and_restored_pair_is_admitted(string key, string canonicalSha256, string restoredSha256)
    {
        Assert.Equal(11, ProcurementRestoreCheckAlternatives.Definitions.Count);
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
    }

    private static bool ExistingComparator(string key, string canonical, string actual) =>
        key == "procurement.sourcing_command_receipts.ck_rfq_receipt_identity"
            ? canonical == ProcurementSourcingDraftSchemaReadiness.CheckDefinitions["ck_rfq_receipt_identity"]
                && (actual == canonical || ProcurementSourcingDraftSchemaReadiness.MatchesRestoredReceipt(key, canonical, actual))
            : key.StartsWith("procurement.assignment_authority_evidence.", StringComparison.Ordinal)
                ? ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, canonical, actual)
            : key.StartsWith("procurement.principal_receiving_", StringComparison.Ordinal)
                ? ProcurementPrincipalReceivingPhysicalSchema.MatchesCapturedCheck(key, canonical, actual)
                : ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(key, actual);
}
