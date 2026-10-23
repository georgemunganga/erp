using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceReviewRoutes
{
    private static async Task<IResult> Decide(Guid id, Guid assignmentId, DecisionInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        if (input is null || input.InvoiceVersion < 1 || input.CaseVersion < 1
            || input.AssignmentVersion < 1 || input.Action is not ("Approve" or "Return" or "Reject")
            || input.Action == "Approve" && input.Attestation != InvoiceUnitReviewCommands.OriginalUnitAttestation
            || input.Reason?.Length > 2000 || input.Action != "Approve" && string.IsNullOrWhiteSpace(input.Reason)
            || !Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key)
            || key == Guid.Empty)
            return Results.Json(new { code = "PrincipalInvoiceReviewDecisionInvalid" }, statusCode: 422);
        var requestSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 2, operation = "DecideReview", reviewId = id, assignmentId, input
        }, Json));
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token)) throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
            var source = await db.PrincipalInvoiceUnitReviewSources.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new KeyNotFoundException();
            var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token) ?? throw new UnauthorizedAccessException();
            if (actor.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || !actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                || !(await auth.AuthorizeAsync(http.User, InvoiceRetainedUnitReview.ReviewPermission)).Succeeded
                || !actor.OrganisationWide && !actor.AllowedBranches.Contains(source.BranchId))
                throw new UnauthorizedAccessException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, source.BranchId, true, token);
            var invoice = await db.SupplierInvoices.SingleOrDefaultAsync(x => x.Id == source.SupplierInvoiceId
                && x.BranchRef == source.BranchId, token) ?? throw new KeyNotFoundException();
            var receipt = await db.PrincipalInvoiceCommandReceipts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Operation == "DecideReview" && x.CommandKey == key, token);
            if (receipt is not null)
            {
                if (receipt.SupplierInvoiceId != invoice.Id || receipt.ActorPrincipalId != scope.SubjectId
                    || receipt.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                    || receipt.BranchId != source.BranchId || receipt.RequestSha256 != requestSha
                    || receipt.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson))
                    throw new InvalidOperationException("PrincipalInvoiceReviewCommandConflict");
                return JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson, Json);
            }
            if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    source.MakerPrincipalId, source.MakerWorkerAliasId,
                    actor.PrincipalId, actor.CoreLinkedWorkerId, token))
                throw new UnauthorizedAccessException();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={invoice.Id} AND branch_ref={source.BranchId} FOR UPDATE", token);
            await cases.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM core_approval.cases WHERE \"TenantId\"={scope.TenantId} AND \"LegalEntityId\"={scope.LegalEntityId} AND \"Id\"={source.CoreCaseId} FOR UPDATE", token);
            var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleAsync(x => x.Id == source.CoreCaseId
                    && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType
                    && x.SourceId == source.Id && x.SourceVersion == 1, token);
            var pin = await db.PrincipalInvoiceUnitReviewAssignments.SingleOrDefaultAsync(x =>
                x.ReviewSourceId == id && x.CoreAssignmentId == assignmentId, token)
                ?? throw new KeyNotFoundException();
            var assignment = workflow.Stages.SelectMany(x => x.Assignments).SingleOrDefault(x =>
                x.Id == assignmentId && x.AssigneeSubjectId == scope.SubjectId
                    && x.PermissionKey == InvoiceRetainedUnitReview.ReviewPermission
                    && x.DelegatedFromSubjectId is null)
                ?? throw new KeyNotFoundException();
            if (pin.ReviewerPrincipalId != scope.SubjectId
                || pin.ReviewerWorkerAliasId != scope.CoreLinkedWorkerId
                || workflow.Version != input.CaseVersion || assignment.Version != input.AssignmentVersion
                || invoice.Version != input.InvoiceVersion || invoice.FinancePostingRef is not null
                || invoice.ActiveMatchClaimResultId is not null)
                throw new InvalidOperationException("PrincipalInvoiceReviewSourceOrVersionChanged");
            var mapping = await db.PrincipalInvoiceMappingVersions.SingleAsync(x =>
                x.Id == source.MappingVersionId && x.SupplierInvoiceId == invoice.Id, token);
            var state = await db.PrincipalInvoiceStates.SingleAsync(x => x.SupplierInvoiceId == invoice.Id, token);
            if (state.CurrentMappingId != mapping.Id || state.InvoiceVersion != invoice.Version
                || state.State != "Mapped")
                throw new InvalidOperationException("PrincipalInvoiceCurrentMappingRequired");
            var pins = await db.PrincipalInvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id)
                .ToArrayAsync(token);
            var opening = PrincipalInvoiceUnitReviewTransitions.Verify(source, mapping, workflow, pins);
            var matching = await db.PolicyVersions.SingleAsync(x => x.Id == source.MatchingPolicyId, token);
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == source.ReviewPolicyId, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope,
                http.RequestServices, matching, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope,
                http.RequestServices, policy, token);
            var originalLines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id
                && x.IsActive).OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var mappedLines = await db.PrincipalInvoiceMappingLines.Where(x => x.MappingVersionId == mapping.Id)
                .ToArrayAsync(token);
            var retainedSource = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases,
                scope, new HashSet<Guid> { source.BranchId }, invoice,
                mapping.PurchaseOrderId, mapping.PrincipalIssuedVersionId, token);
            _ = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, originalLines,
                retainedSource.Order, retainedSource.Acceptances,
                retainedSource.AcceptanceSourceStates, mappedLines);
            var hashes = await ProcurementPrincipalInvoiceExposureReader.CurrentDocumentHashes(db,
                scope, http.RequestServices, invoice.Id,
                opening.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray(), token);
            if (opening.Lines.Any(x => !hashes.TryGetValue(x.DocumentVersionId, out var hash)
                || hash != x.DocumentSha256))
                throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentsChanged");
            var previous = await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToListAsync(token);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var result = ApprovalEngine.Decide(workflow, assignment,
                new(scope.SubjectId, scope.TenantId, scope.LegalEntityId,
                    new HashSet<string> { InvoiceRetainedUnitReview.ReviewPermission }),
                input.Action, key.ToString("D"), input.Reason, previous, now);
            if (!result.Recorded) throw new InvalidOperationException("PrincipalInvoiceReviewDecisionNotRecorded");
            var vote = previous[^1];
            cases.Decisions.Add(vote);
            if (input.Action == "Approve")
                db.AddScoped(new PrincipalInvoiceUnitReviewAttestation
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    ReviewSourceId = id, CoreCaseId = workflow.Id,
                    CoreAssignmentId = assignment.Id, CoreDecisionId = vote.Id,
                    ReviewerPrincipalId = scope.SubjectId,
                    ReviewerWorkerAliasId = scope.CoreLinkedWorkerId,
                    OriginalUnitsSha256 = source.OriginalUnitsSha256,
                    Statement = InvoiceUnitReviewCommands.OriginalUnitAttestation,
                    AttestedAt = vote.DecidedAt
                });
            PrincipalInvoiceUnitReviewOutcome? terminal = null;
            if (workflow.State == "Approved")
            {
                var retained = await db.PrincipalInvoiceUnitReviewAttestations.Where(x => x.ReviewSourceId == id)
                    .ToListAsync(token);
                if (input.Action == "Approve")
                    retained.Add((PrincipalInvoiceUnitReviewAttestation)db.ChangeTracker.Entries<PrincipalInvoiceUnitReviewAttestation>()
                        .Single(x => x.Entity.CoreDecisionId == vote.Id).Entity);
                terminal = PrincipalInvoiceUnitReviewTerminal.Seal(source, mapping, workflow,
                    pins, retained, previous, matching, policy, hashes, now);
                state.State = "Reviewed";
            }
            else if (workflow.State is "Returned" or "Rejected")
            {
                var evidence = JsonSerializer.Serialize(new { schemaVersion = 2, reviewId = id,
                    caseId = workflow.Id, decisionId = vote.Id, action = input.Action,
                    sourceSha256 = source.OriginalUnitsSha256, at = vote.DecidedAt }, Json);
                terminal = new PrincipalInvoiceUnitReviewOutcome
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    ReviewSourceId = id, CoreCaseId = workflow.Id, FinalDecisionId = vote.Id,
                    State = workflow.State, EvidenceJson = evidence,
                    EvidenceSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(evidence),
                    ReviewerPrincipalId = scope.SubjectId,
                    ReviewerWorkerAliasId = scope.CoreLinkedWorkerId, CompletedAt = vote.DecidedAt
                };
            }
            await cases.SaveChangesAsync(token);
            if (terminal is not null) db.AddScoped(terminal);
            var response = JsonSerializer.Serialize(new { reviewId = id, invoiceId = invoice.Id,
                coreCaseId = workflow.Id, commandKey = key, decisionId = vote.Id,
                outcomeId = terminal?.Id, caseVersion = workflow.Version,
                caseState = workflow.State, reviewState = terminal?.State ?? "Pending",
                sourceSchemaVersion = 2, isUnitVerified = terminal?.State == "Verified",
                canPostToFinance = false }, Json);
            db.AddScoped(new PrincipalInvoiceCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SupplierInvoiceId = invoice.Id, Operation = "DecideReview", CommandKey = key,
                ActorPrincipalId = scope.SubjectId, ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                BranchId = source.BranchId, RequestSha256 = requestSha,
                ResponseJson = response, ResponseSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(response),
                ResponseStatus = 200, CompletedAt = now
            });
            db.AddScoped(new AuditEvent
            {
                RecordType = nameof(PrincipalInvoiceUnitReviewSource), RecordId = id,
                ActorSubjectId = scope.SubjectId, Action = "PrincipalInvoiceOriginalUnitDecision",
                ChangedFieldsJson = JsonSerializer.Serialize(new { reviewId = id,
                    assignmentId, decisionId = vote.Id, action = input.Action,
                    caseState = workflow.State, commandKey = key }, Json)
            });
            await db.SaveChangesAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(response, Json);
        }, ct);
    }
}
