using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static readonly string[] ReadActions = ["procurement-po-read", "procurement-po-edit", "procurement-po-convert",
        "procurement-po-submit", "procurement-po-approve", "procurement-po-finance-confirm", "procurement-po-issue", "procurement-po-dispatch-confirm"];

    private static async Task<PurchaseOrderExecutionReadActor> ReadActor(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, CancellationToken ct)
    {
        await Actor(core, scope, http, auth, "procurement-access", ct);
        var account = await Canonical(core, scope, scope.SubjectId, ct);
        var branches = await CurrentRecordScope(core, scope, null, false, ct);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in ReadActions)
            if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, action, ct) is not null)
                permissions.Add(action);
        return new(scope.TenantId, scope.LegalEntityId, scope.SubjectId, account.WorkerId, true,
            scope.WorkLocationId, branches.ToHashSet(), permissions);
    }

    private static async Task<IResult> ExecutionWorklist(string? view, string? status, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, int? page, int? pageSize, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct);
            if (view is not ("buyer" or "finance" or "dispatch")) throw new OrderProblem("OrderWorklistViewRequired", 400);
            var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var reply = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var actor = await ReadActor(core, scope, http, auth, token);
                var required = view == "buyer" ? "procurement-po-read"
                    : view == "finance" ? "procurement-po-finance-confirm" : "procurement-po-dispatch-confirm";
                if (!PurchaseOrderExecutionReadContract.Has(actor, required)) Deny();
                var branches = actor.BranchIds.ToArray();
                var query = db.PurchaseOrders.AsNoTracking().Where(x =>
                    (scope.WorkLocationId == null || x.BranchRef == scope.WorkLocationId)
                    && (branches.Length == 0 || x.BranchRef != null && branches.Contains(x.BranchRef.Value))
                    && db.PurchaseOrderLines.Any(l => l.PurchaseOrderId == x.Id && l.IsActive)
                    && !db.PurchaseOrderLines.Any(l => l.PurchaseOrderId == x.Id && l.IsActive && l.PurchaseRequestLineId == null));
                // Purpose-specific visibility is applied before counting/paging. Finance/dispatch
                // command grants imply these narrow reads, never general ownership or PO-read grants.
                if (view == "buyer") query = query.Where(x => x.BuyerSubjectId == scope.SubjectId);
                else if (view == "finance") query = query.Where(x => x.Status == "Draft" || x.Status == "Returned"
                    || x.Status == "Rejected" || db.OrderFinanceEvidence.Any(f => f.PurchaseOrderId == x.Id && f.VerifiedByPrincipalId == scope.SubjectId));
                else query = query.Where(x => db.OrderIssuedVersions.Any(i => i.PurchaseOrderId == x.Id)
                    && (x.Status == "IssuePending" || x.Status == "Issued"
                        || db.OrderDeliveryEvidence.Any(d => d.VerifiedByPrincipalId == scope.SubjectId
                            && db.OrderIssuedVersions.Any(i => i.Id == d.IssuedVersionId && i.PurchaseOrderId == x.Id))));
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                var total = await query.CountAsync(token);
                var orders = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id)
                    .Skip((number - 1) * size).Take(size)
                    .Select(x => new { Order = x, SupplierName = db.Suppliers.Where(s => s.Id == x.SupplierId)
                        .Select(s => s.DisplayName).FirstOrDefault() }).ToArrayAsync(token);
                var items = new List<object>();
                foreach (var row in orders)
                {
                    var order = row.Order;
                    items.Add(new { order.Id, order.Number, order.SupplierId, row.SupplierName, order.BranchRef, order.Status, order.CurrencyCode,
                        order.Version, order.CurrentRevision, order.ExpectedDeliveryOn,
                        detailPath = $"/api/procurement/v1/orders/{order.Id:D}/execution-detail", actionCandidatesAvailableInDetail = true });
                }
                return new { items, total, page = number, pageSize = size, view };
            }, ct);
            return Results.Ok(reply);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> ExecutionDetail(Guid id, HttpContext http, ProcurementRequestScope scope,
        ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness, ProcurementApprovalTransaction transaction,
        IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct);
            var reply = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var actor = await ReadActor(core, scope, http, auth, token);
                var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new OrderProblem("OrderNotFound", 404);
                await CurrentRecordScope(core, scope, order.BranchRef, true, token);
                var facts = await ReadFacts(db, cases, core, scope, options, actor, order, token);
                if (!PurchaseOrderExecutionReadContract.CanRead(actor, facts)) Missing();
                var lines = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == id && x.IsActive)
                    .OrderBy(x => x.LineNumber).ToArrayAsync(token);
                var totals = PurchaseOrderExecutionReadAmounts.Calculate(lines);
                var supplierName = await db.Suppliers.AsNoTracking().Where(x => x.Id == order.SupplierId)
                    .Select(x => x.DisplayName).SingleOrDefaultAsync(token);
                var finance = await db.OrderFinanceEvidence.AsNoTracking().Where(x => x.PurchaseOrderId == id)
                    .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToArrayAsync(token);
                var resolutions = await db.OrderFinanceResolutions.AsNoTracking().Where(x => x.PurchaseOrderId == id)
                    .OrderBy(x => x.ConfirmedAt).ToArrayAsync(token);
                var submissions = await db.OrderSubmissions.AsNoTracking().Where(x => x.PurchaseOrderId == id)
                    .OrderBy(x => x.CommercialRevision).ToArrayAsync(token);
                var caseIds = submissions.Select(x => x.CoreApprovalCaseId).ToArray();
                var workflows = await cases.Cases.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .Where(x => caseIds.Contains(x.Id) && x.SourceModule == "procurement" && x.SourceType == "purchase_order" && x.SourceId == id)
                    .OrderBy(x => x.SourceVersion).ToArrayAsync(token);
                var decisions = await cases.Decisions.AsNoTracking().Where(x => caseIds.Contains(x.CaseId))
                    .OrderBy(x => x.DecidedAt).Select(x => new { x.Id, x.CaseId, x.AssignmentId, x.Action, x.Reason, x.ActorSubjectId, x.DecidedAt }).ToArrayAsync(token);
                var issued = await db.OrderIssuedVersions.AsNoTracking().Where(x => x.PurchaseOrderId == id)
                    .OrderBy(x => x.RevisionNumber).ToArrayAsync(token);
                var issuedIds = issued.Select(x => x.Id).ToArray();
                var deliveries = await db.OrderDeliveryEvidence.AsNoTracking().Where(x => issuedIds.Contains(x.IssuedVersionId))
                    .OrderBy(x => x.ObservedAt).ToArrayAsync(token);
                var reservations = finance.FirstOrDefault()?.SourceReservationsJson;
                if (reservations is null)
                {
                    var pins = await ReadSourcePins(db, lines, token);
                    var ids = pins?.Select(x => x.FinanceSnapshotId).ToArray() ?? [];
                    var refs = await db.BudgetCheckSnapshots.AsNoTracking().Where(x => ids.Contains(x.Id))
                        .Select(x => x.ReservationRef).Distinct().ToArrayAsync(token);
                    reservations = JsonSerializer.Serialize(refs.Where(x => !string.IsNullOrWhiteSpace(x)).Order(StringComparer.Ordinal));
                }
                return new
                {
                    order = new { order.Id, order.Number, order.SupplierId, supplierName, order.CurrencyCode,
                        order.Status, order.BuyerSubjectId, order.BranchRef, order.ShipToRef, order.BillToRef,
                        order.PaymentTermRef, order.ExpectedDeliveryOn, order.ClosureReason, order.Version,
                        order.CreatedAt, order.CurrentRevision, order.SubmittedBusinessVersion, order.SubmittedAt,
                        order.ApprovalInstanceRef, total = totals.Gross,
                        lines = lines.Select(x => new { x.Id, x.LineNumber, x.Description, x.UnitCode,
                            quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity), unitPrice = ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
                            taxAmount = ProcurementEvidenceEncoding.Decimal(x.TaxAmount),
                            acceptedQuantity = ProcurementEvidenceEncoding.Decimal(x.AcceptedQuantity),
                            invoicedQuantity = ProcurementEvidenceEncoding.Decimal(x.InvoicedQuantity), x.CostCenterRef, x.ProjectRef,
                            sourcePurchaseRequestLineId = x.PurchaseRequestLineId, x.ExpectedDeliveryOn }) },
                    totals = new { lineBaseAmount = totals.LineBase, grossAmount = totals.Gross },
                    nextCommercialRevision = order.Status == "Draft" ? order.CurrentRevision + 1 : order.CurrentRevision,
                    financeStatus = facts.FinanceHeld ? "CommitmentHeld" : "NoConfirmedCommitmentHeld",
                    sourceReservationReferences = JsonSerializer.Deserialize<string[]>(reservations),
                    financeEvidence = finance.Select(x => new { x.Id, x.CommercialRevision, x.CommitmentReference, x.EvidenceReference,
                        x.EvidenceMode, x.ApprovedManualPolicyReference, x.Status, x.CurrencyCode,
                        confirmedGrossAmount = ProcurementEvidenceEncoding.Decimal(x.ConfirmedGrossAmount), x.ConfirmedAt,
                        x.CommercialSnapshotSha256, x.VerifiedByPrincipalId, x.ReplacesCommitmentReference,
                        releaseConfirmed = resolutions.Any(r => r.FinanceEvidenceId == x.Id) }),
                    financeResolutions = resolutions.Select(x => new { x.Id, x.FinanceEvidenceId, x.SubmissionId, x.EvidenceReference,
                        x.CommitmentReference, x.Outcome, x.CurrencyCode, releasedGrossAmount = ProcurementEvidenceEncoding.Decimal(x.ReleasedGrossAmount), x.ConfirmedAt, x.Reason }),
                    submissions = submissions.Select(x => new { x.Id, x.CommercialRevision, x.CoreApprovalCaseId, x.PolicyVersionId,
                        x.PolicyVersionNumber, x.FinanceEvidenceId, x.SubmittedAt, x.CommercialSnapshotSha256 }),
                    cases = workflows.Select(x => new { x.Id, x.SourceVersion, x.State, x.Version,
                        stages = x.Stages.Select(s => new { s.Id, s.Code, s.Sequence, s.State,
                            assignments = s.Assignments.Select(a => new { a.Id, a.AssigneeSubjectId, a.State, a.Version,
                                assignedToMe = a.AssigneeSubjectId == scope.SubjectId }) }) }),
                    decisions,
                    issued = issued.Select(x => new { x.Id, x.SubmissionId, x.RevisionNumber, x.AuthorizedAt, x.CommercialSnapshotSha256 }),
                    deliveries = deliveries.Select(x => new { x.Id, x.IssuedVersionId, x.DeliveryReference, x.Outcome, x.Channel,
                        x.EvidenceReference, x.ObservedAt, x.VerifiedByPrincipalId }),
                    actionCandidates = PurchaseOrderExecutionReadContract.Actions(actor, facts),
                    configuredDeliveryChannels = options.HasApprovedDeliveryPolicy ? options.AllowedDeliveryChannels : [],
                    currentFinancePolicyConfigured = facts.CurrentFinancePolicy,
                    currentDispatchPolicyConfigured = facts.CurrentDispatchPolicy,
                    commandValidationRequired = true,
                };
            }, ct);
            return Results.Ok(reply);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> RecoverCommercialCommand(string operation, Guid key, Guid? orderId,
        HttpContext http, ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        if (ProcurementReceivingRoutes.SupportsRecovery(operation))
            return await ProcurementReceivingRoutes.Recover(operation, key, orderId, http, scope, ct);
        try
        {
            await EnabledAsync(options, readiness, ct);
            if (key == Guid.Empty || !PurchaseOrderExecutionReadContract.OperationPermissions.TryGetValue(operation, out var permission))
                throw new OrderProblem("OrderCommandRecoveryInputInvalid", 400);
            var reply = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, permission, token);
                _ = await Canonical(core, scope, scope.SubjectId, token);
                var receipt = await db.CommercialCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Operation == operation && x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId
                    && (orderId == null || x.PurchaseOrderId == orderId), token)
                    ?? throw new OrderProblem("OrderCommandReceiptNotFound", 404);
                var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.PurchaseOrderId, token)
                    ?? throw new OrderProblem("OrderNotFound", 404);
                await CurrentRecordScope(core, scope, order.BranchRef, true, token);
                using var result = JsonDocument.Parse(receipt.ResponseJson);
                return new { state = "RecordedHistoricalOutcome", receipt.PurchaseOrderId, receipt.Operation,
                    receipt.CommandKey, receipt.Fingerprint, receipt.CompletedAt, recordedResult = result.RootElement.Clone(),
                    currentOrderVersion = order.Version, currentOrderStatus = order.Status, replayAuthorized = false,
                    guidance = "This confirms a recorded result only. Retain the original command body and key; every retry rechecks current authority." };
            }, ct);
            return Results.Ok(reply);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<PurchaseOrderExecutionReadFacts> ReadFacts(ProcurementDbContext db, ApprovalDbContext cases,
        HrmDbContext core, ProcurementRequestScope scope, ProcurementOrderExecutionOptions options,
        PurchaseOrderExecutionReadActor actor, PurchaseOrder order, CancellationToken ct)
    {
        var lines = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == order.Id && x.IsActive).ToArrayAsync(ct);
        var submission = await db.OrderSubmissions.AsNoTracking().Where(x => x.PurchaseOrderId == order.Id)
            .OrderByDescending(x => x.CommercialRevision).FirstOrDefaultAsync(ct);
        var finance = await db.OrderFinanceEvidence.AsNoTracking().Where(x => x.PurchaseOrderId == order.Id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var issued = await db.OrderIssuedVersions.AsNoTracking().Where(x => x.PurchaseOrderId == order.Id)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefaultAsync(ct);
        var resolution = finance is null ? null : await db.OrderFinanceResolutions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.FinanceEvidenceId == finance.Id && x.PurchaseOrderId == order.Id, ct);
        var caseQuery = cases.Cases.AsNoTracking().Where(x => x.SourceModule == "procurement" && x.SourceType == "purchase_order"
            && x.SourceId == order.Id);
        var caseIds = await caseQuery.Select(x => x.Id).ToArrayAsync(ct);
        var assignments = await cases.Assignments.AsNoTracking().Where(x => caseIds.Contains(x.CaseId)).ToArrayAsync(ct);
        var activeStageIds = await cases.Stages.AsNoTracking().Where(x => caseIds.Contains(x.CaseId) && x.State == "Active")
            .Select(x => x.Id).ToArrayAsync(ct);
        var pendingCases = await caseQuery.Where(x => x.State == "Pending").Select(x => x.Id).ToArrayAsync(ct);
        var participantJson = await ReadParticipantWorkers(db, core, scope, order, lines, submission, finance, ct);
        var independent = participantJson is not null && PurchaseOrderWorkerIdentity.IsIndependent(participantJson, actor.WorkerId);
        var independentRelease = independent;
        if (submission is not null)
        {
            var retained = await db.OrderAssignmentAuthority.AsNoTracking().Where(x => x.SubmissionId == submission.Id).ToArrayAsync(ct);
            var tasks = assignments.Where(x => x.CaseId == submission.CoreApprovalCaseId).ToArray();
            independentRelease = independent && tasks.Length > 0 && tasks.Length == retained.Length
                && tasks.All(x => retained.Any(p => p.CoreAssignmentId == x.Id && p.CoreStageId == x.StageId && p.PrincipalId == x.AssigneeSubjectId))
                && retained.All(x => PurchaseOrderAssignmentIdentity.ReadVerified(x) is { } identity && identity.AssigneeWorkerId != actor.WorkerId);
        }
        var now = DateTimeOffset.UtcNow;
        var policyReady = options.HasCurrentManualCommitmentPolicy(scope.LegalEntityId, now);
        var currentFinance = false;
        if (finance is not null && resolution is null && policyReady && finance.CommitmentReference == order.FinanceCommitmentRef
            && finance.CommercialRevision == (order.Status == "Draft" ? order.CurrentRevision + 1 : order.CurrentRevision))
        {
            var policy = ManualPolicy(options, scope, now);
            var verifier = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
                scope.LegalEntityId, finance.VerifiedByPrincipalId, ct);
            if (verifier is not null)
            {
                var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, verifier.RolesCsv, ct);
                var admin = LocalIdentityRoutes.ParseRoles(verifier.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase);
                currentFinance = finance.Status == "Confirmed" && finance.CurrencyCode == order.CurrencyCode
                    && finance.ApprovedManualPolicyReference == policy.ApprovedPolicyReference
                    && finance.VerifiedByWorkerId == verifier.WorkerId && finance.ConfirmedAt <= now
                    && (policy.EvidenceMaximumAgeMinutes is null || finance.ConfirmedAt >= now.AddMinutes(-policy.EvidenceMaximumAgeMinutes.Value))
                    && (admin || actions.Contains("procurement-access") && actions.Contains("procurement-po-finance-confirm"));
            }
        }
        var dispatchedByActor = issued is not null && await db.OrderDeliveryEvidence.AsNoTracking().AnyAsync(x =>
            x.VerifiedByPrincipalId == scope.SubjectId && db.OrderIssuedVersions.Any(i => i.Id == x.IssuedVersionId && i.PurchaseOrderId == order.Id), ct);
        var dispatchIndependent = issued is not null && submission is not null && issued.SubmissionId == submission.Id
            && issued.CommercialSnapshotSha256 == submission.CommercialSnapshotSha256
            && issued.CommercialSnapshotSha256 == ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
            && issued.ParticipantWorkersJson == submission.ParticipantWorkersJson
            && PurchaseOrderWorkerIdentity.IsIndependent(issued.ParticipantWorkersJson, actor.WorkerId);
        return new()
        {
            TenantId = order.TenantId, LegalEntityId = order.LegalEntityId, BranchId = order.BranchRef,
            BuyerPrincipalId = order.BuyerSubjectId, Status = order.Status,
            SourceControlled = lines.Length > 0 && lines.All(x => x.PurchaseRequestLineId.HasValue),
            AssignedToActor = assignments.Any(x => x.AssigneeSubjectId == scope.SubjectId && x.PermissionKey == "procurement-po-approve"),
            PendingAssignmentToActor = assignments.Any(x => x.AssigneeSubjectId == scope.SubjectId && x.PermissionKey == "procurement-po-approve"
                && x.State == "Pending" && activeStageIds.Contains(x.StageId) && pendingCases.Contains(x.CaseId)),
            RecordedFinanceActor = await db.OrderFinanceEvidence.AsNoTracking().AnyAsync(x => x.PurchaseOrderId == order.Id && x.VerifiedByPrincipalId == scope.SubjectId, ct),
            RecordedDispatchActor = dispatchedByActor, FinanceIndependent = independent, FinanceReleaseIndependent = independentRelease,
            DispatchIndependent = dispatchIndependent, HasIssuedVersion = issued is not null,
            HasSubmission = submission is not null,
            DraftReopened = submission is not null && await db.OrderFinanceResolutions.AsNoTracking().AnyAsync(x =>
                x.PurchaseOrderId == order.Id && x.SubmissionId == submission.Id && x.Outcome == "ReopenedDraft", ct),
            FinanceHeld = order.FinanceCommitmentRef is not null || finance is not null && resolution is null,
            CurrentFinanceEvidence = currentFinance, CurrentFinancePolicy = policyReady,
            CurrentDispatchPolicy = options.HasApprovedDeliveryPolicy,
            PublishedOrderPolicy = await db.PolicyVersions.AsNoTracking().CountAsync(x => x.PolicyKey == "PURCHASE_ORDER"
                && x.State == "Published" && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo), ct) == 1,
        };
    }

    private static async Task<string?> ReadParticipantWorkers(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, PurchaseOrder order, PurchaseOrderLine[] lines, PurchaseOrderSubmission? submission,
        PurchaseOrderFinanceEvidence? finance, CancellationToken ct)
    {
        if (submission is not null)
            return submission.BuyerPrincipalId == order.BuyerSubjectId && submission.WorkerIdentitySha256 == PurchaseOrderWorkerIdentity.Digest(
                order, submission.CommercialRevision, submission.CoreApprovalCaseId, submission.BuyerWorkerId, submission.ParticipantWorkersJson)
                ? submission.ParticipantWorkersJson : null;
        if (finance is not null)
            return finance.WorkerIdentitySha256 == PurchaseOrderWorkerIdentity.Digest(order, finance.CommercialRevision, Guid.Empty,
                finance.BuyerWorkerId, finance.ParticipantWorkersJson) ? finance.ParticipantWorkersJson : null;
        var buyer = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, order.BuyerSubjectId, ct);
        if (buyer is null) return null;
        var pins = await ReadSourcePins(db, lines, ct);
        if (pins is null || pins.Any(x => !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(x))) return null;
        return JsonSerializer.Serialize(pins.SelectMany(x => new[] { x.MakerWorkerId!.Value, x.RequesterWorkerId!.Value })
            .Append(buyer.WorkerId).Distinct().Order().ToArray());
    }

    private static async Task<PurchaseRequestApprovalSubmission[]?> ReadSourcePins(ProcurementDbContext db, PurchaseOrderLine[] lines, CancellationToken ct)
    {
        if (lines.Length == 0 || lines.Any(x => x.PurchaseRequestLineId is null)) return null;
        var lineIds = lines.Select(x => x.PurchaseRequestLineId!.Value).ToArray();
        var requests = await (from line in db.PurchaseRequestLines.AsNoTracking()
            join request in db.PurchaseRequests.AsNoTracking() on line.PurchaseRequestId equals request.Id
            where lineIds.Contains(line.Id)
            select new { line.Id, request.ApprovalInstanceRef }).ToArrayAsync(ct);
        if (requests.Length != lineIds.Length || requests.Any(x => !Guid.TryParse(x.ApprovalInstanceRef, out _))) return null;
        var caseIds = requests.Select(x => Guid.Parse(x.ApprovalInstanceRef!)).Distinct().ToArray();
        var pins = await db.ApprovalSubmissions.AsNoTracking().Where(x => caseIds.Contains(x.CoreApprovalCaseId)).ToArrayAsync(ct);
        return pins.Length == caseIds.Length ? pins : null;
    }
}
