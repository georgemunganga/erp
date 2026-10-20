using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Source-authored tests for the pure receiving increment. Runtime transactions, policy
/// resolution, permissions, migrations and database concurrency remain separate release gates.</summary>
public sealed class ControlledReceivingTests
{
    [Fact]
    public void Physical_receipt_is_not_acceptance_and_inspection_qualifies_only_its_passed_quantity()
    {
        var f = new Fixture();
        var physical = f.Record(10, 1);
        Assert.Empty(f.Ledger.Effects);
        Assert.Equal(0, ControlledReceiving.Balance(f.Ledger, physical.GoodsReceiptLineId).AvailableQuantity);
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 1));
        f.Inspect(physical, 6);
        var accepted = f.Accept(physical, 4); f.Append(accepted);
        Assert.Equal(0.02m, accepted.NetAmountDelta);
        var balance = ControlledReceiving.Balance(f.Ledger, physical.GoodsReceiptLineId);
        Assert.Equal(4, balance.AvailableQuantity);
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 3));
        f.Append(f.Accept(physical, 2));
        Assert.Equal(6, ControlledReceiving.Balance(f.Ledger, physical.GoodsReceiptLineId).AvailableQuantity);
        Assert.Equal(10, physical.ReceivedQuantity); // original evidence is unchanged
    }

    [Fact]
    public void Inspector_assignment_and_canonical_worker_independence_are_required()
    {
        var f = new Fixture(); var physical = f.Record(2);
        var aliasOfReceiver = f.Inspector with { WorkerId = physical.ReceiverWorkerId, PrincipalId = "another-account" };
        var policy = f.Policy with { InspectorWorkerIds = new HashSet<Guid> { physical.ReceiverWorkerId } };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.InspectGoods(f.Source, policy,
            aliasOfReceiver, f.Ledger, physical.Id, 2, "INSPECT", Guid.NewGuid(), f.Now));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.InspectGoods(f.Source, f.Policy,
            f.Inspector with { WorkerId = Guid.NewGuid() }, f.Ledger, physical.Id, 2, "INSPECT", Guid.NewGuid(), f.Now));
        f.Inspect(physical, 1);
        Assert.Throws<InvalidOperationException>(() => f.Inspect(physical, 2));
    }

    [Fact]
    public void Rejected_inspection_never_certifies_and_same_physical_line_is_not_recorded_twice()
    {
        var f = new Fixture(); var physical = f.Record(3); f.Inspect(physical, 0);
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 1));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.RecordGoods(f.Source, f.Policy,
            f.Receiver, f.Ledger, physical.GoodsReceiptId, physical.GoodsReceiptLineId, f.Site, 3, 0,
            "ANOTHER-DELIVERY-NOTE", Guid.NewGuid(), f.Now));
    }

    [Fact]
    public void Order_capacity_is_cumulative_across_receipts_and_excess_can_only_remain_held()
    {
        var f = new Fixture(); var first = f.Record(8); f.Inspect(first, 8); f.Append(f.Accept(first, 8));
        var excess = f.Record(8); f.Inspect(excess, 8);
        Assert.Throws<InvalidOperationException>(() => f.Accept(excess, 3));
        f.Append(f.Accept(excess, 2));
        Assert.Equal(10, f.Ledger.Effects.Sum(x => x.QuantityDelta));
        Assert.Equal(16, f.Ledger.Observations.Sum(x => x.ReceivedQuantity));
    }

    [Fact]
    public void Service_requires_assigned_owner_exact_sealed_net_basis_and_stable_deliverable_identity()
    {
        var f = new Fixture(ReceivingKind.Service);
        ReceivingAcceptanceEffect Certify(Guid id, decimal quantity, decimal amount, string deliverable) =>
            ControlledReceiving.CertifyService(f.Source, f.Policy, f.Certifier, f.Ledger, id,
                quantity, amount, deliverable, new(2026, 10, 1), new(2026, 10, 20), "SERVICE-PROOF", Guid.NewGuid(), f.Now);
        var record = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Certify(record, 2, 0.02m, "MILESTONE-1"));
        var accepted = Certify(record, 2, 0.01m, "MILESTONE-1"); f.Append(accepted);
        Assert.Null(accepted.PhysicalObservationId);
        Assert.Equal(0.01m, ControlledReceiving.Balance(f.Ledger, record).AvailableNetAmount);
        Assert.Throws<InvalidOperationException>(() => Certify(Guid.NewGuid(), 1, 0.005m, "MILESTONE-1"));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.CertifyService(f.Source,
            f.Policy with { ServiceOwnerWorkerIds = new HashSet<Guid>() }, f.Certifier, f.Ledger, Guid.NewGuid(),
            1, 0.005m, "MILESTONE-2", new(2026, 10, 1), new(2026, 10, 20), "PROOF", Guid.NewGuid(), f.Now));
        Assert.Throws<InvalidOperationException>(() => f.Record(1));
    }

    [Fact]
    public void Return_and_administrative_reversal_reduce_projection_once_and_keep_original_fragments()
    {
        var f = new Fixture(); var physical = f.Record(8); f.Inspect(physical, 8);
        var original = f.Accept(physical, 8); f.Append(original);
        var returned = f.Correct(original, 2, AcceptanceEffectKind.ReturnDispatched); f.Append(returned);
        var reversed = f.Correct(original, 1, AcceptanceEffectKind.AdministrativeReversal); f.Append(reversed);
        var balance = ControlledReceiving.Balance(f.Ledger, original.AcceptanceRecordId);
        Assert.Equal(7, balance.AcceptedQuantity); Assert.Equal(0.035m, balance.AcceptedNetAmount);
        Assert.Equal(2, balance.ReturnedQuantity); Assert.Equal(0.01m, balance.ReturnedNetAmount);
        Assert.Equal(5, balance.AvailableQuantity); Assert.Equal(0.025m, balance.AvailableNetAmount);
        Assert.False(balance.Reversed); Assert.Equal(8, original.QuantityDelta);
        Assert.NotNull(returned.CorrectionEvidence); Assert.Equal(original.Id, reversed.OriginalAcceptanceEffectId);
        Assert.Throws<InvalidOperationException>(() => f.Correct(original, 6, AcceptanceEffectKind.AdministrativeReversal));
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 1)); // return is not fresh physical stock
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("stale")]
    [InlineData("wrong-source")]
    [InlineData("consumed")]
    [InlineData("value-consumed")]
    [InlineData("posting-bound")]
    [InlineData("owner-unknown")]
    [InlineData("owner-blocked")]
    [InlineData("owner-unbound")]
    public void Negative_effects_require_complete_current_unconsumed_owner_evidence(string failure)
    {
        var f = new Fixture(); var physical = f.Record(2); f.Inspect(physical, 2);
        var original = f.Accept(physical, 2); f.Append(original);
        var key = Guid.NewGuid(); var consumption = f.Consumption(original, key);
        consumption = failure switch
        {
            "unknown" => consumption with { Complete = false },
            "stale" => consumption with { AcceptanceSequence = consumption.AcceptanceSequence - 1 },
            "wrong-source" => consumption with { Source = consumption.Source with { IssuedVersionId = Guid.NewGuid() } },
            "consumed" => consumption with { ConsumedQuantity = 2 },
            "value-consumed" => consumption with { ConsumedNetAmount = 0.01m },
            "posting-bound" => consumption with { PostingBound = true },
            "owner-unknown" => consumption with { OwnerState = ReceivingOwnerCorrectionState.Unknown },
            "owner-blocked" => consumption with { OwnerState = ReceivingOwnerCorrectionState.Blocked },
            _ => consumption with { CorrectionEffectKey = Guid.NewGuid() },
        };
        Assert.Throws<InvalidOperationException>(() => f.Correct(original, 1,
            AcceptanceEffectKind.AdministrativeReversal, key, consumption));
        Assert.Single(f.Ledger.Effects); Assert.Equal(2, original.QuantityDelta);
    }

    [Fact]
    public void Pending_return_or_alias_of_original_certifier_cannot_reduce_entitlement()
    {
        var f = new Fixture(); var physical = f.Record(2); f.Inspect(physical, 2);
        var original = f.Accept(physical, 2); f.Append(original);
        var key = Guid.NewGuid(); var authority = f.Authority(original, key, AcceptanceEffectKind.ReturnDispatched, 1);
        Assert.Throws<InvalidOperationException>(() => f.Correct(original, 1, AcceptanceEffectKind.ReturnDispatched,
            key, authority: authority with { DispatchVerified = false }));
        Assert.Throws<InvalidOperationException>(() => f.Correct(original, 1, AcceptanceEffectKind.ReturnDispatched,
            key, authority: authority with { CurrentAndGranted = false }));
        var alias = f.Corrector with { PrincipalId = "alias-account", WorkerId = original.ActorWorkerId };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.CorrectAcceptance(f.Source,
            f.Policy with { CorrectionWorkerIds = new HashSet<Guid> { alias.WorkerId } }, alias, f.Ledger,
            f.Consumption(original, key), authority, original.Id, 1, AcceptanceEffectKind.ReturnDispatched,
            "DAMAGED", "RETURN-PROOF", key, f.Now));
    }

    [Fact]
    public void Two_competing_results_from_same_sequence_cannot_form_a_complete_ledger()
    {
        var f = new Fixture(); var physical = f.Record(10); f.Inspect(physical, 10);
        var first = f.Accept(physical, 6); var second = f.Accept(physical, 6);
        Assert.Equal(first.LedgerSequence, second.LedgerSequence);
        var staleMerge = f.Ledger with { Sequence = f.Ledger.Sequence + 2, Effects = new[] { first, second } };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.ValidateLedger(staleMerge));
        f.Append(first);
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 6));
    }

    [Fact]
    public void Consumed_balance_is_preserved_after_partial_correction_and_full_void_is_distinct_from_return()
    {
        var f = new Fixture(); var physical = f.Record(3); f.Inspect(physical, 3);
        var original = f.Accept(physical, 3); f.Append(original);
        var key = Guid.NewGuid(); var consumption = f.Consumption(original, key) with
        { ConsumedQuantity = 1, ConsumedNetAmount = 0.005m };
        f.Append(f.Correct(original, 2, AcceptanceEffectKind.AdministrativeReversal, key, consumption));
        var partial = ControlledReceiving.Balance(f.Ledger, original.AcceptanceRecordId);
        Assert.Equal(1, partial.AvailableQuantity); Assert.False(partial.Reversed);
        f.Append(f.Correct(original, 1, AcceptanceEffectKind.AdministrativeReversal));
        var full = ControlledReceiving.Balance(f.Ledger, original.AcceptanceRecordId);
        Assert.True(full.Reversed); Assert.Equal(0, full.AvailableNetAmount); Assert.Equal(0, full.ReturnedQuantity);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("selected-branch")]
    [InlineData("branch")]
    [InlineData("site")]
    [InlineData("inactive")]
    [InlineData("permission")]
    public void Scoped_current_actor_is_required_even_for_organisation_wide_roles(string failure)
    {
        var f = new Fixture(); var actor = f.Receiver;
        actor = failure switch
        {
            "tenant" => actor with { TenantId = "foreign" },
            "company" => actor with { LegalEntityId = Guid.NewGuid() },
            "selected-branch" => actor with { SelectedBranchId = Guid.NewGuid(), OrganisationWide = true },
            "branch" => actor with { OrganisationWide = false, BranchIds = new HashSet<Guid>() },
            "site" => actor with { SiteIds = new HashSet<Guid>() },
            "inactive" => actor with { Active = false },
            _ => actor with { Permissions = new HashSet<string>() },
        };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.RecordGoods(f.Source, f.Policy,
            actor, f.Ledger, Guid.NewGuid(), Guid.NewGuid(), f.Site, 1, 0, "DELIVERY", Guid.NewGuid(), f.Now));
    }

    [Fact]
    public void Missing_published_policy_and_alias_of_buyer_do_not_gain_certification_from_permission()
    {
        var f = new Fixture(); var physical = f.Record(1); f.Inspect(physical, 1);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.AcceptGoods(f.Source,
            f.Policy with { Published = false }, f.Certifier, f.Ledger, physical.Id, 1, "PROOF", Guid.NewGuid(), f.Now));
        var buyerAlias = f.Certifier with { PrincipalId = "buyer-second-account", WorkerId = f.Source.BuyerWorkerId };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.AcceptGoods(f.Source,
            f.Policy, buyerAlias, f.Ledger, physical.Id, 1, "PROOF", Guid.NewGuid(), f.Now));
    }

    [Theory]
    [InlineData("issued-hash")]
    [InlineData("line-id")]
    [InlineData("request-line-id")]
    [InlineData("unit")]
    [InlineData("category")]
    [InlineData("kind")]
    [InlineData("currency")]
    [InlineData("price")]
    [InlineData("price-basis")]
    [InlineData("classification")]
    public void Sealed_source_binds_exact_ids_classification_and_net_price_not_names(string failure)
    {
        var f = new Fixture(); var p = f.Source.Pin;
        var changed = failure switch
        {
            "issued-hash" => p with { IssuedSnapshotSha256 = new string('0', 64) },
            "line-id" => p with { PurchaseOrderLineId = Guid.NewGuid() },
            "request-line-id" => p with { PurchaseRequestLineId = Guid.NewGuid() },
            "unit" => p with { UnitCode = "BOX" },
            "category" => p with { CategoryCode = "OTHER" },
            "kind" => p with { Kind = ReceivingKind.Service },
            "currency" => p with { CurrencyCode = "USD" },
            "price" => p with { NetUnitPrice = 0.006m },
            "price-basis" => p with { NetPriceBasis = "GrossAmountDividedByQuantity" },
            _ => p with { ClassificationReference = "CURRENT-ITEM-NAME" },
        };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Source with { Pin = changed }));
        ControlledReceiving.VerifySource(f.Source);
    }

    [Fact]
    public void Delivery_unknown_is_not_issued_authority_and_effect_keys_are_not_retry_authority()
    {
        var f = new Fixture(); var d = f.Source.Delivery;
        var unknown = new PurchaseOrderDeliveryEvidence { TenantId = d.TenantId, LegalEntityId = d.LegalEntityId,
            IssuedVersionId = d.IssuedVersionId, VerifiedByWorkerId = d.VerifiedByWorkerId,
            EvidenceReference = d.EvidenceReference, Outcome = "Unknown" };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Source with { Delivery = unknown }));
        var physical = f.Record(1); f.Inspect(physical, 1); var accepted = f.Accept(physical, 1); f.Append(accepted);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.AcceptGoods(f.Source, f.Policy,
            f.Certifier, f.Ledger, physical.Id, 1, "PROOF", accepted.EffectKey, f.Now));
    }

    [Fact]
    public void Exact_fractional_rate_is_not_rounded_per_partial_return_and_timestamps_use_database_microseconds()
    {
        var f = new Fixture(); var physical = f.Record(2); f.Inspect(physical, 2);
        var original = f.Accept(physical, 2); f.Append(original);
        f.Append(f.Correct(original, 1, AcceptanceEffectKind.ReturnDispatched));
        f.Append(f.Correct(original, 1, AcceptanceEffectKind.ReturnDispatched));
        var balance = ControlledReceiving.Balance(f.Ledger, original.AcceptanceRecordId);
        Assert.Equal(0.01m, balance.ReturnedNetAmount); Assert.Equal(0, balance.AvailableNetAmount);
        Assert.False(balance.Reversed); Assert.Equal(0, physical.RecordedAt.UtcTicks % 10);
    }

    [Fact]
    public void Explicit_no_inspection_policy_still_requires_independent_permitted_certifier()
    {
        var f = new Fixture(); var policy = f.Policy with { InspectionRequired = false, ChecklistReference = null };
        var physical = ControlledReceiving.RecordGoods(f.Source, policy, f.Receiver, f.Ledger,
            Guid.NewGuid(), Guid.NewGuid(), f.Site, 2, 0, "DELIVERY", Guid.NewGuid(), f.Now);
        f.Append(physical);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.AcceptGoods(f.Source, policy, f.Receiver,
            f.Ledger, physical.Id, 1, "CERTIFY", Guid.NewGuid(), f.Now));
        var acceptance = ControlledReceiving.AcceptGoods(f.Source, policy, f.Certifier,
            f.Ledger, physical.Id, 1, "CERTIFY", Guid.NewGuid(), f.Now);
        Assert.Null(acceptance.InspectionEvidenceId); f.Append(acceptance);
        Assert.Equal(1, ControlledReceiving.Balance(f.Ledger, physical.GoodsReceiptLineId).AvailableQuantity);
    }

    [Fact]
    public void Duplicated_line_identity_or_unmodelled_discount_is_rejected_even_with_matching_snapshot_digest()
    {
        var f = new Fixture();
        using var json = JsonDocument.Parse(f.Source.Issued.CommercialSnapshotJson);
        var line = json.RootElement.GetProperty("lines")[0].GetRawText();
        var duplicated = f.Source.Issued.CommercialSnapshotJson.Replace("[" + line + "]", "[" + line + "," + line + "]", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Reseal(duplicated)));
        var discountLine = line[..^1] + ",\"discount\":\"0.001\"}";
        var discount = f.Source.Issued.CommercialSnapshotJson.Replace(line, discountLine, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Reseal(discount)));
        var lossyPrice = f.Source.Issued.CommercialSnapshotJson.Replace("\"0.005\"", "\"0.0050000000000000000000000000000000001\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Reseal(lossyPrice)));
    }

    [Fact]
    public void Quantity_precision_is_explicit_and_partial_negative_effect_cannot_consume_another_positive_fragment()
    {
        var f = new Fixture(); var physical = f.Record(4); f.Inspect(physical, 4);
        Assert.Throws<InvalidOperationException>(() => f.Accept(physical, 0.00001m));
        var first = f.Accept(physical, 1); f.Append(first);
        var second = f.Accept(physical, 3); f.Append(second);
        Assert.Throws<InvalidOperationException>(() => f.Correct(first, 2, AcceptanceEffectKind.ReturnDispatched));
        f.Append(f.Correct(second, 2, AcceptanceEffectKind.ReturnDispatched));
        Assert.Equal(2, ControlledReceiving.Balance(f.Ledger, physical.GoodsReceiptLineId).AvailableQuantity);
    }

    [Fact]
    public void Retained_request_participants_cannot_certify_using_another_account_and_old_issued_authority_is_denied()
    {
        var f = new Fixture(); var physical = f.Record(1); f.Inspect(physical, 1);
        var workers = JsonSerializer.Deserialize<Guid[]>(f.Source.Submission.ParticipantWorkersJson)!;
        var requester = workers.Single(x => x != f.Source.BuyerWorkerId);
        var alias = f.Certifier with { PrincipalId = "requester-other-account", WorkerId = requester };
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.AcceptGoods(f.Source, f.Policy, alias,
            f.Ledger, physical.Id, 1, "CERTIFY", Guid.NewGuid(), f.Now));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(f.Source with { CurrentIssuedAuthority = false }));
    }

    [Fact]
    public void A_foreign_scoped_row_or_truncated_ledger_is_never_zero_available_consumption()
    {
        var f = new Fixture(); var physical = f.Record(2); f.Inspect(physical, 2); f.Append(f.Accept(physical, 2));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.Balance(f.Ledger with { Complete = false }, physical.GoodsReceiptLineId));
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.Balance(f.Ledger with { Effects = Array.Empty<ReceivingAcceptanceEffect>() }, physical.GoodsReceiptLineId));
        var originalTenant = physical.TenantId;
        physical.TenantId = "foreign"; // persistence must also reject modifying these final rows
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.ValidateLedger(f.Ledger));
        physical.TenantId = originalTenant;
        ControlledReceiving.ValidateLedger(f.Ledger);
    }

    private sealed class Fixture
    {
        public DateTimeOffset Now { get; } = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
        public Guid Site { get; } = Guid.NewGuid();
        public ReceivingSourceProjection Source { get; }
        public ReceivingPolicyProjection Policy { get; }
        public ReceivingActorProjection Receiver { get; }
        public ReceivingActorProjection Inspector { get; }
        public ReceivingActorProjection Certifier { get; }
        public ReceivingActorProjection Corrector { get; }
        public ReceivingLedger Ledger { get; private set; }
        public Fixture(ReceivingKind kind = ReceivingKind.Goods)
        {
            var entity = Guid.NewGuid(); var branch = Guid.NewGuid(); var order = Guid.NewGuid();
            var issuedId = Guid.NewGuid(); var line = Guid.NewGuid(); var requestLine = Guid.NewGuid();
            var buyer = Guid.NewGuid(); var requester = Guid.NewGuid(); var submissionId = Guid.NewGuid();
            var category = kind == ReceivingKind.Goods ? "GOODS" : "CONSULTING";
            var snapshot = JsonSerializer.Serialize(new { schemaVersion = 1, TenantId = "test", LegalEntityId = entity,
                Id = order, revision = 1, BuyerSubjectId = "buyer", BranchRef = branch, CurrencyCode = "ZMW",
                lines = new[] { new { Id = line, PurchaseRequestLineId = requestLine, UnitCode = "EA", quantity = "10", unitPrice = "0.005", tax = "1.5" } } });
            var sourceJson = JsonSerializer.Serialize(new[] { new { orderLineId = line, requestLineId = requestLine,
                CategoryCode = category, PurchaseType = kind.ToString(), UnitCode = "EA", quantity = "10" } });
            var sourceHash = ProcurementEvidenceEncoding.PolicyJsonDigest("{\"sources\":" + sourceJson + "}");
            var classification = JsonSerializer.Serialize(new { tenantId = "test", legalEntityId = entity,
                issuedVersionId = issuedId, orderLineId = line, sourceEvidenceSha256 = sourceHash,
                reference = "CLASSIFICATION-V1", kind = kind.ToString(), netPriceBasis = ControlledReceiving.NetPriceBasis });
            var workers = JsonSerializer.Serialize(new[] { buyer, requester }.Order().ToArray());
            var identityHash = PurchaseOrderWorkerIdentity.Digest(new PurchaseOrder { Id = order, TenantId = "test",
                LegalEntityId = entity, BuyerSubjectId = "buyer" }, 1, Guid.Empty, buyer, workers);
            var hash = ProcurementEvidenceEncoding.Digest(snapshot);
            var pin = new ReceivingSourcePin("test", entity, branch, order, issuedId, line, requestLine, 1,
                hash, sourceHash, "CLASSIFICATION-V1", ProcurementEvidenceEncoding.PolicyJsonDigest(classification),
                kind, category, kind.ToString(), "ZMW", "EA", 10, 0.005m, ControlledReceiving.NetPriceBasis);
            Source = new(new() { Id = issuedId, TenantId = "test", LegalEntityId = entity,
                    PurchaseOrderId = order, SubmissionId = submissionId, RevisionNumber = 1,
                    CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = hash, ParticipantWorkersJson = workers, AuthorizedAt = Now.AddHours(-1) },
                new() { Id = submissionId, TenantId = "test", LegalEntityId = entity,
                    PurchaseOrderId = order, CommercialRevision = 1, BuyerWorkerId = buyer,
                    CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = hash, SourceEvidenceJson = sourceJson,
                    ParticipantWorkersJson = workers, WorkerIdentitySha256 = identityHash, SubmittedAt = Now.AddHours(-2) },
                new() { TenantId = "test", LegalEntityId = entity, IssuedVersionId = issuedId,
                    Outcome = "Delivered", VerifiedByWorkerId = Guid.NewGuid(), EvidenceReference = "VERIFIED-DELIVERY", ObservedAt = Now.AddMinutes(-1) },
                pin, buyer, classification, true);
            var permissions = new HashSet<string> { ControlledReceiving.RecordPermission, ControlledReceiving.InspectPermission,
                ControlledReceiving.AcceptPermission, ControlledReceiving.CertifyPermission, ControlledReceiving.ReversePermission,
                ControlledReceiving.ServiceReversePermission, ControlledReceiving.ReturnPermission };
            ReceivingActorProjection Actor(string name) => new(name, Guid.NewGuid(), true, "test", entity, branch,
                false, new HashSet<Guid> { branch }, new HashSet<Guid> { Site }, permissions);
            Receiver = Actor("receiver"); Inspector = Actor("inspector"); Certifier = Actor("certifier"); Corrector = Actor("corrector");
            Policy = new(Guid.NewGuid(), "test", entity, branch, category, kind, true, Now.AddDays(-1), Now.AddDays(1),
                kind == ReceivingKind.Goods, kind == ReceivingKind.Goods ? "CHECKLIST-V1" : null,
                false, false, false, true, new HashSet<Guid> { Inspector.WorkerId },
                new HashSet<Guid> { Certifier.WorkerId }, new HashSet<Guid> { Corrector.WorkerId }, new HashSet<Guid> { Site });
            Ledger = new(pin, 0, true, Array.Empty<GoodsPhysicalObservation>(), Array.Empty<GoodsInspectionEvidence>(), Array.Empty<ReceivingAcceptanceEffect>());
        }
        public ReceivingSourceProjection Reseal(string snapshot)
        {
            var hash = ProcurementEvidenceEncoding.Digest(snapshot); var i = Source.Issued; var s = Source.Submission;
            return Source with
            {
                Pin = Source.Pin with { IssuedSnapshotSha256 = hash },
                Issued = new() { Id = i.Id, TenantId = i.TenantId, LegalEntityId = i.LegalEntityId,
                    PurchaseOrderId = i.PurchaseOrderId, SubmissionId = i.SubmissionId, RevisionNumber = i.RevisionNumber,
                    CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = hash, ParticipantWorkersJson = i.ParticipantWorkersJson, AuthorizedAt = i.AuthorizedAt },
                Submission = new() { Id = s.Id, TenantId = s.TenantId, LegalEntityId = s.LegalEntityId,
                    PurchaseOrderId = s.PurchaseOrderId, CommercialRevision = s.CommercialRevision, BuyerWorkerId = s.BuyerWorkerId,
                    CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = hash, SourceEvidenceJson = s.SourceEvidenceJson,
                    CoreApprovalCaseId = s.CoreApprovalCaseId, ParticipantWorkersJson = s.ParticipantWorkersJson, WorkerIdentitySha256 = s.WorkerIdentitySha256, SubmittedAt = s.SubmittedAt },
            };
        }
        public GoodsPhysicalObservation Record(decimal quantity, decimal rejected = 0)
        {
            var result = ControlledReceiving.RecordGoods(Source, Policy, Receiver, Ledger, Guid.NewGuid(), Guid.NewGuid(),
                Site, quantity, rejected, "DELIVERY-NOTE", Guid.NewGuid(), Now); Append(result); return result;
        }
        public void Inspect(GoodsPhysicalObservation observation, decimal qualified) => Append(
            ControlledReceiving.InspectGoods(Source, Policy, Inspector, Ledger, observation.Id, qualified, "INSPECTION", Guid.NewGuid(), Now));
        public ReceivingAcceptanceEffect Accept(GoodsPhysicalObservation observation, decimal quantity) =>
            ControlledReceiving.AcceptGoods(Source, Policy, Certifier, Ledger, observation.Id, quantity, "ACCEPTANCE", Guid.NewGuid(), Now);
        public ReceivingConsumptionProjection Consumption(ReceivingAcceptanceEffect original, Guid key)
        {
            var balance = ControlledReceiving.Balance(Ledger, original.AcceptanceRecordId);
            return new(Source.Pin, original.AcceptanceRecordId, Ledger.Sequence, balance.EffectsSha256,
                true, "MATCHER-COMPLETE", 0, 0, false, ReceivingOwnerCorrectionState.Unprocessed, key, "OWNER-CONFIRMED-UNPROCESSED");
        }
        public ReceivingCorrectionAuthorityProjection Authority(ReceivingAcceptanceEffect original, Guid key,
            AcceptanceEffectKind kind, decimal quantity) => new(Source.Pin, original.Id, key, kind, true, quantity,
                Corrector.WorkerId, "AUTHORITY-DECISION", true, "VERIFIED-PHYSICAL-DISPATCH");
        public ReceivingAcceptanceEffect Correct(ReceivingAcceptanceEffect original, decimal quantity, AcceptanceEffectKind kind,
            Guid? key = null, ReceivingConsumptionProjection? consumption = null, ReceivingCorrectionAuthorityProjection? authority = null)
        {
            var effectKey = key ?? Guid.NewGuid();
            return ControlledReceiving.CorrectAcceptance(Source, Policy, Corrector, Ledger, consumption ?? Consumption(original, effectKey),
                authority ?? Authority(original, effectKey, kind, quantity), original.Id, quantity, kind,
                "DOCUMENTED-REASON", "CORRECTION-PROOF", effectKey, Now);
        }
        public void Append(ProcurementRecord record)
        {
            Ledger = record switch
            {
                GoodsPhysicalObservation physical => Ledger with { Sequence = Ledger.Sequence + 1, Observations = Ledger.Observations.Append(physical).ToArray() },
                GoodsInspectionEvidence inspection => Ledger with { Sequence = Ledger.Sequence + 1, Inspections = Ledger.Inspections.Append(inspection).ToArray() },
                ReceivingAcceptanceEffect effect => Ledger with { Sequence = Ledger.Sequence + 1, Effects = Ledger.Effects.Append(effect).ToArray() },
                _ => throw new InvalidOperationException(),
            };
            ControlledReceiving.ValidateLedger(Ledger);
        }
    }
}
