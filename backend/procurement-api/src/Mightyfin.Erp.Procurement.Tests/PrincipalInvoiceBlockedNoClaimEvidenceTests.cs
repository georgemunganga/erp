using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceBlockedNoClaimEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Recorded_header_block_without_claim_is_classified_without_revaluing_old_header()
    {
        var f = Fixture();
        Verify(f);
        Assert.Equal(5.1234m, f.Invoice.GrossAmount); // Historical scale exceeds the policy's two minor units.
        f.Exception.Reason = "different unresolved effect";
        Assert.Throws<InvalidOperationException>(() => Verify(f));
    }

    [Fact]
    public void Null_physical_header_po_keeps_the_verified_mapping_projection_and_wrong_header_denies()
    {
        Verify(Fixture(nullHeaderPo: true));
        var wrong = Fixture(); wrong.Invoice.PurchaseOrderId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Verify(wrong));
    }

    [Fact]
    public void Recorded_policy_uses_the_foundation_string_enum_and_decimal_contract()
    {
        Verify(Fixture());
        Assert.Throws<InvalidOperationException>(() => Verify(Fixture(numericPolicyEvidence: true)));
        Assert.Throws<InvalidOperationException>(() => Verify(Fixture(numericToleranceEvidence: true)));
    }

    [Fact]
    public void Extra_effects_and_self_consistent_but_wrong_markers_are_denied()
    {
        var f = Fixture();
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceBlockedNoClaimEvidence.Verify(
            f.Result, f.Invoice, f.Mapping, [f.Line], f.Policy, f.ReviewSource, f.ReviewOutcome, f.Receipt,
            f.CompareAudit, f.CreateAudit, f.Outbox, [f.Exception],
            [new InvoiceMatchAllocation { MatchResultId = f.Result.Id }]));
        f.CompareAudit.ChangedFieldsJson = JsonSerializer.Serialize(new { schemaVersion = 3,
            resultId = f.Result.Id, hasCapacityClaim = true }, Json);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture();
        f.Outbox.PayloadJson = JsonSerializer.Serialize(new { schemaVersion = 3,
            resultId = f.Result.Id, canPostToFinance = true }, Json);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture();
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceBlockedNoClaimEvidence.Verify(
            f.Result, f.Invoice, f.Mapping, [f.Line], f.Policy, f.ReviewSource, f.ReviewOutcome, f.Receipt,
            f.CompareAudit, f.CreateAudit, f.Outbox, [f.Exception, f.Exception], []));
        f = Fixture();
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceBlockedNoClaimEvidence.VerifyClosedFootprint(
            [f.Result], [f.Invoice], [f.Line], [f.Mapping], [f.ReviewSource], [f.ReviewOutcome], [f.Policy],
            [f.Receipt, f.Receipt], [f.CompareAudit, f.CreateAudit], [f.Outbox], [f.Exception], []));
        f = Fixture(1m);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture(badWatermark: true);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture(forgedSourceSupplier: true);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture(duplicateOrderCheckpoint: true);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        f = Fixture(duplicateAcceptanceCheckpoint: true);
        Assert.Throws<InvalidOperationException>(() => Verify(f));
        foreach (var mutation in new[] { "line", "fact", "kind", "source", "currency", "omitted" })
        {
            f = Fixture(projectionMutation: mutation);
            Assert.Throws<InvalidOperationException>(() => Verify(f));
        }
    }

    private static void Verify(FixtureState f) => PrincipalInvoiceBlockedNoClaimEvidence.Verify(
        f.Result, f.Invoice, f.Mapping, [f.Line], f.Policy, f.ReviewSource, f.ReviewOutcome, f.Receipt,
        f.CompareAudit, f.CreateAudit, f.Outbox, [f.Exception], []);

    private sealed record FixtureState(SupplierInvoice Invoice, SupplierInvoiceLine Line,
        InvoiceMatchMappingVersion Mapping, InvoiceMatchResult Result, PolicyVersion Policy,
        InvoiceUnitReviewSource ReviewSource, InvoiceUnitReviewOutcome ReviewOutcome,
        ProcurementCommercialCommandReceipt Receipt, AuditEvent CompareAudit,
        AuditEvent CreateAudit, OutboxEvent Outbox, InvoiceException Exception);

    private static FixtureState Fixture(decimal? unexpectedVariance = null,
        bool badWatermark = false, bool forgedSourceSupplier = false,
        bool duplicateOrderCheckpoint = false, bool duplicateAcceptanceCheckpoint = false,
        string? projectionMutation = null, bool numericPolicyEvidence = false,
        bool numericToleranceEvidence = false, bool nullHeaderPo = false)
    {
        var tenant = "blocked-no-claim-test";
        var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var supplier = Guid.NewGuid(); var order = Guid.NewGuid(); var issued = Guid.NewGuid();
        var orderLine = Guid.NewGuid(); var sourceId = Guid.NewGuid(); var factId = Guid.NewGuid();
        var key = Guid.NewGuid(); var actor = Guid.NewGuid().ToString("D");
        var matched = new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);
        var invoice = new SupplierInvoice { TenantId = tenant, LegalEntityId = company,
            BranchRef = branch, SupplierId = supplier, PurchaseOrderId = nullHeaderPo ? null : order,
            InvoiceNumber = "TEST-BLOCKED-1", NormalizedInvoiceNumber = "TEST-BLOCKED-1",
            CurrencyCode = "ZMW", IntakeChannel = "Manual", InvoiceDate = new DateOnly(2026, 10, 30),
            NetAmount = 5.1234m, GrossAmount = 5.1234m, Status = "Received", Version = 3 };
        var line = new SupplierInvoiceLine { TenantId = tenant, LegalEntityId = company,
            SupplierInvoiceId = invoice.Id, LineNumber = 1,
            Description = "Original", Quantity = 1,
            UnitPrice = 5.1234m, TaxAmount = 0 };
        var payload = PrincipalInvoiceMappingEvidence.Create(invoice, [line], order, issued,
            [new PrincipalInvoiceMappingLine(line.Id, orderLine, sourceId, factId,
                "EA", Guid.Empty, "")], actor, null);
        var mapping = new InvoiceMatchMappingVersion { TenantId = tenant, LegalEntityId = company,
            SupplierInvoiceId = invoice.Id, PurchaseOrderId = order, IssuedVersionId = issued,
            MappingRevision = payload.Snapshot.Revision, IdentitySchemaVersion = 2, SourceSchemaVersion = 3,
            InvoiceOriginalSha256 = payload.Snapshot.InvoiceOriginalSha256, MappingJson = payload.Json,
            MappingSha256 = payload.Sha256, ActorPrincipalId = actor };
        invoice.Version = 4; invoice.MappingRevision = mapping.MappingRevision;
        invoice.CurrentMappingVersionId = mapping.Id; invoice.MatchingState = "ComparisonBlocked";
        invoice.MatchRevision = 1; invoice.UpdatedBy = actor;
        var policy = new PolicyVersion { TenantId = tenant, LegalEntityId = company,
            PolicyKey = InvoiceMatchingPolicyRules.PolicyKey, PolicyVersionNumber = 1,
            State = "Published", RulesJson = """
                {"schemaVersion":1,"transactionType":"InvoiceMatching","currencyCode":"ZMW","moneyScale":2,"rounding":"ToEven","rules":[{"categoryCode":"TEST","method":"ThreeWay","acceptanceKind":"Goods","quantity":{"absolute":"0","percent":"0","combination":"AllLimits"},"unitPrice":{"absolute":"0","percent":"0","combination":"AllLimits"},"tax":{"absolute":"0","percent":"0","combination":"AllLimits"},"total":{"absolute":"0","percent":"0","combination":"AllLimits"}}]}
                """, CreatedBy = actor, PublishedBy = Guid.NewGuid().ToString("D"), PublicationReason = "Test",
            PublishedAt = matched.AddMinutes(-1),
            EffectiveFrom = matched.AddMinutes(-1) };
        var inputFingerprint = new string('a', 64);
        var consumptionWatermark = new string('b', 64);
        var duplicateWatermark = new string('c', 64);
        var secondOrderLine = Guid.NewGuid(); var secondFact = Guid.NewGuid();
        var sourceOrderLines = duplicateOrderCheckpoint
            ? new[] { new { id = orderLine, version = 1 }, new { id = secondOrderLine, version = 2 } }
            : new[] { new { id = orderLine, version = 1 } };
        var checkpointOrderLines = duplicateOrderCheckpoint
            ? new[] { new { id = orderLine, version = 1 }, new { id = orderLine, version = 1 } }
            : sourceOrderLines;
        object AcceptedFact(Guid id, Guid poLine, int version) => new {
            id, version, tenantId = tenant, legalEntityId = company, branchRef = branch,
            purchaseOrderId = order, revisionId = issued, purchaseOrderLineId = poLine,
            currencyCode = projectionMutation == "currency" ? "USD" : "ZMW", unitCode = "EA",
            kind = projectionMutation == "kind" ? "Service" : "Goods", state = "Accepted",
            acceptanceReference = $"PrincipalReceiving:{(projectionMutation == "source" ? Guid.NewGuid() : sourceId):D}:{id:D}",
            acceptedAt = matched.AddMinutes(-1), reversed = false,
            acceptedQuantity = "1", returnedQuantity = "0",
            acceptedNetAmount = "5.1234", returnedNetAmount = "0" };
        var sourceAcceptances = projectionMutation == "omitted" ? Array.Empty<object>() : duplicateAcceptanceCheckpoint
            ? new[] { AcceptedFact(factId, orderLine, 3), AcceptedFact(secondFact, secondOrderLine, 2) }
            : new[] { AcceptedFact(factId, orderLine, 3) };
        var acceptanceVersions = projectionMutation == "omitted" ? Array.Empty<object>() : duplicateAcceptanceCheckpoint
            ? new object[] { new { id = factId, version = 3 }, new { id = secondFact, version = 2 } }
            : new object[] { new { id = factId, version = 3 } };
        var checkpointAcceptances = duplicateAcceptanceCheckpoint
            ? new[] { new { id = factId, version = 3 }, new { id = factId, version = 3 } }
            : acceptanceVersions;
        var reviewSource = new InvoiceUnitReviewSource { TenantId = tenant, LegalEntityId = company,
            SupplierInvoiceId = invoice.Id, MappingVersionId = mapping.Id, PurchaseOrderId = order,
            IssuedVersionId = issued, OriginalSha256 = mapping.InvoiceOriginalSha256,
            MappingSha256 = mapping.MappingSha256, MatchingPolicyId = policy.Id,
            CoreCaseId = Guid.NewGuid(), MakerPrincipalId = actor };
        var reviewOutcome = new InvoiceUnitReviewOutcome { TenantId = tenant, LegalEntityId = company,
            ReviewSourceId = reviewSource.Id, CoreCaseId = reviewSource.CoreCaseId,
            // This fixture tests retained digest linkage, not independent Core review approval.
            State = "Verified", EvidenceSchemaVersion = 4, EvidenceJson = "{}",
            EvidenceSha256 = ProcurementEvidenceEncoding.Digest("{}") };
        var serializedPolicy = InvoiceMatchingFoundation.SerializePolicyEvidence(
            InvoiceMatchingPolicyRules.Project(policy));
        if (numericToleranceEvidence)
            serializedPolicy = serializedPolicy.Replace("\"absolute\":\"0\"", "\"absolute\":0",
                StringComparison.Ordinal);
        var source = new
        {
            invoice = new { invoice.Id, version = 3, tenantId = tenant, legalEntityId = company,
                branchRef = branch, supplierId = supplier, purchaseOrderId = order,
                currencyCode = "ZMW", status = "Received", netAmount = "5.1234",
                taxAmount = "0", grossAmount = "5.1234", invoice.InvoiceNumber,
                invoice.NormalizedInvoiceNumber, invoice.InvoiceDate, invoice.DueDate,
                invoice.EvidenceDocumentRefId, invoice.CreditForInvoiceRef, invoice.FinancePostingRef,
                lines = new[] { new { line.Id, line.Version, line.SupplierInvoiceId,
                    tenantId = tenant, legalEntityId = company,
                    purchaseOrderLineId = (Guid?)(projectionMutation == "line" ? Guid.NewGuid() : orderLine),
                    goodsReceiptLineId = (Guid?)(projectionMutation == "fact" ? Guid.NewGuid() : factId),
                    serviceAcceptanceId = (Guid?)null, unitCode = "EA",
                    quantity = "1", unitPrice = "5.1234", taxAmount = "0", line.Description,
                    line.TaxCodeRef, line.CostCenterRef } } },
            order = new { id = order, revisionId = issued, version = 1,
                supplierId = forgedSourceSupplier ? Guid.NewGuid() : supplier,
                currencyCode = "ZMW", branchRef = branch,
                lines = sourceOrderLines },
            acceptances = sourceAcceptances,
            policy = numericPolicyEvidence
                ? JsonSerializer.SerializeToElement(InvoiceMatchingPolicyRules.Project(policy), Json)
                : JsonDocument.Parse(serializedPolicy).RootElement.Clone(),
            consumption = new { watermark = consumptionWatermark, complete = true, allocations = Array.Empty<object>() },
            duplicates = new { watermark = duplicateWatermark, complete = true }
        };
        var evidence = JsonSerializer.Serialize(new
        { schemaVersion = 1, commandKey = key, inputFingerprint, actor,
            invoiceId = invoice.Id, invoiceVersion = 3, outcome = "Blocked",
            discrepancies = new[] { new { invoiceLineId = (Guid?)null, code = "InvoiceHeaderAmountInvalid" } },
            lines = Array.Empty<object>(), source }, Json);
        var watermarks = JsonSerializer.Serialize(new { schemaVersion = 1,
            mappingVersionId = mapping.Id, mappingSha256 = mapping.MappingSha256,
            invoiceOriginalSha256 = mapping.InvoiceOriginalSha256, invoiceId = invoice.Id,
            invoiceVersion = 3, purchaseOrderId = order, issuedVersionId = issued,
            orderVersion = 1, orderLines = checkpointOrderLines,
            accepted = checkpointAcceptances,
            policyId = policy.Id, policyVersion = 1,
            reviewSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(reviewOutcome.EvidenceJson),
            consumptionWatermark = badWatermark ? new string('f', 64) : consumptionWatermark,
            consumptionSha256 = InvoiceMatchExecutionEvidence.AllocationsDigest(Array.Empty<InvoiceMatchConsumption>()),
            duplicatesWatermark = duplicateWatermark }, Json);
        var result = new InvoiceMatchResult { TenantId = tenant, LegalEntityId = company,
            SupplierInvoiceId = invoice.Id, MatchType = "Unresolved", Outcome = "Blocked",
            PriceVariance = unexpectedVariance,
            PolicyVersionId = policy.Id, MatchedAt = matched, ExecutionVersion = 2,
            MappingVersionId = mapping.Id, PurchaseOrderId = order, IssuedVersionId = issued,
            SourceSchemaVersion = 3, InvoiceVersion = 3, InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256,
            ActorPrincipalId = actor, CommandKey = key, InputFingerprint = inputFingerprint,
            EvidenceJson = evidence, EvidenceSha256 = ProcurementEvidenceEncoding.Digest(evidence),
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            SourceWatermarksJson = watermarks };
        invoice.LastMatchResultId = result.Id;
        var exception = new InvoiceException { TenantId = tenant, LegalEntityId = company,
            SupplierInvoiceId = invoice.Id, MatchResultId = result.Id, Type = "InvoiceHeaderAmountInvalid",
            Status = "Open", OwnerSubjectId = actor,
            DiscrepancyKey = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 3, invoiceId = invoice.Id, invoiceLineId = (Guid?)null,
                code = "InvoiceHeaderAmountInvalid" }, Json)),
            Reason = "Review the original invoice and authoritative source evidence." };
        var response = JsonSerializer.Serialize(new { invoiceId = invoice.Id, commandKey = key,
            commandPermission = "procurement-invoice-match", commandSchemaVersion = 3,
            sourceSchemaVersion = 3, recordedHistoricalResult = true, isCurrentApproval = false,
            canPostToFinance = false, result = new { result.Id, result.Outcome, invoice.Version,
                invoice.MatchingState, hasCapacityClaim = false, canPostToFinance = false,
                discrepancies = new[] { new { invoiceLineId = (Guid?)null, code = "InvoiceHeaderAmountInvalid" } },
                sourceBlockers = Array.Empty<string>() } }, Json);
        var receipt = new ProcurementCommercialCommandReceipt { TenantId = tenant, LegalEntityId = company,
            PurchaseOrderId = order, Operation = "InvoiceCompare", CommandKey = key,
            ActorPrincipalId = actor, Fingerprint = new string('e', 64), ResponseJson = response,
            CompletedAt = matched.AddSeconds(1) };
        var comparisonMarker = JsonSerializer.Serialize(new { schemaVersion = 3,
            sourceSchemaVersion = 3, mappingVersionId = mapping.Id, resultId = result.Id,
            commandKey = key, outcome = "Blocked", hasCapacityClaim = false,
            canPostToFinance = false }, Json);
        var outboxMarker = JsonSerializer.Serialize(new { schemaVersion = 3,
            sourceSchemaVersion = 3, invoiceId = invoice.Id, resultId = result.Id,
            outcome = "Blocked", executionVersion = 2, principalEvidenceVersion = 2,
            canPostToFinance = false }, Json);
        var compareAudit = new AuditEvent { TenantId = tenant, LegalEntityId = company,
            RecordType = "SupplierInvoice", RecordId = invoice.Id, Action = "PrincipalInvoiceCompared",
            ActorSubjectId = actor, CorrelationId = key.ToString("D"), ChangedFieldsJson = comparisonMarker };
        var createdFields = typeof(InvoiceMatchResult).GetProperties().Select(x => x.Name)
            .Concat(["LegacyIssuedVersionRefId", "PrincipalIssuedVersionRefId"]).Order().ToArray();
        var createAudit = new AuditEvent { TenantId = tenant, LegalEntityId = company,
            RecordType = "InvoiceMatchResult", RecordId = result.Id, Action = "create",
            ActorSubjectId = actor, ChangedFieldsJson = JsonSerializer.Serialize(new
            { fields = createdFields, principalEvidenceKind = "InvoiceMatchExecution",
                principalEvidenceVersion = 2 }, Json) };
        var outbox = new OutboxEvent { TenantId = tenant, LegalEntityId = company,
            AggregateType = "SupplierInvoice", AggregateId = invoice.Id,
            EventType = "supplier_invoice.compared.v3", EventVersion = 3,
            IdempotencyKey = key.ToString("D"), PayloadJson = outboxMarker };
        return new(invoice, line, mapping, result, policy, reviewSource, reviewOutcome,
            receipt, compareAudit, createAudit, outbox, exception);
    }
}
