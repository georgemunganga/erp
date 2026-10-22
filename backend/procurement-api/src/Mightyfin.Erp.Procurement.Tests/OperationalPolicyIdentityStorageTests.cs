using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class OperationalPolicyIdentityStorageTests
{
    private const string Maker = "11111111-1111-4111-8111-111111111111";
    private static readonly Guid Worker = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset At = new(2026, 10, 22, 4, 0, 0, TimeSpan.Zero);

    private static OperationalPolicyWorkerIdentityEvidence Origin(string key)
    {
        var policy = new PolicyVersion
        {
            TenantId = "operational-policy-storage", LegalEntityId = Guid.NewGuid(),
            PolicyKey = key, PolicyVersionNumber = 1, State = "Draft", Version = 1,
            CreatedBy = Maker, CreatedAt = At, RulesJson = "{}", EffectiveFrom = At.AddDays(1),
        };
        return OperationalPolicyWorkerIdentity.Capture(policy, "Created", Maker, Worker, 1, At);
    }

    [Theory]
    [InlineData("RECEIVING")]
    [InlineData("INVOICE_MATCHING")]
    [InlineData("INVOICE_UNIT_REVIEW")]
    public void Typed_conversion_preserves_the_entire_verified_envelope(string key)
    {
        var origin = Origin(key);
        var row = OperationalPolicyIdentityStorage.ToRecord(origin);
        Assert.Equal(origin, OperationalPolicyIdentityStorage.Read(row));
        Assert.Equal(row.ActionAt, row.CreatedAt);
        Assert.Equal(row.ActorPrincipalId, row.CreatedBy);
        Assert.Equal(1, row.Version);
    }

    [Theory]
    [InlineData("creator")]
    [InlineData("time")]
    [InlineData("revision")]
    [InlineData("updated")]
    public void Modified_storage_metadata_cannot_be_adopted_as_original_action_history(string fault)
    {
        var row = OperationalPolicyIdentityStorage.ToRecord(Origin("RECEIVING"));
        if (fault == "creator") row.CreatedBy = "33333333-3333-4333-8333-333333333333";
        if (fault == "time") row.CreatedAt = At.AddMinutes(1);
        if (fault == "revision") row.Version = 2;
        if (fault == "updated") row.UpdatedAt = At.AddMinutes(1);
        var error = Assert.Throws<InvalidOperationException>(() => OperationalPolicyIdentityStorage.Read(row));
        Assert.Equal("OperationalPolicyIdentityStorageInvalid", error.Message);
    }

    [Fact]
    public void Changing_the_policy_key_without_a_new_digest_is_detected_before_storage()
    {
        var origin = Origin("RECEIVING");
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyIdentityStorage.ToRecord(
            origin with { PolicyKey = "INVOICE_MATCHING" }));
    }
}
