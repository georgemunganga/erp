using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private sealed record SourceLoad(PurchaseOrder Order, PurchaseOrderLine Line, PurchaseOrderSubmission Submission,
        PurchaseOrderIssuedVersion Issued, PurchaseOrderDeliveryEvidence Delivery, string Category, string PurchaseType);
    private sealed record FinalSource(SourceLoad Load, ReceivingSourceState State, ReceivingSourceProjection Source,
        PolicyVersion Policy, ReceivingRule Rule, ReceivingPolicyProjection Rules);
    private static async Task<ReceivingActorProjection> Actor(HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, string permission, Guid? branch, Guid? site, CancellationToken ct, bool target = true)
    {
        if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, permission, ct) is null) throw new ReceivingProblem("ReceivingAccessDenied", 403);
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct)
            ?? throw new ReceivingProblem("ReceivingCanonicalPersonRequired", 403);
        if (await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId, scope.LegalEntityId, account.WorkerId, ct) is null)
            throw new ReceivingProblem("ReceivingCanonicalPersonRequired", 403);
        var branches = await ProcurementExecutionRecordScope.Branches(core, scope, branch, target, ct);
        var sites = new HashSet<Guid>();
        if (site is { } location && location == branch && await core.WorkLocations.AsNoTracking().AnyAsync(x => x.Id == location
            && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived, ct)) sites.Add(location);
        if (site.HasValue && !sites.Contains(site.Value)) throw new ReceivingProblem("ReceivingLocationUnavailable", 409);
        return new(account.PrincipalId, account.WorkerId, true, scope.TenantId, scope.LegalEntityId, scope.WorkLocationId,
            branches.Length == 0, branches.ToHashSet(), sites, new HashSet<string>([permission], StringComparer.Ordinal));
    }
    private static async Task<SourceLoad> Source(ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        Guid orderId, Guid issuedId, Guid lineId, CancellationToken ct, bool lockSource = true)
    {
        if (lockSource)
        {
        var lineIds = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == orderId && x.IsActive && x.PurchaseRequestLineId != null)
            .Select(x => x.PurchaseRequestLineId!.Value).ToArrayAsync(ct);
        var requestIds = await db.PurchaseRequestLines.AsNoTracking().Where(x => lineIds.Contains(x.Id)).Select(x => x.PurchaseRequestId).Distinct().Order().ToArrayAsync(ct);
        foreach (var requestId in requestIds) await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={orderId} FOR UPDATE", ct);
        }
        var order = await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new ReceivingProblem("ReceivingOrderNotFound", 404);
        if (scope.WorkLocationId is { } selected && order.BranchRef != selected) throw new ReceivingProblem("ReceivingOrderNotFound", 404);
        var line = await db.PurchaseOrderLines.SingleOrDefaultAsync(x => x.Id == lineId && x.PurchaseOrderId == orderId && x.IsActive, ct)
            ?? throw new ReceivingProblem("ReceivingOrderLineNotFound", 404);
        var issued = await db.OrderIssuedVersions.SingleOrDefaultAsync(x => x.Id == issuedId && x.PurchaseOrderId == orderId, ct)
            ?? throw new ReceivingProblem("ReceivingIssuedVersionNotFound", 404);
        var submission = await db.OrderSubmissions.SingleAsync(x => x.Id == issued.SubmissionId && x.PurchaseOrderId == orderId, ct);
        var delivery = await db.OrderDeliveryEvidence.Where(x => x.IssuedVersionId == issuedId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw new ReceivingProblem("ReceivingConfirmedDeliveryRequired", 409);
        if (order.Status != "Issued" || order.CurrentRevision != issued.RevisionNumber || delivery.Outcome != "Delivered"
            || !await cases.Cases.AnyAsync(x => x.Id == submission.CoreApprovalCaseId && x.SourceModule == "procurement"
                && x.SourceType == "purchase_order" && x.SourceId == orderId && x.State == "Approved", ct))
            throw new ReceivingProblem("ReceivingCurrentIssuedAuthorityRequired", 409);
        using var snapshot = JsonDocument.Parse(issued.CommercialSnapshotJson);
        var sealedLine = snapshot.RootElement.GetProperty("lines").EnumerateArray().Single(x => x.GetProperty("Id").GetGuid() == lineId);
        if (issued.CommercialSnapshotJson != submission.CommercialSnapshotJson || issued.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
            || line.PurchaseRequestLineId != sealedLine.GetProperty("PurchaseRequestLineId").GetGuid()
            || line.UnitCode != sealedLine.GetProperty("UnitCode").GetString() || line.Quantity != SealedDecimal(sealedLine.GetProperty("quantity"))
            || line.UnitPrice != SealedDecimal(sealedLine.GetProperty("unitPrice")) || line.TaxAmount != SealedDecimal(sealedLine.GetProperty("tax")))
            throw new ReceivingProblem("ReceivingSealedLineChanged", 409);
        using var sources = JsonDocument.Parse(submission.SourceEvidenceJson);
        var lineage = sources.RootElement.EnumerateArray().Single(x => x.GetProperty("orderLineId").GetGuid() == lineId);
        return new(order, line, submission, issued, delivery, lineage.GetProperty("CategoryCode").GetString()!, lineage.GetProperty("PurchaseType").GetString()!);
    }
    private static decimal SealedDecimal(JsonElement value) => decimal.Parse(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText(), CultureInfo.InvariantCulture);
    private static async Task<(PolicyVersion Policy, ReceivingRule Rule)> Policy(ProcurementDbContext db, SourceLoad source, Guid? retained, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var matches = await db.PolicyVersions.Where(x => x.PolicyKey == "RECEIVING" && x.State == "Published"
            && (retained == null || x.Id == retained) && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (matches.Length != 1) throw new ReceivingProblem("ReceivingPublishedPolicyUnavailable", 409);
        var document = ReceivingPolicyRules.Parse(matches[0].RulesJson);
        if (document.CurrencyCode != source.Order.CurrencyCode) throw new ReceivingProblem("ReceivingPolicyCurrencyMismatch", 409);
        var rule = ReceivingPolicyRules.Select(document, source.Category, source.PurchaseType);
        return (matches[0], rule);
    }
    private static async Task<FinalSource> Resolve(ProcurementDbContext db, SourceLoad source, Guid? retainedPolicy, CancellationToken ct)
    {
        var (policy, rule) = await Policy(db, source, retainedPolicy, ct);
        var state = await db.ReceivingSourceStates.SingleOrDefaultAsync(x => x.IssuedVersionId == source.Issued.Id && x.PurchaseOrderLineId == source.Line.Id, ct);
        if (state is null)
        {
            var lineageHash = ProcurementEvidenceEncoding.PolicyJsonDigest("{\"sources\":" + source.Submission.SourceEvidenceJson + "}");
            var reference = $"RECEIVING:{policy.Id:D}:{policy.PolicyVersionNumber}:{rule.Code}";
            var classification = JsonSerializer.Serialize(new { schemaVersion = 1, tenantId = source.Order.TenantId, legalEntityId = source.Order.LegalEntityId,
                issuedVersionId = source.Issued.Id, orderLineId = source.Line.Id, sourceEvidenceSha256 = lineageHash, reference,
                kind = rule.Kind.ToString(), netPriceBasis = ControlledReceiving.NetPriceBasis, policyVersionId = policy.Id,
                policyVersionNumber = policy.PolicyVersionNumber, policyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson), ruleCode = rule.Code });
            var pin = new ReceivingSourcePin(source.Order.TenantId, source.Order.LegalEntityId, source.Order.BranchRef, source.Order.Id, source.Issued.Id,
                source.Line.Id, source.Line.PurchaseRequestLineId!.Value, source.Issued.RevisionNumber, source.Issued.CommercialSnapshotSha256,
                lineageHash, reference, ProcurementEvidenceEncoding.PolicyJsonDigest(classification), rule.Kind, source.Category, source.PurchaseType,
                source.Order.CurrencyCode, source.Line.UnitCode, source.Line.Quantity, source.Line.UnitPrice, ControlledReceiving.NetPriceBasis);
            state = new() { TenantId = pin.TenantId, LegalEntityId = pin.LegalEntityId, PurchaseOrderId = pin.PurchaseOrderId,
                PurchaseOrderLineId = pin.PurchaseOrderLineId, SubmissionId = source.Submission.Id, IssuedVersionId = pin.IssuedVersionId,
                DeliveryEvidenceId = source.Delivery.Id, Source = pin, SourceSha256 = ReceivingPersistenceProjection.PinDigest(pin),
                ClassificationJson = classification, ClassificationPolicyVersionId = policy.Id };
            db.AddScoped(state);
        }
        else source = source with { Delivery = await db.OrderDeliveryEvidence.SingleAsync(x => x.Id == state.DeliveryEvidenceId && x.IssuedVersionId == state.IssuedVersionId, ct) };
        ReceivingPersistenceProjection.VerifyState(state);
        var classificationPolicy = await db.PolicyVersions.SingleAsync(x => x.Id == state.ClassificationPolicyVersionId, ct);
        var classificationRule = ReceivingPolicyRules.Select(ReceivingPolicyRules.Parse(classificationPolicy.RulesJson), source.Category, source.PurchaseType);
        _ = ReceivingPolicyRules.Project(classificationPolicy, classificationRule);
        using (var classification = JsonDocument.Parse(state.ClassificationJson))
        {
            var c = classification.RootElement;
            if (c.GetProperty("policyVersionId").GetGuid() != classificationPolicy.Id
                || c.GetProperty("policyVersionNumber").GetInt32() != classificationPolicy.PolicyVersionNumber
                || c.GetProperty("policyRulesSha256").GetString() != ProcurementEvidenceEncoding.PolicyJsonDigest(classificationPolicy.RulesJson)
                || c.GetProperty("ruleCode").GetString() != classificationRule.Code || classificationRule.Kind != state.Source.Kind)
                throw new ReceivingProblem("ReceivingClassificationEvidenceChanged", 409);
        }
        if (state.Source.Kind != rule.Kind) throw new ReceivingProblem("ReceivingClassificationChanged", 409);
        var projection = new ReceivingSourceProjection(source.Issued, source.Submission, source.Delivery, state.Source,
            source.Submission.BuyerWorkerId, state.ClassificationJson, true);
        ControlledReceiving.VerifySource(projection);
        return new(source, state, projection, policy, rule, ReceivingPolicyRules.Project(policy, rule));
    }
    private static async Task<ReceivingLedger> Facts(ProcurementDbContext db, ReceivingSourceState state, bool verified, CancellationToken ct)
    {
        var lines = await db.GoodsReceiptLines.Where(x => x.ReceivingSourceStateId == state.Id).ToArrayAsync(ct);
        var headerIds = lines.Select(x => x.GoodsReceiptId).ToArray();
        var headers = await db.GoodsReceipts.Where(x => headerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var inspections = await db.ReceiptInspections.Where(x => x.ReceivingSourceStateId == state.Id).ToArrayAsync(ct);
        var certificates = await db.ServiceAcceptances.Where(x => x.ReceivingSourceStateId == state.Id).ToArrayAsync(ct);
        var effects = await db.ReceivingAcceptanceEffects.Where(x => x.ReceivingSourceStateId == state.Id).OrderBy(x => x.LedgerSequence).ToArrayAsync(ct);
        return ReceivingPersistenceProjection.Ledger(state, lines.Select(x => (headers[x.GoodsReceiptId], x)).ToArray(), inspections, certificates, effects, true, verified);
    }
    private static async Task<ReceivingLedger> Ledger(ProcurementDbContext db, FinalSource source, CancellationToken ct)
    {
        var requestLineId = source.Load.Line.PurchaseRequestLineId!.Value;
        var orderLines = await db.PurchaseOrderLines.Where(x => x.PurchaseRequestLineId == requestLineId && x.IsActive).ToArrayAsync(ct);
        var ids = orderLines.Select(x => x.Id).ToArray();
        var states = await db.ReceivingSourceStates.Where(x => ids.Contains(x.PurchaseOrderLineId)).OrderBy(x => x.Id).ToArrayAsync(ct);
        if (!states.Any(x => x.Id == source.State.Id)) states = states.Append(source.State).ToArray();
        var verified = !await db.GoodsReceiptLines.AnyAsync(x => ids.Contains(x.PurchaseOrderLineId)
            && (x.ExecutionVersion != 1 && (x.AcceptedQuantity != 0 || x.ReceivedQuantity != 0 || x.Status != "Draft" && x.Status != "Cancelled")
                || x.ExecutionVersion == 1 && (x.ReceivingSourceStateId == null || x.SourceStateBindingId != x.ReceivingSourceStateId)
                    && (x.AcceptedQuantity != 0 || x.PhysicalObservationId != null || x.Status != "Draft" && x.Status != "Cancelled")), ct)
            && !await db.ServiceAcceptances.AnyAsync(x => ids.Contains(x.PurchaseOrderLineId) && x.ExecutionVersion != 1
                && (x.AcceptedQuantity != 0 || x.AcceptedAmount != 0 || x.CertifiedAt != null), ct);
        var quantities = new Dictionary<Guid, decimal>();
        foreach (var state in states)
        {
            if (db.Entry(state).State != EntityState.Added)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.receiving_source_states WHERE tenant_id={state.TenantId} AND legal_entity_id={state.LegalEntityId} AND id={state.Id} FOR UPDATE", ct);
            var facts = await Facts(db, state, false, ct);
            quantities[state.PurchaseOrderLineId] = ExactDecimalArithmetic.Add(quantities.GetValueOrDefault(state.PurchaseOrderLineId), ExactDecimalArithmetic.Sum(facts.Effects.Select(x => x.QuantityDelta)));
        }
        verified &= orderLines.All(x => x.AcceptedQuantity == quantities.GetValueOrDefault(x.Id));
        var requestLine = await db.PurchaseRequestLines.SingleAsync(x => x.Id == requestLineId, ct);
        verified &= requestLine.AcceptedQuantity == ExactDecimalArithmetic.Sum(orderLines.Select(x => x.AcceptedQuantity));
        return await Facts(db, source.State, verified, ct);
    }
}
