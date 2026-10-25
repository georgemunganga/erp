using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// The genuine EF migration defines this model. PostgreSQL's canonical CHECK,
// FK and parent-key definitions still require an independent owner capture.
// Empty physical dictionaries deliberately keep the provider unavailable.
internal static class ProcurementRfqRecommendationSchemaCapture
{
    internal const string MigrationId = "20261025014631_P36HumanRecommendationDraft";
    internal static readonly Type[] Types =
    [
        typeof(PrincipalRfqRecommendationDraftHead),
        typeof(PrincipalRfqRecommendationDraftRevision),
        typeof(PrincipalRfqRecommendationDraftLine),
        typeof(PrincipalRfqRecommendationDraftReceipt)
    ];
    internal static readonly IReadOnlySet<string> TableNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts",
        "procurement.principal_rfq_recommendation_revisions",
        "procurement.principal_rfq_recommendation_lines",
        "procurement.principal_rfq_recommendation_receipts"
    };
    internal static readonly IReadOnlySet<string> CheckNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts.ck_p36_rec_head",
        "procurement.principal_rfq_recommendation_revisions.ck_p36_rec_revision",
        "procurement.principal_rfq_recommendation_lines.ck_p36_rec_line",
        "procurement.principal_rfq_recommendation_receipts.ck_p36_rec_ack"
    };
    internal static readonly IReadOnlySet<string> ForeignNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_opened",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_plan",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_round",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_seal",
        "procurement.principal_rfq_recommendation_revisions.fk_p36_rec_prev",
        "procurement.principal_rfq_recommendation_revisions.fk_p36_rec_revision_head",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_offer",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_revision",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_source_line",
        "procurement.principal_rfq_recommendation_receipts.fk_p36_rec_ack_revision"
    };
    internal static readonly IReadOnlySet<string> ParentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.sourcing_lines.ak_p36_rec_source_line",
        "procurement.principal_rfq_commercial_round_seals.ak_p36_rec_seal",
        "procurement.principal_rfq_commercial_offer_submissions.ak_p36_rec_offer"
    };
    internal static readonly IReadOnlyDictionary<string,string> Checks =
        new Dictionary<string,string>(StringComparer.Ordinal);
    internal static readonly IReadOnlyDictionary<string,string> ForeignKeys =
        new Dictionary<string,string>(StringComparer.Ordinal);
    internal static readonly IReadOnlyDictionary<string,string> ParentKeys =
        new Dictionary<string,string>(StringComparer.Ordinal);

    internal static bool Complete(IReadOnlyDictionary<string,string> checks,
        IReadOnlyDictionary<string,string> foreignKeys,
        IReadOnlyDictionary<string,string> parentKeys) =>
        checks.Count == CheckNames.Count && foreignKeys.Count == ForeignNames.Count
        && parentKeys.Count == ParentNames.Count
        && checks.All(x => CheckNames.Contains(x.Key) && x.Value.StartsWith("CHECK (", StringComparison.Ordinal))
        && foreignKeys.All(x => ForeignNames.Contains(x.Key) && x.Value.StartsWith("FOREIGN KEY (", StringComparison.Ordinal))
        && parentKeys.All(x => ParentNames.Contains(x.Key) && x.Value.StartsWith("UNIQUE (", StringComparison.Ordinal));

    internal static bool Populated => Complete(Checks,ForeignKeys,ParentKeys);
    internal static readonly ProcurementPrincipalCommercialSchemaInspection.Capture Physical =
        new(MigrationId,Checks,ForeignKeys,ParentKeys);
}
