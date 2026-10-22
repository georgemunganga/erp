using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private readonly Dictionary<string, Guid> unitBrowserActors = [];
    private CapturedOriginal? unitBrowserOriginal;
    private const string UnitBrowserReviewerRole = "unit_browser_reviewer";

    private async Task<string> PrepareUnitReviewBrowserManifest(int port)
    {
        Require(OrderDistinctRuntime && PrivateBrowserTransport.Enabled,
            "Original-unit browser requires finite runtime and the private Unix transport.");
        // Reuse genuine upstream commands. Suppress only the intermediate manifest write.
        _ = await PrepareMatchingBrowserManifest(port, writeManifest: false);
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var other = new WorkLocation { Code = "UNIT-BROWSER-OTHER", Name = "Other review branch", LegalEntityId = OrderCompany };
            core.WorkLocations.Add(other);
            foreach (var (name, actions, location) in new[] {
                ("maker", "procurement-invoice-read,procurement-invoice-capture,procurement-invoice-match,procurement-vendor-read,procurement-invoice-unit-review-request", OrderBranch),
                ("reviewer", "procurement-invoice-unit-review", OrderBranch),
                ("unassigned", "procurement-invoice-unit-review", OrderBranch),
                ("otherBranch", "procurement-invoice-unit-review", other.Id),
                ("revoked", "", OrderBranch) })
            {
                var key = "unit_browser_" + name.ToLowerInvariant();
                core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = key, RoleName = "Synthetic unit review " + name,
                    Active = true, PermissionsCsv = "procurement-access" + (actions.Length == 0 ? "" : "," + actions) });
                var user = new LocalUser { Email = name + "-unit-browser@example.test",
                    NormalizedEmail = (name + "-unit-browser@example.test").ToUpperInvariant(), DisplayName = name + " unit reviewer",
                    PasswordHash = "synthetic-no-login-secret", WorkerId = null, RolesCsv = key };
                core.LocalUsers.Add(user);
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = location });
                unitBrowserActors.Add(name, user.Id);
            }
            await core.SaveChangesAsync();
        }
        await PublishUnitReviewPolicy(unitBrowserActors["reviewer"]);
        var original = await CaptureUnitOriginal(unitBrowserActors["maker"], "UNIT-BROWSER-ORIGINAL-1");
        unitBrowserOriginal = original;
        var actors = new Dictionary<string, object>();
        foreach (var (name, principal) in unitBrowserActors)
        {
            // Other-branch actor context is read with its own genuine assignment.
            Guid actorBranch;
            using (var services = Scope(OrderBuyer)) actorBranch = await services.ServiceProvider.GetRequiredService<HrmDbContext>()
                .UserBranchAssignments.Where(x => x.UserId == principal && !x.IsArchived).Select(x => x.LocationId).SingleAsync();
            var response = await Send(HttpMethod.Get, Root + "/context", principal, selectedLocation: actorBranch);
            await Check(response, HttpStatusCode.OK);
            var context = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            Require(context.GetProperty("principalId").GetGuid() == principal && context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
                "Unit browser principal must be a real workerless Core account.");
            var expected = (name == "maker" ? "procurement-access,procurement-invoice-read,procurement-invoice-capture,procurement-invoice-match,procurement-vendor-read,procurement-invoice-unit-review-request"
                : name == "revoked" ? "procurement-access" : "procurement-access,procurement-invoice-unit-review").Split(',');
            Require(context.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal).SetEquals(expected),
                "Unit browser actor has unexpected stored action grants.");
            actors.Add(name, new { principalId = principal, branchId = actorBranch, context });
        }
        var meta = await Get(Root + "/meta", unitBrowserActors["maker"]);
        Require(meta.GetProperty("schemaReady").GetBoolean() && meta.GetProperty("capabilities").GetProperty("invoiceOriginalUnitReview").GetBoolean()
            && !meta.GetProperty("capabilities").GetProperty("invoiceQualifiedClaims").GetBoolean()
            && !meta.GetProperty("capabilities").GetProperty("financeHandoff").GetBoolean(),
            "Genuine P23 readiness is required; no Finance availability may be invented.");
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.InvoiceUnitReviewSources.AnyAsync() && !await db.InvoiceUnitReviewAssignments.AnyAsync()
                && !await db.InvoiceUnitReviewOutcomes.AnyAsync() && !await db.InvoiceUnitReviewReceipts.AnyAsync(),
                "Browser must open and decide the actual review; no review effects may be seeded.");
        }
        return await WriteBrowserManifest(new {
            fixtureKind = "invoice-unit-review-v1", baseUrl = $"http://127.0.0.1:{port}", tenantId = "probe",
            companyId = OrderCompany, branchId = OrderBranch, currencyCode = "ZMW", authenticationHeader = "X-Probe-User", actors,
            original = new { invoiceId = original.Invoice, invoiceVersion = original.Version,
                mappingVersionId = original.Mapping, mappingRevision = original.MappingRevision,
                documentVersionId = original.Document, documentSha256 = MatchingDocumentSha },
            noReviewEffectsSeeded = true, scannerMode = "synthetic-known-bytes", production = false,
            limits = "Synthetic outer authentication and known-byte scanner only; real scoped API/Core/PG/document use. No real ClamD, complete Finance history, AP or payment proof." });
    }

    public async Task VerifyUnitReviewBrowserCompletion()
    {
        var original = unitBrowserOriginal ?? throw new InvalidOperationException("Unit browser original missing.");
        var maker = unitBrowserActors["maker"]; var reviewer = unitBrowserActors["reviewer"];
        Guid reviewId; Guid assignmentId; long assignmentVersion; string decisionRequest; Guid decisionKey;
        using (var services = Scope(OrderBuyer))
        {
            // Fresh owning contexts, never browser reports, certify persisted effects.
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var cases = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
            var docs = services.ServiceProvider.GetRequiredService<DocumentDbContext>();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var source = await db.InvoiceUnitReviewSources.AsNoTracking().SingleAsync(); reviewId = source.Id;
            var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleAsync(x => x.Id == source.CoreCaseId);
            var pin = await db.InvoiceUnitReviewAssignments.AsNoTracking().SingleAsync();
            var outcome = await db.InvoiceUnitReviewOutcomes.AsNoTracking().SingleAsync();
            var receipts = await db.InvoiceUnitReviewReceipts.AsNoTracking().OrderBy(x => x.CompletedAt).ToArrayAsync();
            _ = InvoiceUnitReviewStorage.Restore(source, [pin], workflow);
            Require(source.SupplierInvoiceId == original.Invoice && source.MappingVersionId == original.Mapping
                && source.MakerPrincipalId == maker.ToString("D") && source.MakerWorkerId is null
                && InvoiceUnitReviewStorage.Read(source).IdentitySchemaVersion == 3
                && pin.PrincipalId == reviewer.ToString("D") && pin.WorkerId is null && pin.ReviewSourceId == reviewId
                && pin.CoreCaseId == workflow.Id && workflow.SourceId == reviewId && workflow.SourceType == "invoice_unit_review"
                && workflow.MakerSubjectId == maker.ToString("D")
                && workflow.Stages.SelectMany(x => x.Assignments).Single().Id == pin.CoreAssignmentId
                && workflow.Stages.SelectMany(x => x.Assignments).Single().AssigneeSubjectId == reviewer.ToString("D")
                && outcome.ReviewSourceId == reviewId && outcome.CoreCaseId == workflow.Id
                && outcome.EvidenceSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(outcome.EvidenceJson)
                && workflow.State == "Approved" && outcome.State == "Verified" && outcome.EvidenceSchemaVersion == 3
                && outcome.ReviewerPrincipalId == reviewer.ToString("D") && outcome.ReviewerWorkerId is null
                && receipts.Length == 2 && receipts.All(x => x.ActorWorkerId is null), "Browser did not persist exact workerless source3/case/outcome3/two receipts.");
            foreach (var receipt in receipts)
            {
                var historical = InvoiceUnitReviewCommands.ReadHistorical(receipt, source);
                Require(historical.SchemaVersion == 1 && historical.MakerPrincipalId == maker.ToString("D")
                    && historical.IdentitySchemaVersion == 3 && !historical.CanPostToFinance && !historical.IsInvoiceApproval,
                    "Browser receipt identity/envelope or financial boundary changed.");
            }
            var decided = receipts.Single(x => x.Operation == "Decision");
            var opened = receipts.Single(x => x.Operation == "Open");
            Require(opened.ActorPrincipalId == maker.ToString("D") && decided.ActorPrincipalId == reviewer.ToString("D")
                && await cases.Decisions.CountAsync(x => x.CaseId == workflow.Id) == 1,
                "Browser replay created duplicate Core decisions or crossed actors.");
            var use = await docs.UseSets.AsNoTracking().SingleAsync(x => x.SourceRecordId == reviewId);
            var documentPin = await docs.UsePins.AsNoTracking().SingleAsync(x => x.UseSetId == use.Id);
            Require(use.UseKey == reviewId && use.SourceRevision == 1 && documentPin.VersionId == original.Document
                && documentPin.Sha256 == MatchingDocumentSha && await docs.RecordEvents.AnyAsync(x => x.Action == "DownloadAuthorized"),
                "Browser bypassed exact original document retention or authorized download.");
            Require(await core.LocalUsers.Where(x => x.Id == maker || x.Id == reviewer).AllAsync(x => x.WorkerId == null),
                "Workerless browser actors acquired employee identities.");
            var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == original.Invoice);
            Require(invoice.ActiveMatchClaimResultId is null && invoice.FinancePostingRef is null
                && !await db.InvoiceMatchAllocations.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync(),
                "Unit browser invented qualified matching or AP authority.");
            var assignment = workflow.Stages.SelectMany(x => x.Assignments).Single(); assignmentId = assignment.Id;
            var request = JsonSerializer.Deserialize<InvoiceUnitReviewRequest>(decided.RequestJson, Json)!;
            assignmentVersion = request.AssignmentVersion!.Value; decisionRequest = decided.RequestJson; decisionKey = decided.CommandKey;
        }
        var path = $"{Root}/invoice-unit-reviews/{reviewId}";
        var before = await UnitReviewFootprint();
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, path, unitBrowserActors["revoked"]);
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/invoices/{original.Invoice}", reviewer);
        // Current action revocation is tested against the actual mapped handler after browser completion.
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == UnitBrowserReviewerRole)).PermissionsCsv = "procurement-access";
            await core.SaveChangesAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, Root + "/invoice-unit-reviews/tasks", reviewer);
        var input = JsonSerializer.Deserialize<InvoiceUnitReviewRequest>(decisionRequest, Json)!;
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, path + $"/assignments/{assignmentId}/decisions", reviewer,
            new ProcurementInvoiceUnitReviewRoutes.DecisionInput(input.InvoiceVersion, input.CaseVersion!.Value,
                assignmentVersion, input.Action!, input.Reason, input.Attestation), decisionKey);
        Require(before == await UnitReviewFootprint(), "Denied/revoked browser follow-up changed review/document/Core effects.");
        Require(matchingBrowserSourceBefore == await MatchingSourceFootprint(), "Unit browser changed its genuine upstream source.");
        Console.WriteLine("PASS browser original-unit review owning reload: source3/outcome3/wire1, workerless named actors, one Core decision/two receipts, exact retained documents; revocation denies, no Finance/AP authority.");
    }
}
