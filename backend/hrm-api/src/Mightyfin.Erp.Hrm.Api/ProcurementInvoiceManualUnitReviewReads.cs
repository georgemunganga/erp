using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static Task<IResult> ManualReadiness(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http,scope,false,async(db,cases,core,auth,token)=>
        {
            var invoice=await Invoice(db,scope,id,token,false);
            var actor=await Actor(core,scope,http,auth,invoice.BranchRef,[InvoiceUnitReviewTransitions.RequestPermission],token);
            if(!actor.Permissions.Contains("procurement-invoice-read")
                || !(await auth.AuthorizeAsync(http.User,null,"procurement-invoice-read")).Succeeded)throw new UnauthorizedAccessException();
            var blockers=new List<string>();Guid? existing=null;object? manualLines=null;
            var enabled=http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>().ManualAttestationEnabled;
            try
            {
                var mapping=await Mapping(db,invoice,token);var originals=await Originals(db,id,token);
                RequireManualMapping(mapping,invoice,originals);
                if(actor.PrincipalId!=mapping.ActorPrincipalId || actor.WorkerId!=mapping.ActorWorkerId)
                    throw new ReviewProblem("InvoiceUnitReviewMakerRequired",409);
                existing=await db.InvoiceUnitReviewSources.Where(x=>x.SupplierInvoiceId==id && x.MappingVersionId==mapping.Id
                    && x.BranchRef==invoice.BranchRef).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(token);
                if(existing is not null)blockers.Add("OriginalUnitReviewAlreadyOpened");
                _=await Policy(db,scope,http,InvoiceMatchingPolicyRules.PolicyKey,token);
                var review=await Policy(db,scope,http,InvoiceRetainedUnitReview.ReviewPolicyKey,token);RequireManualPolicy(review);
                var people=new List<InvoiceUnitReviewPolicyRules.Person>();
                foreach(var principal in InvoiceUnitReviewPolicyRules.Parse(review.RulesJson).Stages.SelectMany(x=>x.Principals).Distinct(StringComparer.Ordinal))
                    people.Add(await ProcurementInvoiceUnitReviewPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,token)
                        ?? throw new ReviewProblem("InvoiceUnitReviewerUnavailable",409));
                foreach(var person in people)
                    if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,actor.PrincipalId,actor.WorkerId,
                        person.PrincipalId,person.WorkerId,token))throw new ReviewProblem("InvoiceUnitReviewIndependentReviewerRequired",409);
                _=InvoiceUnitReviewPolicyRules.Resolve(review,scope.TenantId,scope.LegalEntityId,invoice.BranchRef,
                    mapping.ActorPrincipalId,mapping.ActorWorkerId,people,Now());
                _=await ReviewIssuedOrder(db,cases,core,scope,actor,mapping,mapping.PurchaseOrderId,mapping.IssuedVersionId,token);
                manualLines=InvoiceMappingReviewSourceDispatch.Verify(mapping,invoice,originals).Lines
                    .Select(x=>new{invoiceLineId=x.InvoiceLineId,unitCode=x.InvoiceUnitCode}).ToArray();
            }
            catch(ReviewProblem error)when(error.Status==409){blockers.Add(error.Message);}
            catch(InvalidOperationException error)when(!ProcurementPostgresFailures.IsStoreFailure(error)
                && (error.Message.StartsWith("Invoice",StringComparison.Ordinal)||error.Message.StartsWith("Receiving",StringComparison.Ordinal)))
                {blockers.Add("InvoiceManualUnitReviewEvidenceUnavailable");}
            if(!enabled)blockers.Add("InvoiceManualUnitReviewExecutionDisabled");
            return InvoiceUnitReviewCommands.Serialize(new{invoiceId=id,invoiceVersion=invoice.Version,mappingRevision=invoice.MappingRevision,
                existingReviewId=existing,evidenceMode="ManualAttestation",sourceSchemaVersion=4,requiresMakerReason=true,manualUnitLines=manualLines,
                canRequestReview=blockers.Count==0,blockers,isUnitVerification=false,isInvoiceApproval=false,canPostToFinance=false,
                actionCandidateMeaning="CurrentPrerequisitesOnlyRevalidatedByCommand"});
        },ct);

    private static Task<IResult> ManualTasks(int? page, int? pageSize, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var p = page ?? 1; var size = pageSize ?? 20;
            if (p < 1 || p > 100000 || size < 1 || size > 50) throw new ReviewProblem("InvoiceUnitReviewPageInvalid", 400);
            var actor = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token) ?? throw new UnauthorizedAccessException();
            if (scope.PrincipalId?.ToString("D") != actor.PrincipalId || !actor.Permissions.Contains("procurement-access") || !actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                || !(await auth.AuthorizeAsync(http.User, null, InvoiceRetainedUnitReview.ReviewPermission)).Succeeded) throw new UnauthorizedAccessException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, token);
            // Immutable source branch and current invoice must agree BEFORE count/paging.
            // Current actual Core assignment and its retained principal pin must both match.
            var query = db.InvoiceUnitReviewSources.FromSqlInterpolated($@"
                SELECT s.* FROM procurement.invoice_unit_review_sources s
                JOIN procurement.supplier_invoices i ON i.tenant_id=s.tenant_id AND i.legal_entity_id=s.legal_entity_id AND i.id=s.supplier_invoice_id
                    AND i.branch_ref IS NOT DISTINCT FROM s.branch_ref
                    AND i.current_mapping_version_id=s.mapping_version_id AND i.mapping_revision=s.mapping_revision
                WHERE s.tenant_id={scope.TenantId} AND s.legal_entity_id={scope.LegalEntityId}
                AND s.source_json::jsonb->>'SourceSchemaVersion'='4'
                AND EXISTS (SELECT 1 FROM core_approval.cases c
                    JOIN core_approval.stages g ON g.""CaseId""=c.""Id"" AND g.""TenantId""=c.""TenantId"" AND g.""LegalEntityId""=c.""LegalEntityId""
                    JOIN core_approval.assignments a ON a.""StageId""=g.""Id"" AND a.""CaseId""=c.""Id"" AND a.""TenantId""=c.""TenantId"" AND a.""LegalEntityId""=c.""LegalEntityId""
                    JOIN procurement.invoice_unit_review_assignments w ON w.tenant_id=s.tenant_id AND w.legal_entity_id=s.legal_entity_id AND w.review_source_id=s.id AND w.core_case_id=c.""Id"" AND w.core_assignment_id=a.""Id""
                    WHERE c.""Id""=s.core_case_id AND c.""TenantId""=s.tenant_id AND c.""LegalEntityId""=s.legal_entity_id
                    AND c.""SourceModule""='procurement' AND c.""SourceType""='invoice_unit_review' AND c.""SourceId""=s.id AND c.""SourceVersion""=1
                    AND c.""State""='Pending' AND g.""State""='Active' AND a.""State""='Pending'
                    AND a.""PermissionKey""={InvoiceRetainedUnitReview.ReviewPermission} AND a.""AssigneeSubjectId""={scope.SubjectId}
                    AND w.principal_id={scope.SubjectId})").AsNoTracking();
            if (scope.WorkLocationId is { } selected) query = query.Where(x => x.BranchRef == selected);
            if (!actor.OrganisationWide)
            {
                var allowed = actor.AllowedBranches.ToArray();
                query = query.Where(x => x.BranchRef != null && allowed.Contains(x.BranchRef.Value));
            }
            var total = await query.CountAsync(token);
            var rows = await query.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id).Skip((p - 1) * size).Take(size)
                .Select(x => new { reviewId = x.Id, invoiceId = x.SupplierInvoiceId,
                    invoiceNumber = db.SupplierInvoices.Where(invoice => invoice.TenantId == scope.TenantId
                        && invoice.LegalEntityId == scope.LegalEntityId && invoice.Id == x.SupplierInvoiceId
                        && invoice.BranchRef == x.BranchRef && invoice.CurrentMappingVersionId == x.MappingVersionId
                        && invoice.MappingRevision == x.MappingRevision).Select(invoice => invoice.InvoiceNumber).FirstOrDefault(),
                    mappingVersionId = x.MappingVersionId, x.MappingRevision,
                    branchId = x.BranchRef, x.OpenedAt, canPostToFinance = false, isInvoiceApproval = false }).ToArrayAsync(token);
            return InvoiceUnitReviewCommands.Serialize(new { items = rows, total, page = p, pageSize = size, availabilityMeaning = "CurrentMappingAssignmentsOnly", canPostToFinance = false });
        }, ct);

    private static Task<IResult> ManualDetail(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (source, invoice) = await Source(db, scope, id, false, token);
            RequireManualSource(source);
            var actor = await Actor(core, scope, http, auth, source.BranchRef,
                [InvoiceRetainedUnitReview.ReviewPermission, InvoiceUnitReviewTransitions.RequestPermission, InvoiceUnitReviewTransitions.ReadPermission], token);
            var workflow = await Workflow(cases, scope, source, false, token);
            var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            var owned = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
            var maker = actor.PrincipalId == source.MakerPrincipalId
                && actor.Permissions.Contains(InvoiceUnitReviewTransitions.RequestPermission) && actor.Permissions.Contains("procurement-invoice-read");
            var assigned = actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                && pins.Any(x => x.PrincipalId == actor.PrincipalId);
            var reader = actor.Permissions.Contains(InvoiceUnitReviewTransitions.ReadPermission) && actor.Permissions.Contains("procurement-invoice-read");
            if (!maker && !assigned && !reader) throw new UnauthorizedAccessException();
            var outcome = await db.InvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x => x.ReviewSourceId == id, token);
            var currentMapping = invoice.CurrentMappingVersionId == source.MappingVersionId && invoice.MappingRevision == source.MappingRevision;
            var originals = await Originals(db, invoice.Id, token);
            var originalDigestCurrent = InvoiceMappingEvidence.OriginalDigest(invoice, originals) == source.OriginalSha256;
            var blockers = new List<string>();
            var manualEnabled = http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>().ManualAttestationEnabled;
            if (!manualEnabled) blockers.Add("InvoiceManualUnitReviewExecutionDisabled");
            if (!currentMapping) blockers.Add("InvoiceMappingSuperseded");
            if (!originalDigestCurrent) blockers.Add("InvoiceOriginalChanged");
            // Same invoice only; no source-PO lines, other allocations or Finance references.
            // Exact original line values make the named document/unit mapping reviewable
            // without granting a purpose reviewer general invoice list/detail access.
            object? original = !originalDigestCurrent ? null : new { invoice.InvoiceNumber, invoice.InvoiceDate, invoice.CurrencyCode,
                netAmount = ProcurementEvidenceEncoding.Decimal(invoice.NetAmount), taxAmount = ProcurementEvidenceEncoding.Decimal(invoice.TaxAmount),
                grossAmount = ProcurementEvidenceEncoding.Decimal(invoice.GrossAmount),
                lines = originals.Select(x => new { x.Id, x.LineNumber, x.Description, quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity),
                    unitPrice = ProcurementEvidenceEncoding.Decimal(x.UnitPrice), taxAmount = ProcurementEvidenceEncoding.Decimal(x.TaxAmount) }).ToArray() };
            var independent = await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                source.MakerPrincipalId, source.MakerWorkerId, actor.PrincipalId, actor.WorkerId, token);
            if (assigned && !independent) blockers.Add("InvoiceUnitReviewIndependentReviewerRequired");
            var assignments = workflow.Stages.Where(x => manualEnabled && independent && currentMapping && originalDigestCurrent && workflow.State == "Pending" && x.State == "Active").SelectMany(x => x.Assignments)
                .Where(x => x.State == "Pending" && x.AssigneeSubjectId == actor.PrincipalId && pins.Any(w => w.CoreAssignmentId == x.Id && w.PrincipalId == actor.PrincipalId))
                .Select(x => new { assignmentId = x.Id, assignmentVersion = x.Version }).ToArray();
            return InvoiceUnitReviewCommands.Serialize(new { reviewId = id, invoiceId = invoice.Id, invoiceVersion = invoice.Version,
                makerPrincipalId = source.MakerPrincipalId, identitySchemaVersion = owned.Source.IdentitySchemaVersion,
                evidenceSchemaVersion = outcome?.EvidenceSchemaVersion, mappingVersionId = source.MappingVersionId, source.MappingRevision, coreCaseId = workflow.Id, caseVersion = workflow.Version,
                caseState = workflow.State, reviewState = outcome?.State ?? "Pending", currentMapping, originalDigestCurrent, original, blockers, assignments,
                evidenceMode = "ManualAttestation", sourceSchemaVersion = 4,
                manualUnitLines = owned.Source.ManualLines, makerReason = owned.Source.ManualReason,
                attestation = InvoiceUnitReviewCommands.ManualUnitAttestation,
                actionCandidates = assignments.Length == 0 ? Array.Empty<string>() : new[] { "Approve", "Return", "Reject" },
                actionCandidateMeaning = "CurrentAssignmentOnlyRevalidatedByCommand", isInvoiceApproval = false, canPostToFinance = false });
        }, ct);

    private static Task<IResult> ManualRecover(Guid id, Guid key, string? operation, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http, scope, false, async (db, cases, core, auth, token) =>
        {
            if (key == Guid.Empty) throw new ReviewProblem("InvoiceUnitReviewCommandKeyRequired", 400);
            if (operation is not ("Open" or "Decision")) throw new ReviewProblem("InvoiceUnitReviewOperationRequired", 400);
            var (source, invoice) = await Source(db, scope, id, false, token);
            RequireManualSource(source);
            var receipt = await db.InvoiceUnitReviewReceipts.SingleOrDefaultAsync(x => x.ReviewSourceId == id && x.Operation == operation && x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, token) ?? throw new KeyNotFoundException();
            var action = receipt.Operation == "Open" ? InvoiceUnitReviewTransitions.RequestPermission : InvoiceRetainedUnitReview.ReviewPermission;
            var actor = await Actor(core, scope, http, auth, source.BranchRef, [action], token);
            if (receipt.ActorPrincipalId != actor.PrincipalId) throw new UnauthorizedAccessException();
            if (receipt.Operation == "Open" && (!actor.Permissions.Contains("procurement-invoice-read") || !(await auth.AuthorizeAsync(http.User, null, "procurement-invoice-read")).Succeeded)) throw new UnauthorizedAccessException();
            return ManualRecordedValue(receipt,source,invoice,scope,operation,key);
        }, ct);

    // Open recovery cannot require a review ID that was contained only in a lost ACK.
    // The current invoice branch/action is authorized before loading an actor-owned receipt.
    private static Task<IResult> ManualRecoverOpen(Guid id,Guid key,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
        => RunManual(http,scope,false,async(db,cases,core,auth,token)=>
        {
            if(key==Guid.Empty)throw new ReviewProblem("InvoiceUnitReviewCommandKeyRequired",400);
            var invoice=await Invoice(db,scope,id,token,false);
            var actor=await Actor(core,scope,http,auth,invoice.BranchRef,
                [InvoiceUnitReviewTransitions.RequestPermission],token);
            if(!actor.Permissions.Contains("procurement-invoice-read")
                || !(await auth.AuthorizeAsync(http.User,null,"procurement-invoice-read")).Succeeded)
                throw new UnauthorizedAccessException();
            var receipt=await (from row in db.InvoiceUnitReviewReceipts
                join source in db.InvoiceUnitReviewSources on row.ReviewSourceId equals source.Id
                where row.ActorPrincipalId==scope.SubjectId && row.Operation=="Open" && row.CommandKey==key
                    && source.SupplierInvoiceId==id && source.BranchRef==invoice.BranchRef
                    && source.TenantId==scope.TenantId && source.LegalEntityId==scope.LegalEntityId
                select row).SingleOrDefaultAsync(token)??throw new KeyNotFoundException();
            var retained=await db.InvoiceUnitReviewSources.SingleAsync(x=>x.Id==receipt.ReviewSourceId,token);
            return ManualRecordedValue(receipt,retained,invoice,scope,"Open",key);
        },ct);

    internal static string ManualRecordedValue(InvoiceUnitReviewCommandReceipt receipt,
        InvoiceUnitReviewSource source,SupplierInvoice invoice,ProcurementRequestScope scope,
        string operation,Guid key)
    {
        // These checks deliberately do not use current mapping/policy/version.
        if(key==Guid.Empty || operation is not ("Open" or "Decision")
            || receipt.CommandKey!=key || receipt.Operation!=operation
            || receipt.ActorPrincipalId!=scope.SubjectId || receipt.ReviewSourceId!=source.Id
            || source.TenantId!=scope.TenantId || source.LegalEntityId!=scope.LegalEntityId
            || receipt.TenantId!=scope.TenantId || receipt.LegalEntityId!=scope.LegalEntityId
            || invoice.TenantId!=scope.TenantId || invoice.LegalEntityId!=scope.LegalEntityId
            || source.SupplierInvoiceId!=invoice.Id || source.BranchRef!=invoice.BranchRef)
            throw new KeyNotFoundException();
        RequireManualSource(source);
        var response=InvoiceUnitReviewCommands.ReadHistorical(receipt,source);
        if(response.ReviewId!=source.Id || response.InvoiceId!=invoice.Id || response.CommandKey!=key
            || !response.RecordedHistoricalResult || response.CanPostToFinance || response.IsInvoiceApproval)
            throw new ReviewProblem("InvoiceUnitReviewReceiptInvalid",409);
        return receipt.ResponseJson;
    }

}
