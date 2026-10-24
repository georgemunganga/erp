using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialIndexPredicateTests
{
    [Theory]
    [InlineData("ux_source_policy_identity_created", "stage = 'Created'", "((stage)::text = 'Created'::text)")]
    [InlineData("ux_source_policy_identity_published", "stage = 'Published'", "((stage)::text = 'Published'::text)")]
    public void Identity_partial_indexes_use_the_existing_exact_Postgres_capture(
        string name, string model, string captured)
    {
        Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(SourcingPolicyIdentityEvidence), name, model, out var actual));
        Assert.Equal(captured, actual);
    }

    [Theory]
    [InlineData("ux_source_policy_identity_created", null)]
    [InlineData("ux_source_policy_identity_created", "stage = 'Published'")]
    [InlineData("ux_source_policy_identity_published", "stage = 'Created'")]
    [InlineData("unknown_partial", "stage = 'Created'")]
    public void Missing_changed_or_unknown_partial_filter_stays_unavailable(string name, string? filter)
    {
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(SourcingPolicyIdentityEvidence), name, filter, out _));
    }

    [Fact]
    public void Identity_capture_cannot_authorize_another_entity_filter()
    {
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(SourcingEvent), "ux_source_policy_identity_created", "stage = 'Created'", out _));
    }

    [Fact]
    public void Ordinary_unfiltered_index_still_requires_no_predicate()
    {
        Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(SourcingEvent), "ordinary", null, out var predicate));
        Assert.Null(predicate);
    }
}
