using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestApprovalPinningTests
{
    [Theory]
    [InlineData("MakerWorkerId")]
    [InlineData("RequesterWorkerId")]
    [InlineData("WorkerIdentitySha256")]
    [InlineData("Legacy")]
    public void Missing_or_modified_retained_worker_identity_never_authorizes_old_or_changed_pins(string field)
    {
        var f = new Fixture(); var pin = f.Pin(); f.Link(pin);
        Assert.True(PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin));
        var json = JsonNode.Parse(JsonSerializer.Serialize(pin))!;
        if (field == "Legacy")
        {
            json["SnapshotSchemaVersion"] = 1; json["MakerWorkerId"] = null;
            json["RequesterWorkerId"] = null; json["WorkerIdentitySha256"] = null;
        }
        else if (field == "WorkerIdentitySha256") json[field] = new string('f', 64);
        else json[field] = Guid.NewGuid().ToString("D");
        var changed = JsonSerializer.Deserialize<PurchaseRequestApprovalSubmission>(json.ToJsonString())!;
        Assert.False(PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(changed));
        Assert.Contains("PinnedWorkerIdentityMissingOrInvalid", f.Verify(changed).Blockers);
    }

    [Fact]
    public void Principal_owned_request_cannot_reuse_legacy_worker_approval_pins()
    {
        var f = new Fixture(); var pin = f.Pin(); f.Link(pin);
        Assert.True(f.Verify(pin).IsValid);
        f.Request.RequesterPrincipalId = Guid.NewGuid();
        Assert.Contains("PrincipalApprovalEvidenceRequired", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Missing_optional_employee_never_becomes_fabricated_legacy_approval_identity()
    {
        var f = new Fixture(); var pin = f.Pin(); f.Link(pin);
        f.Request.RequesterWorkerId = null;
        Assert.Contains("InvalidSource", f.Verify(pin).Blockers);
        Assert.Contains("PinnedWorkerIdentityMissingOrInvalid", f.Verify(pin).Blockers);
        Assert.Throws<ArgumentException>(() => PurchaseRequestApprovalPinning.WorkerIdentityDigest(
            "test", Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "maker", Guid.NewGuid(), null));
    }

    [Fact]
    public void Published_successor_closing_prior_period_does_not_strand_pinned_pending_case()
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        // Policy publication leaves the old row Published and closes only its future
        // selection interval. It does not rewrite the rule/version already submitted.
        f.Policy.EffectiveTo = f.Now.AddDays(1);
        f.Policy.Version++;
        f.Request.UpdatedAt = f.Now.AddDays(2); // Case work can continue past the successor start.
        f.Request.Version++;
        Assert.True(f.Verify(pin).IsValid);
        Assert.Equal(f.Policy.Id, pin.PolicyVersionId);
        Assert.Equal(f.Policy.PolicyVersionNumber, pin.PolicyVersionNumber);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("PendingReview")]
    [InlineData("Revoked")]
    [InlineData("Superseded")]
    public void Unpublished_or_unrecognized_policy_state_cannot_replace_published_pin_authority(string state)
    {
        var f = new Fixture();
        var pin = f.Pin(); f.Link(pin);
        f.Policy.State = state;
        Assert.Contains("PolicyEvidenceInvalid", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Closing_prior_policy_before_submission_or_changing_its_rules_still_invalidates_pin()
    {
        var f = new Fixture(); var pin = f.Pin(); f.Link(pin);
        f.Policy.EffectiveTo = f.Request.SubmittedAt;
        Assert.Contains("PolicyEvidenceInvalid", f.Verify(pin).Blockers);
        f.Policy.EffectiveTo = f.Now.AddDays(1);
        f.Policy.RulesJson = "{\"changed\":true}";
        Assert.Contains("PinnedPolicyMismatch", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Operational_revision_and_approval_link_do_not_change_submitted_business_evidence()
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        f.Request.Version += 4;
        f.Request.ApprovalState = "InProgress";
        f.Request.UpdatedAt = f.Now.AddMinutes(2);
        f.Request.Lines.First().Version++;
        Assert.True(f.Verify(pin).IsValid);
        Assert.Equal(4, pin.SubmittedBusinessVersion);
        Assert.Equal(250m, pin.EstimatedAmount);
        Assert.Equal("EstimatedLineBase", pin.AmountBasis);
    }

    [Fact]
    public void Snapshot_is_stable_across_line_collection_order_and_decimal_scale()
    {
        var f = new Fixture();
        f.AddLine();
        var first = f.Pin();
        f.Request.Lines = f.Request.Lines.Reverse().ToList();
        f.Request.Lines.First().RequestedQuantity = 1.0000m;
        var second = f.Pin();
        Assert.Equal(first.RequestSnapshotJson, second.RequestSnapshotJson);
        Assert.Equal(first.RequestSnapshotSha256, second.RequestSnapshotSha256);
    }

    [Fact]
    public void Snapshot_is_stable_across_server_cultures()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var f = new Fixture();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var first = f.Pin();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = f.Pin();
            Assert.Equal(first.RequestSnapshotJson, second.RequestSnapshotJson);
            Assert.Equal(first.FinanceSnapshotSha256, second.FinanceSnapshotSha256);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void PostgreSQL_microsecond_and_jsonb_roundtrips_preserve_business_evidence()
    {
        var f = new Fixture();
        f.Request.SubmittedAt = f.Request.SubmittedAt!.Value.AddTicks(7);
        f.Finance.CheckedAt = f.Finance.CheckedAt.AddTicks(7);
        f.Policy.RulesJson = "{\"z\":[1.0000,{\"b\":true,\"a\":null}],\"a\":2}";
        var pin = f.Pin();
        f.Link(pin);
        f.Request.SubmittedAt = f.Request.SubmittedAt.Value.AddTicks(-7);
        f.Finance.CheckedAt = f.Finance.CheckedAt.AddTicks(-7);
        f.Policy.RulesJson = "{ \"a\": 2.0, \"z\": [1, {\"a\": null, \"b\": true}] }";
        Assert.True(f.Verify(pin).IsValid);
    }

    [Fact]
    public void Duplicate_policy_object_keys_are_not_valid_pin_evidence()
    {
        var f = new Fixture();
        f.Policy.RulesJson = "{\"limit\":1,\"limit\":2}";
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Theory]
    [InlineData("1e2", "100.0000")]
    [InlineData("1.2500E-4", "0.000125")]
    [InlineData("9007199254740993", "9007199254740993.0")]
    [InlineData("79228162514264337593543950335", "7.9228162514264337593543950335E28")]
    [InlineData("-0.0000", "0")]
    public void Policy_number_formatting_is_exact_and_semantic(string original, string stored)
    {
        var f = new Fixture();
        f.Policy.RulesJson = "{\"amount\":" + original + "}";
        var pin = f.Pin();
        f.Link(pin);
        f.Policy.RulesJson = "{ \"amount\": " + stored + " }";
        Assert.True(f.Verify(pin).IsValid);
    }

    [Theory]
    [InlineData("1e100")]
    [InlineData("1e-100")]
    [InlineData("1.234567890123456789012345678901")]
    public void Unsupported_or_lossy_policy_numbers_are_rejected(string number)
    {
        var f = new Fixture();
        f.Policy.RulesJson = "{\"amount\":" + number + "}";
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Fact]
    public void Policy_array_order_and_large_integer_changes_remain_material()
    {
        var f = new Fixture();
        f.Policy.RulesJson = "{\"steps\":[1,2],\"amount\":9007199254740993}";
        var pin = f.Pin();
        f.Link(pin);
        f.Policy.RulesJson = "{\"steps\":[2,1],\"amount\":9007199254740993}";
        Assert.Contains("PinnedPolicyMismatch", f.Verify(pin).Blockers);
        f.Policy.RulesJson = "{\"steps\":[1,2],\"amount\":9007199254740994}";
        Assert.Contains("PinnedPolicyMismatch", f.Verify(pin).Blockers);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("coding")]
    [InlineData("tax")]
    [InlineData("discount")]
    [InlineData("catalogVersion")]
    [InlineData("requestPurpose")]
    public void Material_input_changes_are_denied_even_without_header_revision_change(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        var line = f.Request.Lines.First();
        switch (change)
        {
            case "description": line.Description += " changed"; break;
            case "quantity": line.RequestedQuantity = 1; break;
            case "price": line.EstimatedUnitPrice = 100; break;
            case "coding": line.CostCenterRef = "CC-OTHER"; break;
            case "tax": line.TaxCodeRef = "TAX-OTHER"; break;
            case "discount": line.DiscountPercent = 1; break;
            case "catalogVersion": line.CatalogPublishedVersion = 2; break;
            case "requestPurpose": f.Request.Purpose += " changed"; break;
        }
        Assert.Contains("SubmittedBusinessInputsChanged", f.Verify(pin).Blockers);
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("duplicate")]
    public void Pinned_policy_must_cover_exact_current_line_identity_set(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        switch (change)
        {
            case "replace": f.Request.Lines.First().Id = Guid.NewGuid(); break;
            case "add": f.Request.Lines.Add(f.NewLine(2)); break;
            case "remove": f.Request.Lines.Clear(); break;
            case "duplicate": f.Request.Lines.Add(f.Request.Lines.First()); break;
        }
        Assert.False(f.Verify(pin).IsValid);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("line")]
    [InlineData("evaluation")]
    [InlineData("policy")]
    [InlineData("finance")]
    public void Foreign_scope_is_rejected_before_pinning(string record)
    {
        var f = new Fixture();
        ProcurementRecord row = record switch
        {
            "request" => f.Request, "line" => f.Request.Lines.First(),
            "evaluation" => f.Evaluation, "policy" => f.Policy, _ => f.Finance,
        };
        row.LegalEntityId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Fact]
    public void Caller_cannot_verify_evidence_under_another_tenant_or_company()
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        Assert.Contains("PinnedScopeMismatch", PurchaseRequestApprovalPinning.Verify(pin,
            f.Request, f.Evaluation, f.Policy, f.Finance, "foreign", f.Company).Blockers);
        Assert.Contains("PinnedScopeMismatch", PurchaseRequestApprovalPinning.Verify(pin,
            f.Request, f.Evaluation, f.Policy, f.Finance, Fixture.Tenant, Guid.NewGuid()).Blockers);
    }

    [Theory]
    [InlineData("evaluation")]
    [InlineData("policy")]
    [InlineData("finance")]
    [InlineData("decision")]
    [InlineData("rules")]
    [InlineData("reservation")]
    [InlineData("submittedVersion")]
    [InlineData("case")]
    public void Replaced_references_or_mutated_evidence_never_reauthorize_a_case(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        switch (change)
        {
            case "evaluation": f.Evaluation.Id = Guid.NewGuid(); break;
            case "policy": f.Policy.Id = Guid.NewGuid(); break;
            case "finance": f.Finance.Id = Guid.NewGuid(); break;
            case "decision": f.Evaluation.DecisionJson += " "; break;
            case "rules": f.Policy.RulesJson = "{\"changed\":true}"; break;
            case "reservation": f.Finance.ReservationRef = "OTHER"; break;
            case "submittedVersion": f.Request.SubmittedVersion++; break;
            case "case": f.Request.ApprovalInstanceRef = Guid.NewGuid().ToString(); break;
        }
        Assert.False(f.Verify(pin).IsValid);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("allocated")]
    [InlineData("ordered")]
    [InlineData("accepted")]
    [InlineData("cancelled")]
    [InlineData("route")]
    [InlineData("approvalStatus")]
    public void Pending_source_cannot_hide_prior_quantity_effects_or_routing(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        var line = f.Request.Lines.First();
        switch (change)
        {
            case "approved": line.ApprovedQuantity = 1; break;
            case "allocated": line.AllocatedQuantity = 1; break;
            case "ordered": line.OrderedQuantity = 1; break;
            case "accepted": line.AcceptedQuantity = 1; break;
            case "cancelled": line.CancelledQuantity = 1; break;
            case "route": line.Route = "DirectPO"; break;
            case "approvalStatus": line.ApprovalStatus = "Approved"; break;
        }
        Assert.Contains("InvalidLines", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Manual_evidence_requires_an_explicit_governance_reference_and_does_not_approve_request()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Pin(PurchaseRequestApprovalPinning.ApprovedManualEvidence));
        var pin = f.Pin(PurchaseRequestApprovalPinning.ApprovedManualEvidence, "POLICY-MANUAL-1");
        Assert.Equal("POLICY-MANUAL-1", pin.ManualEvidenceAuthorityRef);
        Assert.Equal("Submitted", f.Request.Status);
        Assert.Null(f.Request.ApprovalInstanceRef);
        Assert.Equal(0m, f.Request.Lines.First().ApprovedQuantity);
    }

    [Fact]
    public void Unlinked_or_terminal_source_is_not_a_valid_active_case()
    {
        var f = new Fixture();
        var pin = f.Pin();
        Assert.Contains("LinkedApprovalCaseMismatch", f.Verify(pin).Blockers);
        f.Link(pin);
        f.Request.Status = "Approved";
        Assert.Contains("InvalidSource", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Published_policy_must_have_been_effective_at_submission()
    {
        var f = new Fixture();
        f.Policy.EffectiveFrom = f.Now.AddSeconds(1);
        Assert.Throws<ArgumentException>(() => f.Pin());
        f.Policy.EffectiveFrom = f.Now.AddDays(-1);
        f.Policy.EffectiveTo = f.Now;
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Theory]
    [InlineData("released")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("stale")]
    [InlineData("noEvidence")]
    public void Invalid_finance_confirmation_cannot_be_pinned(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "released": f.Finance.ReservationStatus = "Released"; break;
            case "amount": f.Finance.CheckedAmount = 249; break;
            case "currency": f.Finance.CurrencyCode = "USD"; break;
            case "stale": f.Finance.RequestVersion = 3; break;
            case "noEvidence": f.Finance.EvidenceRef = null; break;
        }
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Fact]
    public void Legacy_pin_and_terminal_source_restore_keep_original_finance_bytes_and_worker_identities()
    {
        var f = new Fixture(); var pin = f.Pin(); f.Link(pin);
        Assert.Equal(0, pin.FinanceIdentitySchemaVersion);
        Assert.DoesNotContain("FinanceIdentitySchemaVersion", JsonSerializer.Serialize(pin));
        Assert.Equal(FinanceSnapshotCodec.Legacy(f.Finance), pin.FinanceSnapshotJson);
        var restored = JsonSerializer.Deserialize<PurchaseRequestApprovalSubmission>(JsonSerializer.Serialize(pin))!;
        f.Request.Status = "Returned";
        Assert.True(PurchaseRequestApprovalPinning.VerifyBusinessEvidence(restored, f.Request, f.Evaluation,
            f.Policy, f.Finance, Fixture.Tenant, f.Company).IsValid);
        Assert.True(PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(restored));
        Assert.Equal(f.MakerWorkerId, restored.MakerWorkerId);
        Assert.Equal(f.Request.RequesterWorkerId, restored.RequesterWorkerId);
        Assert.Equal(pin.FinanceSnapshotJson, restored.FinanceSnapshotJson);
        Assert.Equal(pin.FinanceSnapshotSha256, restored.FinanceSnapshotSha256);
        // Historic source decoding does not become new independent-checker authorization.
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f.Finance));
    }

    [Fact]
    public void Bound_finance_pin_dispatches_independently_of_existing_schema_two_maker_requester_identity()
    {
        var f = new Fixture(); var worker = Guid.NewGuid();
        var binding = FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f.Finance, f.Finance.CheckedBySubjectId, worker);
        f.Finance.CheckerIdentitySchemaVersion = binding.SchemaVersion;
        f.Finance.CheckedByWorkerId = binding.WorkerId; f.Finance.CheckerIdentitySha256 = binding.Sha256;
        var pin = f.Pin(); f.Link(pin);
        Assert.Equal(2, pin.SnapshotSchemaVersion); Assert.Equal(1, pin.FinanceIdentitySchemaVersion);
        Assert.True(f.Verify(pin).IsValid);
        Assert.True(PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin));
        var json = JsonNode.Parse(JsonSerializer.Serialize(pin))!;
        json["FinanceIdentitySchemaVersion"] = 0;
        var downgraded = JsonSerializer.Deserialize<PurchaseRequestApprovalSubmission>(json.ToJsonString())!;
        Assert.Contains("PinnedFinanceMismatch", f.Verify(downgraded).Blockers);
        f.Finance.CheckedByWorkerId = Guid.NewGuid();
        Assert.Contains("PinnedFinanceMismatch", f.Verify(pin).Blockers);
    }

    private sealed class Fixture
    {
        public const string Tenant = "tenant-a";
        public readonly Guid Company = Guid.NewGuid();
        public readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        public PurchaseRequest Request { get; }
        public PolicyVersion Policy { get; }
        public PolicyEvaluation Evaluation { get; }
        public BudgetCheckSnapshot Finance { get; }

        public Fixture()
        {
            Request = Scoped(new PurchaseRequest
            {
                Number = "PR-1", Status = "Submitted", Version = 4, SubmittedVersion = 4,
                SubmittedAt = Now, Purpose = "Office supplies", CurrencyCode = "ZMW",
                RequesterWorkerId = Guid.NewGuid(), ApprovalState = "PendingPolicyAndBudget",
            });
            Request.Lines.Add(NewLine(1));
            Policy = Scoped(new PolicyVersion
            {
                PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 2,
                State = "Published", EffectiveFrom = Now.AddDays(-1), RulesJson = "{}",
            });
            Evaluation = Scoped(new PolicyEvaluation
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, PolicyVersionId = Policy.Id,
                PolicyVersionNumber = 2, AmountBasis = "EstimatedLineBase", Outcome = "Matched",
            });
            RefreshDecision();
            Finance = Scoped(new BudgetCheckSnapshot
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, Status = "Available",
                CheckedAmount = 250, CurrencyCode = "ZMW", ReservationStatus = "Confirmed",
                ReservationRef = "RESERVE-1", FinanceCheckRef = "CHECK-1", EvidenceRef = "DOC-1",
                CheckedAt = Now.AddSeconds(1), CheckedBySubjectId = "finance-user",
            });
        }

        public PurchaseRequestLine NewLine(int number) => Scoped(new PurchaseRequestLine
        {
            PurchaseRequestId = Request.Id, LineNumber = number, Description = "Office item",
            UnitCode = "EA", RequestedQuantity = number == 1 ? 2 : 1,
            EstimatedUnitPrice = number == 1 ? 125 : 0, EstimatedCurrencyCode = "ZMW",
        });
        public void AddLine() { Request.Lines.Add(NewLine(2)); RefreshDecision(); }
        private void RefreshDecision() => Evaluation.DecisionJson = JsonSerializer.Serialize(new
        {
            requestId = Request.Id, requestVersion = Request.SubmittedVersion, policyId = Policy.Id,
            lines = Request.Lines.Select(x => new
            {
                lineId = x.Id, decision = new { status = "Match", approvalStages = new[]
                    { new { code = "NEED", assignee = "LineManager", mode = "One" } } },
            }),
        });
        public Guid MakerWorkerId { get; } = Guid.NewGuid();
        public PurchaseRequestApprovalSubmission Pin(string mode = PurchaseRequestApprovalPinning.AutomatedFinance,
            string? authority = null) => PurchaseRequestApprovalPinning.Pin(Request, Evaluation, Policy, Finance,
                Tenant, Company, Guid.NewGuid(), Guid.NewGuid(), mode, authority, Now.AddMinutes(1), MakerWorkerId, Request.RequesterWorkerId);
        public void Link(PurchaseRequestApprovalSubmission pin)
        {
            Request.ApprovalInstanceRef = pin.CoreApprovalCaseId.ToString();
            Request.Version++;
        }
        public ApprovalPinValidation Verify(PurchaseRequestApprovalSubmission pin) =>
            PurchaseRequestApprovalPinning.Verify(pin, Request, Evaluation, Policy, Finance, Tenant, Company);
        private T Scoped<T>(T row) where T : ProcurementRecord
        { row.TenantId = Tenant; row.LegalEntityId = Company; row.CreatedBy = "requester-user"; return row; }
    }
}
