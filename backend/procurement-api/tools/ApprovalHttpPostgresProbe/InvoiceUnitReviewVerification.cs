using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    // TestServer only; real Procurement HTTP/store/approval/document services. Fixture
    // human decision is synthetic and never asserts complete external Finance history.
    public async Task VerifyInvoiceUnitReviews()
    {
        Require(OrderDistinctRuntime, "Original-unit review needs a separate finite runtime.");
        _ = await PrepareMatchingBrowserManifest(5197); // Real upstream source, no invoice/document/review rows seeded.
        var mapper = matchingBrowserActors["mapper"];
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "browser_matching_mapper");
            role.PermissionsCsv += "," + InvoiceUnitReviewTransitions.RequestPermission;
            var reviewRole = new TenantRoleAssignment { RoleKey = "unit_original_reviewer", RoleName = "Synthetic original unit reviewer", Active = true,
                PermissionsCsv = "procurement-access," + InvoiceRetainedUnitReview.ReviewPermission };
            core.TenantRoleAssignments.Add(reviewRole);
            (await core.LocalUsers.SingleAsync(x => x.Id == OrderManager2)).RolesCsv = reviewRole.RoleKey;
            await core.SaveChangesAsync();
        }
        await VerifyUnitReviewMetadataExecutionStore(mapper.Principal);
        await PublishUnitReviewPolicy();
        var original = await CaptureUnitOriginal(mapper.Principal, "UNIT-REVIEW-ORIGINAL-1");
        var invoicePath = $"{Root}/invoices/{original.Invoice}";
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, invoicePath, OrderManager2); // Purpose grant is not broad invoice read.
        var openInput = new ProcurementInvoiceUnitReviewRoutes.OpenInput(original.Version, original.MappingRevision, "Inspect the exact supplier original unit");
        var openKey = Guid.NewGuid();
        var openResponse = await Send(HttpMethod.Post, invoicePath + "/unit-reviews", mapper.Principal, openInput, openKey);
        await Check(openResponse, HttpStatusCode.OK); var openedText = await openResponse.Content.ReadAsStringAsync(); var opened = JsonSerializer.Deserialize<JsonElement>(openedText);
        var reviewId = opened.GetProperty("reviewId").GetGuid(); var coreCase = opened.GetProperty("coreCaseId").GetGuid();
        var reviewPath = $"{Root}/invoice-unit-reviews/{reviewId}";
        var replay = await Send(HttpMethod.Post, invoicePath + "/unit-reviews", mapper.Principal, openInput, openKey);
        await Check(replay, HttpStatusCode.OK); Require(await replay.Content.ReadAsStringAsync() == openedText, "Original review opening replay changed.");
        var queue = await Get(Root + "/invoice-unit-reviews/tasks", OrderManager2);
        Require(queue.GetProperty("total").GetInt32() == 1 && queue.GetProperty("items")[0].GetProperty("reviewId").GetGuid() == reviewId, "Assigned purpose-only review queue is incomplete.");
        var detail = await Get(reviewPath, OrderManager2); var assignment = detail.GetProperty("assignments")[0];
        Require(detail.GetProperty("originalDigestCurrent").GetBoolean()
            && detail.GetProperty("original").GetProperty("lines")[0].GetProperty("quantity").GetString() == "1"
            && detail.GetProperty("original").GetProperty("lines")[0].GetProperty("unitPrice").GetString() == "100"
            && !detail.TryGetProperty("sourceAllocations", out _) && !detail.TryGetProperty("financeEvidence", out _),
            "Purpose review detail lost exact own-invoice facts or exposed unrelated owner history.");
        var documentList = await Get(reviewPath + "/documents", OrderManager2);
        Require(documentList.GetProperty("items").GetArrayLength() == 1 && documentList.GetProperty("items")[0].GetProperty("versionId").GetGuid() == original.Document,
            "Purpose reviewer could not resolve the exact original document.");
        var download = await Send(HttpMethod.Get, reviewPath + $"/documents/{original.Document}/download", OrderManager2);
        await Check(download, HttpStatusCode.OK); Require((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(MatchingDocumentBytes), "Actual Core pinned download bytes changed.");
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, reviewPath + $"/documents/{Guid.NewGuid()}/download", OrderManager2);
        var beforeDenied = await UnitReviewFootprint();
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, reviewPath + $"/assignments/{assignment.GetProperty("assignmentId").GetGuid()}/decisions", mapper.Principal,
            new ProcurementInvoiceUnitReviewRoutes.DecisionInput(original.Version, opened.GetProperty("caseVersion").GetInt64(), assignment.GetProperty("assignmentVersion").GetInt64(), "Approve", null, InvoiceUnitReviewCommands.OriginalUnitAttestation), Guid.NewGuid());
        Require(beforeDenied == await UnitReviewFootprint(), "Unauthorized self-review changed persisted facts.");
        var voteInput = new ProcurementInvoiceUnitReviewRoutes.DecisionInput(original.Version, opened.GetProperty("caseVersion").GetInt64(),
            assignment.GetProperty("assignmentVersion").GetInt64(), "Approve", "Synthetic human attests original document units", InvoiceUnitReviewCommands.OriginalUnitAttestation);
        // Current canonical identity cannot borrow a retained assignment through an alias.
        Guid originalReviewerWorker;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>(); var account = await core.LocalUsers.SingleAsync(x => x.Id == OrderManager2);
            originalReviewerWorker = account.WorkerId!.Value; account.WorkerId = mapper.Worker; await core.SaveChangesAsync();
        }
        try
        {
            var beforeAlias = await UnitReviewFootprint();
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, reviewPath + $"/assignments/{assignment.GetProperty("assignmentId").GetGuid()}/decisions", OrderManager2, voteInput, Guid.NewGuid());
            Require(beforeAlias == await UnitReviewFootprint(), "Rebound reviewer recorded a decision or source outcome.");
        }
        finally
        {
            using var services = Scope(OrderBuyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == OrderManager2)).WorkerId = originalReviewerWorker; await core.SaveChangesAsync();
        }
        var voteKey = Guid.NewGuid();
        var vote = await Send(HttpMethod.Post, reviewPath + $"/assignments/{assignment.GetProperty("assignmentId").GetGuid()}/decisions", OrderManager2, voteInput, voteKey);
        await Check(vote, HttpStatusCode.OK); var voteText = await vote.Content.ReadAsStringAsync(); var terminal = JsonSerializer.Deserialize<JsonElement>(voteText);
        Require(terminal.GetProperty("reviewState").GetString() == "Verified" && !terminal.GetProperty("canPostToFinance").GetBoolean(), "Approved original unit review acquired unrelated Finance authority.");
        var recovered = await Send(HttpMethod.Get, reviewPath + $"/commands/{voteKey}", OrderManager2); await Check(recovered, HttpStatusCode.OK);
        Require(await recovered.Content.ReadAsStringAsync() == voteText, "Actor-owned historical decision recovery changed.");
        var compared = await Send(HttpMethod.Post, invoicePath + "/matching/compare", matchingBrowserActors["matcher"].Principal,
            new ProcurementInvoiceMatchingRoutes.CompareInput(original.Version, original.Mapping, "Retained human units, unknown external exposure"), Guid.NewGuid());
        await Check(compared, HttpStatusCode.OK);
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var cases = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
            var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == original.Invoice);
            var result = await db.InvoiceMatchResults.AsNoTracking().SingleAsync(x => x.SupplierInvoiceId == original.Invoice);
            Require(result.Outcome == "Blocked" && invoice.ActiveMatchClaimResultId is null && invoice.FinancePostingRef is null
                && !await db.InvoiceMatchAllocations.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync(), "Unit review invented complete exposure or AP posting.");
            var verified = await db.InvoiceUnitReviewOutcomes.AsNoTracking().SingleAsync();
            Require(JsonDocument.Parse(result.SourceWatermarksJson!).RootElement.GetProperty("reviewSha256").GetString() == ProcurementEvidenceEncoding.PolicyJsonDigest(verified.EvidenceJson)
                && !await db.InvoiceExceptions.AnyAsync(x => x.SupplierInvoiceId == invoice.Id && x.Type == "VerifiedInvoiceUnitMappingRequired"),
                "Comparison did not actually consume the retained schema2 original-unit proof.");
            Require(await db.InvoiceUnitReviewSources.CountAsync() == 1 && await db.InvoiceUnitReviewAssignments.CountAsync() == 1
                && await db.InvoiceUnitReviewOutcomes.CountAsync() == 1 && await db.InvoiceUnitReviewReceipts.CountAsync() == 2
                && (await cases.Cases.SingleAsync(x => x.Id == coreCase)).State == "Approved", "Original review has missing/duplicate immutable or Core effects.");
            var docs = services.ServiceProvider.GetRequiredService<DocumentDbContext>();
            Require(await docs.RecordEvents.AnyAsync(x => x.Action == "DownloadAuthorized"), "Core download audit was bypassed.");
        }
        Require(matchingBrowserSourceBefore == await MatchingSourceFootprint(), "Original-unit review mutated approved/received upstream source.");
        await VerifyInvoiceUnitReviewConstraints();
        await VerifyUnitReviewInsertProvenance(mapper.Principal, mapper.Worker);
    }

    private async Task PublishUnitReviewPolicy()
    {
        var from = DateTimeOffset.UtcNow.AddSeconds(10);
        var rules = JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "InvoiceUnitReview", stages = new[] {
            new { code = "ORIGINAL_UNITS", mode = "All", principals = new[] { OrderManager2.ToString("D") } } } });
        var created = await Send(HttpMethod.Post, Root + "/configuration/policies", OrderBuyer, new { policyKey = "INVOICE_UNIT_REVIEW", effectiveFrom = from, rulesJson = rules });
        await Check(created, HttpStatusCode.Created); var row = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = row.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, $"{Root}/configuration/policies/{id}/submit-review", OrderBuyer, new { version = row.GetProperty("version").GetInt64(), reason = "Explicit named original-unit reviewer" });
        await Check(submitted, HttpStatusCode.OK); row = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/configuration/policies/{id}/publish", OrderFinance, new { version = row.GetProperty("version").GetInt64(), reason = "Independent synthetic review configuration" });
        var wait = from - DateTimeOffset.UtcNow; if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(50));
    }
    private sealed record CapturedOriginal(Guid Invoice, long Version, Guid Mapping, int MappingRevision, Guid Document);
    private async Task<CapturedOriginal> CaptureUnitOriginal(Guid maker, string number)
    {
        var source = matchingBrowserSource!.Value;
        var created = await Send(HttpMethod.Post, Root + "/invoices", maker, new { supplierId = source.Supplier, purchaseOrderId = source.Order,
            invoiceNumber = number, invoiceDate = new DateOnly(2026,10,20), currencyCode = "ZMW", lines = new[] { new { description = "Synthetic original unit review", quantity = "1", unitPrice = "100", taxAmount = "0" } } });
        await Check(created, HttpStatusCode.Created); var invoice = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = invoice.GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/invoices/{id}/documents");
        request.Headers.Add("X-Probe-User", maker.ToString("D")); request.Headers.Add("X-Shell-Entity", OrderCompany.ToString("D")); request.Headers.Add("X-Shell-Location", OrderBranch.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); request.Headers.Add("X-Document-Name", MatchingDocumentName);
        request.Content = new ByteArrayContent(MatchingDocumentBytes); request.Content.Headers.ContentType = new("application/octet-stream");
        var uploaded = await client.SendAsync(request); await Check(uploaded, HttpStatusCode.OK); var document = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        Require(document.GetProperty("canDownload").GetBoolean() && document.GetProperty("sha256").GetString() == MatchingDocumentSha, "Actual Core upload/known-byte scan did not become available.");
        invoice = await Get($"{Root}/invoices/{id}", maker); var originalLine = invoice.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var mapped = await Send(HttpMethod.Post, $"{Root}/invoices/{id}/matching/mappings", maker,
            new ProcurementInvoiceMatchingRoutes.MappingInput(invoice.GetProperty("version").GetInt64(), source.Order, source.Issued,
                [new(originalLine, source.Line, source.ReceiptLine, null, "EACH", document.GetProperty("versionId").GetGuid(), "Page 1 line 1")]), Guid.NewGuid());
        await Check(mapped, HttpStatusCode.OK); var mapping = (await mapped.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        invoice = await Get($"{Root}/invoices/{id}", maker);
        return new(id, mapping.GetProperty("version").GetInt64(), mapping.GetProperty("id").GetGuid(), mapping.GetProperty("mappingRevision").GetInt32(), document.GetProperty("versionId").GetGuid());
    }
    private async Task<string> UnitReviewFootprint()
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var cases = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        return JsonSerializer.Serialize(new { sources = await db.InvoiceUnitReviewSources.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            pins = await db.InvoiceUnitReviewAssignments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), outcomes = await db.InvoiceUnitReviewOutcomes.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            receipts = await db.InvoiceUnitReviewReceipts.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), audits = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            outbox = await db.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), cases = await cases.Cases.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            votes = await cases.Decisions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync() }, Json);
    }
}

sealed partial class Probe
{
    private async Task VerifyUnitReviewInsertProvenance(Guid maker, Guid worker)
    {
        var original = await CaptureUnitOriginal(maker, "UNIT-REVIEW-ROLLBACK-2");
        InvoiceUnitReviewTransitions.OpeningPlan plan; InvoiceIssuedOrder issued;
        using (var services = Scope(maker))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == original.Invoice);
            var lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == original.Invoice && x.IsActive).ToArrayAsync();
            var mapping = await db.InvoiceMatchMappingVersions.AsNoTracking().SingleAsync(x => x.Id == original.Mapping);
            var prior = await db.InvoiceUnitReviewSources.AsNoTracking().SingleAsync();
            issued = JsonSerializer.Deserialize<InvoiceIssuedOrder>(prior.IssuedJson)!;
            var matching = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == prior.MatchingPolicyId);
            var review = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == prior.ReviewPolicyId);
            var reviewer = await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == OrderManager2);
            // Exact known synthetic principals/worker bindings for adversarial storage-only
            // tests. Actual role/scope authorization was independently exercised above.
            var person = new InvoiceUnitReviewPolicyRules.Person("probe", OrderCompany, maker.ToString("D"), worker, true,
                new HashSet<string> { "procurement-access", InvoiceUnitReviewTransitions.RequestPermission }, false, new HashSet<Guid> { OrderBranch });
            var checker = new InvoiceUnitReviewPolicyRules.Person("probe", OrderCompany, OrderManager2.ToString("D"), reviewer.WorkerId!.Value, true,
                new HashSet<string> { "procurement-access", InvoiceRetainedUnitReview.ReviewPermission }, false, new HashSet<Guid> { OrderBranch });
            plan = InvoiceUnitReviewTransitions.PrepareOpen(invoice, lines, mapping, issued, matching, review, person, [checker],
                new Dictionary<Guid,string> { [original.Document] = MatchingDocumentSha },
                new(Guid.NewGuid(), original.Version, original.MappingRevision, DateTimeOffset.UtcNow, "Synthetic persistence rollback proof"));
        }
        var scope = new ProcurementRequestScope(); scope.Set("probe", OrderCompany, maker.ToString("D"), OrderBranch, null, true, "unit-review-provenance"); scope.AssignWorker(worker);
        var coordinator = new ProcurementApprovalTransaction(databaseConnection, scope, scope);
        void Stage(ProcurementDbContext db)
        {
            var source = InvoiceUnitReviewStorage.Source(plan, issued); db.AddScoped(source);
            foreach (var pin in plan.AssignmentWorkers) db.AddScoped(new InvoiceUnitReviewAssignmentEvidence { ReviewSourceId = source.Id, CoreCaseId = source.CoreCaseId,
                CoreAssignmentId = pin.AssignmentId, PrincipalId = pin.PrincipalId, WorkerId = pin.WorkerId });
            var request = new InvoiceUnitReviewRequest(1,"Open",plan.CommandKey,original.Invoice,null,null,original.Version,original.MappingRevision,null,null,null,plan.Reason,null);
            var response = new InvoiceUnitReviewResponse(1,original.Invoice,source.Id,source.CoreCaseId,plan.CommandKey,"Open",original.Version,1,"Pending","Pending",null,null,plan.Source.OpenedAt,true,false,false);
            response = InvoiceUnitReviewCommands.BindSource(response,source);
            var json = InvoiceUnitReviewCommands.Serialize(request);
            db.AddScoped(new InvoiceUnitReviewCommandReceipt { ReviewSourceId = source.Id, Operation = "Open", CommandKey = plan.CommandKey, ActorPrincipalId = maker.ToString("D"), ActorWorkerId = worker,
                Fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(json), RequestJson = json, ResponseJson = InvoiceUnitReviewCommands.Serialize(response), CompletedAt = plan.Source.OpenedAt });
        }
        var before = await UnitReviewFootprint();
        try
        {
            await coordinator.ExecuteAsync<int>(async (db,cases,ct) =>
            {
                cases.AddCase(plan.Case); await cases.SaveChangesAsync(ct); Stage(db); await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("UnitReviewProbeRollbackAfterBothOwnersSaved");
            });
            throw new InvalidOperationException("Unit-review rollback probe unexpectedly committed.");
        }
        catch (InvalidOperationException error) when (error.Message == "UnitReviewProbeRollbackAfterBothOwnersSaved") { }
        Require(before == await UnitReviewFootprint(), "Explicit transaction rollback retained a Core case or review row.");
        // Create a genuine pre-existing Core orphan, then UPDATE it inside the finite
        // execution transaction. A tuple's xmin would now change, but it was not inserted
        // by this UoW and must not be adopted as a newly opened source.
        using (var services = Scope(maker))
        {
            var cases = services.ServiceProvider.GetRequiredService<ApprovalDbContext>(); cases.AddCase(plan.Case); await cases.SaveChangesAsync();
        }
        before = await UnitReviewFootprint();
        try
        {
            await coordinator.ExecuteAsync(async (db,cases,ct) =>
            {
                await cases.Database.ExecuteSqlInterpolatedAsync($"UPDATE core_approval.cases SET \"UpdatedAt\"=\"UpdatedAt\" WHERE \"Id\"={plan.Case.Id} AND \"TenantId\"={scope.TenantId} AND \"LegalEntityId\"={scope.LegalEntityId}",ct);
                Stage(db); await db.SaveChangesAsync(ct); return 0;
            });
            throw new InvalidOperationException("Updated historical Core orphan was adopted.");
        }
        catch (InvalidOperationException error) when (error.Message == "InvoiceUnitReviewOpeningCaseRequired") { }
        Require(before == await UnitReviewFootprint(), "Denied historical orphan adoption changed persisted effects.");
    }
}
