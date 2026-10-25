using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

internal static partial class PrincipalApprovalHttpVerification
{
    private const string InvoiceRoot = "/api/procurement/v1/invoices";
    private const string PolicyRoot = "/api/procurement/v1/configuration/policies";

    private static async Task VerifyPrincipalInvoiceAsync(PrincipalApprovalFixture fixture, string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles, Dictionary<string, object?> result,
        Action<string> stage, PrincipalSend send, PrincipalIssuedFixture existingOrder)
    {
        Need(fixture.PrincipalInvoiceProfile && fixture.PrincipalPoProfile && fixture.PrincipalReceivingProfile,
            "P31 actual HTTP proof requires its exclusive PO and receiving cohort.");
        stage("P31ActualNormalProviderAndFiniteDocumentReadiness");
        await using var host = new PrincipalApprovalHost(fixture, owner, roles.Record, roles.Execution,
            actualSchema: true, privateManualEvidence: true);
        host.PrepareEntryPointEnvironment();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        using (var services = host.Services.CreateScope())
        {
            var ready = services.ServiceProvider.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>();
            Need(ready is ProcurementPrincipalInvoiceStoreReadiness
                && await ready.IsReadReadyAsync(default) && await ready.IsReadyAsync(default)
                && await ready.IsDownloadReadyAsync(default),
                "P31 actual owner capture, finite SQL roles and Core document download admission are required.");
        }
        var metadata = await send(client, HttpMethod.Get, "/api/procurement/v1/meta", fixture.InvoiceMaker);
        Need(metadata.GetProperty("capabilities").GetProperty("principalInvoiceMatching").GetBoolean(),
            "P31 actual normal host did not advertise genuinely ready typed matching.");
        var context = await send(client, HttpMethod.Get, "/api/procurement/v1/context", fixture.InvoiceMaker);
        Need(context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
            "P31 invoice actor must remain a genuine workerless Core account.");
        await send(client, HttpMethod.Get, "/api/procurement/v1/principal-unit-reviews/tasks",
            fixture.Ungranted, null, HttpStatusCode.Forbidden);

        stage("P31IndependentlyPublishedMatchingAndReviewPolicy");
        var zero = new { absolute = "0", percent = "0", combination = "AllLimits" };
        var matchingRules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "InvoiceMatching", currencyCode = "ZMW",
            moneyScale = 4, rounding = "ToEven",
            rules = new[] { new { categoryCode = "PRIVATE_P25", method = "ThreeWay",
                acceptanceKind = "Goods", quantity = zero, unitPrice = zero, tax = zero, total = zero } }
        });
        var reviewRules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "InvoiceUnitReview",
            stages = new[] { new { code = "ORIGINAL_UNITS", mode = "One",
                principals = new[] { fixture.InvoiceReviewer.ToString("D") } } }
        });
        await PublishP31Policy(client, send, fixture, "INVOICE_MATCHING", matchingRules);
        await PublishP31Policy(client, send, fixture, "INVOICE_UNIT_REVIEW", reviewRules);

        stage("P31RealApprovedGoodsOrderAndTwoAcceptedFacts");
        var order = await CreateActualReceivingCookieGoodsSourceAsync(fixture, client, send,
            existingOrder.SupplierId, 100m);
        const string receivingRoot = "/api/procurement/v1";
        var draft = await send(client, HttpMethod.Post, receivingRoot + "/receipts/principal-drafts",
            fixture.Receiver, new
            {
                purchaseOrderId = order.OrderId, issuedVersionId = order.IssuedId,
                siteId = fixture.Branch, receivedAt = DateTimeOffset.UtcNow,
                deliveryNoteRef = "PRIVATE-P31-PHYSICAL-DELIVERY", notes = "Actual private physical receipt",
                lines = new[] { new { id = (Guid?)null, purchaseOrderLineId = order.OrderLineId,
                    receivedQuantity = "1", rejectedQuantity = "0" } }
            }, HttpStatusCode.Created, Guid.NewGuid());
        var receiptId = draft.GetProperty("id").GetGuid();
        var receiptPath = receivingRoot + "/receipts/" + receiptId;
        var detail = await send(client, HttpMethod.Get, receiptPath + "/principal-draft", fixture.Receiver);
        var receiptLineId = detail.GetProperty("receipt").GetProperty("lines").EnumerateArray().Single()
            .GetProperty("id").GetGuid();
        var recorded = await send(client, HttpMethod.Post, receiptPath + "/principal-record", fixture.Receiver,
            new { version = draft.GetProperty("version").GetInt64(),
                evidenceReference = "PRIVATE-REC:P31-PHYSICAL-OBSERVATION" }, HttpStatusCode.OK, Guid.NewGuid());
        var inspection = await send(client, HttpMethod.Get,
            receivingRoot + "/receiving/principal-inspection-tasks?offset=0&limit=25", fixture.Inspector);
        var inspectionTask = inspection.GetProperty("records").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == receiptId);
        await send(client, HttpMethod.Post, receiptPath + "/lines/" + receiptLineId + "/principal-inspect",
            fixture.Inspector, new { version = inspectionTask.GetProperty("version").GetInt64(),
                sourceVersion = inspectionTask.GetProperty("sourceVersion").GetInt64(), quantity = "1",
                evidenceReference = "PRIVATE-REC:P31-INDEPENDENT-INSPECTION" }, HttpStatusCode.OK, Guid.NewGuid());
        foreach (var quantity in new[] { "0.25", "0.75" })
        {
            var tasks = await send(client, HttpMethod.Get,
                receiptPath + "/principal-acceptance-task?offset=0&limit=25", fixture.Acceptor);
            var task = tasks.GetProperty("records").EnumerateArray().Single();
            await send(client, HttpMethod.Post, receiptPath + "/lines/" + receiptLineId + "/principal-accept",
                fixture.Acceptor, new { version = task.GetProperty("version").GetInt64(),
                    sourceVersion = task.GetProperty("sourceVersion").GetInt64(), quantity,
                    evidenceReference = "PRIVATE-REC:P31-ACCEPT-" + quantity }, HttpStatusCode.OK, Guid.NewGuid());
        }

        stage("P31OriginalStructuredInvoiceAndActualCoreDocumentScan");
        var captured = await send(client, HttpMethod.Post, InvoiceRoot, fixture.InvoiceMaker,
            new { supplierId = order.SupplierId, purchaseOrderId = (Guid?)null,
                invoiceNumber = "PRIVATE-P31-" + Guid.NewGuid().ToString("N"),
                invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow), currencyCode = "ZMW",
                lines = new[] { new { description = "Private original goods bill", quantity = "0.5",
                    unitPrice = "100", taxAmount = "0", taxCodeRef = (string?)null,
                    costCenterRef = (string?)null } } }, HttpStatusCode.Created);
        var invoiceId = captured.GetProperty("id").GetGuid();
        var invoicePath = InvoiceRoot + "/" + invoiceId;
        var original = await send(client, HttpMethod.Get, invoicePath, fixture.InvoiceMaker);
        var invoiceLineId = original.GetProperty("lines").EnumerateArray().Single().GetProperty("id").GetGuid();
        Need(original.GetProperty("exactAmounts").GetProperty("grossAmount").GetString() == "50",
            "Original invoice gross amount was not exact 0.5 times 100.");
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nPrivate P31 original: one half unit at 100 ZMW.\n%%EOF\n");
        var document = await UploadP31Original(client, fixture, invoiceId, pdf);
        var versionId = document.GetProperty("versionId").GetGuid();
        Need(document.GetProperty("scanState").GetString() == "Available"
            && document.GetProperty("canDownload").GetBoolean()
            && document.GetProperty("sha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(pdf)),
            "Real Core scanner did not approve exactly the uploaded original bytes.");
        var sourcePage = await send(client, HttpMethod.Get,
            invoicePath + "/principal-matching/sources?page=1&pageSize=25", fixture.InvoiceMaker);
        Need(sourcePage.GetProperty("items").EnumerateArray().Any(x =>
            x.GetProperty("orderId").GetGuid() == order.OrderId
            && x.GetProperty("issuedVersionId").GetGuid() == order.IssuedId),
            "Retained issued source was not discoverable under invoice-purpose scope.");
        await using (var ownerProbe = new Npgsql.NpgsqlConnection(owner))
        {
            await ownerProbe.OpenAsync();
            var before = await PrincipalFootprintAsync(ownerProbe);
            using var wrongBranch = new HttpRequestMessage(HttpMethod.Get,
                invoicePath + "/principal-matching/sources?page=1&pageSize=25");
            wrongBranch.Headers.Add("X-Private-P25-Subject", fixture.InvoiceMaker.ToString("D"));
            wrongBranch.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
            wrongBranch.Headers.Add("X-Shell-Location", fixture.OtherReceivingBranch.ToString("D"));
            using var denied = await client.SendAsync(wrongBranch);
            Need(denied.StatusCode == HttpStatusCode.Forbidden,
                "Unassigned current Core branch exposed invoice-purpose source discovery.");
            Need(before == await PrincipalFootprintAsync(ownerProbe),
                "Wrong-branch source discovery wrote business or Core evidence.");
        }
        var accepted = await send(client, HttpMethod.Get,
            invoicePath + "/principal-matching/sources/" + order.OrderId + "/lines/"
                + order.OrderLineId + "/acceptances", fixture.InvoiceMaker);
        var facts = accepted.GetProperty("acceptedFacts").EnumerateArray().ToArray();
        Need(facts.Length == 2 && facts.Any(x => x.GetProperty("acceptedQuantity").GetString() == "0.25")
            && facts.Any(x => x.GetProperty("acceptedQuantity").GetString() == "0.75"),
            "P31 fixture requires two genuine positive, retained goods acceptance facts.");

        async Task<(JsonElement Response, Guid Key)> Map(JsonElement fact, int revision, long stateVersion)
        {
            var key = Guid.NewGuid();
            var body = new { invoiceVersion = original.GetProperty("version").GetInt64(),
                expectedMappingRevision = revision, expectedStateVersion = stateVersion,
                purchaseOrderId = order.OrderId, principalIssuedVersionId = order.IssuedId,
                lines = new[] { new { invoiceLineId, purchaseOrderLineId = order.OrderLineId,
                    principalReceivingSourceStateId = fact.GetProperty("principalReceivingSourceStateId").GetGuid(),
                    principalAcceptanceFactId = fact.GetProperty("principalAcceptanceFactId").GetGuid(),
                    acceptanceSequence = fact.GetProperty("sourceSequence").GetInt64(),
                    invoiceUnitCode = "each", documentVersionId = versionId,
                    documentLineReference = "PRIVATE-P31-ORIGINAL-LINE-1" } } };
            if (revision == 0)
            {
                await using var ownerProbe = new Npgsql.NpgsqlConnection(owner);
                await ownerProbe.OpenAsync();
                var before = await PrincipalFootprintAsync(ownerProbe);
                var missingDocument = new { body.invoiceVersion, body.expectedMappingRevision,
                    body.expectedStateVersion, body.purchaseOrderId, body.principalIssuedVersionId,
                    lines = new[] { new { body.lines[0].invoiceLineId,
                        body.lines[0].purchaseOrderLineId,
                        body.lines[0].principalReceivingSourceStateId,
                        body.lines[0].principalAcceptanceFactId,
                        body.lines[0].acceptanceSequence, body.lines[0].invoiceUnitCode,
                        documentVersionId = Guid.NewGuid(), body.lines[0].documentLineReference } } };
                await send(client, HttpMethod.Post, invoicePath + "/principal-matching/mappings",
                    fixture.InvoiceMaker, missingDocument, HttpStatusCode.Conflict, Guid.NewGuid());
                Need(before == await PrincipalFootprintAsync(ownerProbe),
                    "Missing original document wrote a mapping, receipt or audit effect.");
            }
            var response = await send(client, HttpMethod.Post, invoicePath + "/principal-matching/mappings",
                fixture.InvoiceMaker, body, HttpStatusCode.OK, key);
            await using (var ownerProbe = new Npgsql.NpgsqlConnection(owner))
            {
                await ownerProbe.OpenAsync();
                var before = await PrincipalFootprintAsync(ownerProbe);
                var changedPayload = new { body.invoiceVersion, body.expectedMappingRevision,
                    body.expectedStateVersion, body.purchaseOrderId, body.principalIssuedVersionId,
                    lines = new[] { new { body.lines[0].invoiceLineId,
                        body.lines[0].purchaseOrderLineId,
                        body.lines[0].principalReceivingSourceStateId,
                        body.lines[0].principalAcceptanceFactId,
                        body.lines[0].acceptanceSequence, body.lines[0].invoiceUnitCode,
                        body.lines[0].documentVersionId,
                        documentLineReference = "PRIVATE-P31-ALTERED-LINE" } } };
                await send(client, HttpMethod.Post, invoicePath + "/principal-matching/mappings",
                    fixture.InvoiceMaker, changedPayload, HttpStatusCode.Conflict, key);
                await send(client, HttpMethod.Post, invoicePath + "/principal-matching/mappings",
                    fixture.InvoiceMaker, body, HttpStatusCode.Conflict, Guid.NewGuid());
                Need(before == await PrincipalFootprintAsync(ownerProbe),
                    "Changed same-key payload or stale mapping CAS caused an effect.");
            }
            var recovery = await send(client, HttpMethod.Get,
                invoicePath + "/principal-matching/commands/" + key, fixture.InvoiceMaker);
            Need(response.GetRawText() == recovery.GetRawText(), "Mapping original-key GET recovery changed stored response.");
            return (response, key);
        }

        async Task<(JsonElement Open, JsonElement Decision)> Review(JsonElement map)
        {
            if (map.GetProperty("mappingRevision").GetInt32() == 1)
            {
                stage("P31MissingCoreDocumentPinWriteGrantClosesOpen");
                await using var grantOwner = new Npgsql.NpgsqlConnection(owner);
                await grantOwner.OpenAsync();
                var role = PrincipalApprovalRuntimePrivileges.Q(roles.ExecutionRole);
                async Task Grant(string verb)
                {
                    await using var command = new Npgsql.NpgsqlCommand(verb
                        + " INSERT ON core_document.use_pins "
                        + (verb == "REVOKE" ? "FROM " : "TO ") + role, grantOwner);
                    await command.ExecuteNonQueryAsync();
                }
                await Grant("REVOKE");
                try
                {
                    using var services = host.Services.CreateScope();
                    var ready = services.ServiceProvider.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>();
                    Need(!await ready.IsReadyAsync(default) && await ready.IsReadReadyAsync(default),
                        "Missing Core use-pin INSERT did not close only P31 writes.");
                    var before = await PrincipalFootprintAsync(grantOwner);
                    await send(client, HttpMethod.Post, invoicePath + "/principal-unit-reviews",
                        fixture.InvoiceMaker, new
                        {
                            invoiceVersion = original.GetProperty("version").GetInt64(),
                            mappingRevision = 1, reason = "Must be unavailable without Core use-pin write"
                        }, HttpStatusCode.ServiceUnavailable, Guid.NewGuid());
                    Need(before == await PrincipalFootprintAsync(grantOwner),
                        "Denied unpinned review opened a Core case or Procurement source.");
                }
                finally { await Grant("GRANT"); }
                using var restored = host.Services.CreateScope();
                Need(await restored.ServiceProvider.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>()
                    .IsReadyAsync(default), "P31 write readiness did not recover after exact Core use-pin grant.");
            }
            var openKey = Guid.NewGuid();
            var opened = await send(client, HttpMethod.Post, invoicePath + "/principal-unit-reviews",
                fixture.InvoiceMaker, new { invoiceVersion = original.GetProperty("version").GetInt64(),
                    mappingRevision = map.GetProperty("mappingRevision").GetInt32(),
                    reason = "Independently verify original document units" }, HttpStatusCode.OK, openKey);
            Need((await send(client, HttpMethod.Get,
                    "/api/procurement/v1/principal-unit-reviews/" + opened.GetProperty("reviewId").GetGuid()
                    + "/commands/" + openKey, fixture.InvoiceMaker)).GetRawText() == opened.GetRawText(),
                "Original review opening GET recovery changed stored response.");
            var reviewId = opened.GetProperty("reviewId").GetGuid();
            await using (var proof = new Npgsql.NpgsqlConnection(owner))
            {
                await proof.OpenAsync();
                await using var command = new Npgsql.NpgsqlCommand("""
                    SELECT count(*)=1 AND bool_and(p."VersionId"=@version)
                      AND bool_and(s."OwnerId"=@invoice)
                    FROM core_document.use_sets s
                    JOIN core_document.use_pins p ON p."UseSetId"=s."Id"
                      AND p."TenantId"=s."TenantId" AND p."LegalEntityId"=s."LegalEntityId"
                    WHERE s."TenantId"=@tenant AND s."LegalEntityId"=@company
                      AND s."SourceModule"='procurement'
                      AND s."SourceRecordType"='principal_invoice_unit_review'
                      AND s."SourceRecordId"=@review
                    """, proof);
                command.Parameters.AddWithValue("version", versionId);
                command.Parameters.AddWithValue("invoice", invoiceId);
                command.Parameters.AddWithValue("tenant", PrincipalApprovalFixture.Tenant);
                command.Parameters.AddWithValue("company", fixture.Company);
                command.Parameters.AddWithValue("review", reviewId);
                Need(await command.ExecuteScalarAsync() is true,
                    "Opening did not atomically retain an exact Core-owned authorized document use set.");
            }
            var tasks = await send(client, HttpMethod.Get,
                "/api/procurement/v1/principal-unit-reviews/tasks?page=1&pageSize=25", fixture.InvoiceReviewer);
            Need(tasks.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("reviewId").GetGuid() == reviewId),
                "Named Core reviewer did not receive a current purpose-only task.");
            var reviewPath = "/api/procurement/v1/principal-unit-reviews/" + reviewId;
            var detail = await send(client, HttpMethod.Get, reviewPath, fixture.InvoiceReviewer);
            Need(detail.GetProperty("originalDigestCurrent").GetBoolean()
                && detail.GetProperty("original").GetProperty("grossAmount").GetString() == "50",
                "Purpose-only reviewer did not see the exact retained original invoice.");
            var pin = await send(client, HttpMethod.Get, reviewPath + "/documents", fixture.InvoiceReviewer);
            Need(pin.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("versionId").GetGuid() == versionId),
                "Named reviewer could not inspect the retained original document version.");
            await DownloadP31Original(client, fixture, reviewPath + "/documents/" + versionId + "/download", pdf);
            await send(client, HttpMethod.Get, invoicePath, fixture.InvoiceReviewer,
                null, HttpStatusCode.Forbidden);
            var assignment = detail.GetProperty("assignments").EnumerateArray().Single();
            var key = Guid.NewGuid();
            var body = new { invoiceVersion = original.GetProperty("version").GetInt64(),
                caseVersion = detail.GetProperty("caseVersion").GetInt64(),
                assignmentVersion = assignment.GetProperty("assignmentVersion").GetInt64(),
                action = "Approve", reason = "Original document confirms one half unit",
                attestation = "OriginalDocumentUnitsConfirmed" };
            var decisionPath = reviewPath + "/assignments/" + assignment.GetProperty("assignmentId").GetGuid()
                + "/decisions";
            await send(client, HttpMethod.Post, decisionPath, fixture.InvoiceMaker, body,
                HttpStatusCode.Forbidden, Guid.NewGuid());
            var decision = await send(client, HttpMethod.Post, decisionPath, fixture.InvoiceReviewer,
                body, HttpStatusCode.OK, key);
            Need(decision.GetProperty("reviewState").GetString() == "Verified"
                && decision.GetProperty("isUnitVerified").GetBoolean()
                && !decision.GetProperty("canPostToFinance").GetBoolean(),
                "Named independent reviewer did not retain a verified original-unit outcome.");
            Need((await send(client, HttpMethod.Get, reviewPath + "/commands/" + key,
                    fixture.InvoiceReviewer)).GetRawText() == decision.GetRawText(),
                "Terminal review original-key GET recovery changed stored response.");
            return (opened, decision);
        }

        async Task<(JsonElement Response, Guid Key)> Compare(JsonElement map, JsonElement decision)
        {
            var status = await send(client, HttpMethod.Get, invoicePath + "/principal-matching/status",
                fixture.InvoiceMatcher);
            var body = new { invoiceVersion = original.GetProperty("version").GetInt64(),
                mappingVersionId = map.GetProperty("mappingVersionId").GetGuid(),
                reviewOutcomeId = decision.GetProperty("outcomeId").GetGuid(),
                exposureVersion = status.GetProperty("exposureVersion").GetInt64(),
                reason = "Private complete commercial comparison" };
            var key = Guid.NewGuid();
            var response = await send(client, HttpMethod.Post,
                invoicePath + "/principal-matching/comparisons", fixture.InvoiceMatcher,
                body, HttpStatusCode.OK, key);
            await using (var ownerProbe = new Npgsql.NpgsqlConnection(owner))
            {
                await ownerProbe.OpenAsync();
                var before = await PrincipalFootprintAsync(ownerProbe);
                await send(client, HttpMethod.Post, invoicePath + "/principal-matching/comparisons",
                    fixture.InvoiceMatcher, new { body.invoiceVersion, body.mappingVersionId,
                        body.reviewOutcomeId, body.exposureVersion, reason = "Changed same-key command" },
                    HttpStatusCode.Conflict, key);
                Need(before == await PrincipalFootprintAsync(ownerProbe),
                    "Changed commercial comparison command wrote an effect.");
            }
            Need((await send(client, HttpMethod.Get,
                    invoicePath + "/principal-matching/comparisons/commands/" + key,
                    fixture.InvoiceMatcher)).GetRawText() == response.GetRawText(),
                "Commercial comparison original-key GET recovery changed stored response.");
            await send(client, HttpMethod.Get,
                invoicePath + "/principal-matching/comparisons/commands/" + key,
                fixture.InvoiceMaker, null, HttpStatusCode.NotFound);
            return (response, key);
        }

        stage("P31FirstMapOriginalReviewAndBlockedComparison");
        var first = await Map(facts.Single(x => x.GetProperty("acceptedQuantity").GetString() == "0.25"), 0, 0);
        var firstReview = await Review(first.Response);
        await using var ownerConnection = new Npgsql.NpgsqlConnection(owner);
        await ownerConnection.OpenAsync();
        var beforeDenied = await PrincipalFootprintAsync(ownerConnection);
        await send(client, HttpMethod.Post, invoicePath + "/principal-matching/mappings",
            fixture.Ungranted, new { invoiceVersion = original.GetProperty("version").GetInt64() },
            HttpStatusCode.Forbidden, Guid.NewGuid());
        Need(beforeDenied == await PrincipalFootprintAsync(ownerConnection),
            "Ungrantable invoice mapping command changed business or Core evidence.");
        var blocked = await Compare(first.Response, firstReview.Decision);
        Need(blocked.Response.GetProperty("state").GetString() == "ComparisonBlocked"
            && !blocked.Response.GetProperty("commercialClaimRecorded").GetBoolean()
            && blocked.Response.GetProperty("issues").GetArrayLength() > 0,
            "Insufficient first acceptance incorrectly created a commercial claim.");
        await using (var db = fixture.Procurement())
            Need(!await db.PrincipalInvoiceClaims.AnyAsync(x => x.SupplierInvoiceId == invoiceId),
                "Blocked comparison wrote a positive claim.");

        stage("P31OwnedCorrectionRemapAndSecondIndependentReview");
        var issues = await send(client, HttpMethod.Get,
            invoicePath + "/principal-matching/exceptions?page=1&pageSize=20", fixture.InvoiceMatcher);
        var issue = issues.GetProperty("items").EnumerateArray().Single(x =>
            x.GetProperty("mappingVersionId").GetGuid() == first.Response.GetProperty("mappingVersionId").GetGuid());
        var issueKey = Guid.NewGuid();
        var corrected = await send(client, HttpMethod.Post,
            invoicePath + "/principal-matching/exceptions/" + issue.GetProperty("issueId").GetGuid() + "/actions",
            fixture.InvoiceMatcher, new { expectedVersion = issue.GetProperty("version").GetInt64(),
                action = "CorrectionRequested", reason = "Use the later accepted goods fact",
                evidenceReference = "PRIVATE-P31-CORRECTION", newOwnerPrincipalId = (string?)null },
            HttpStatusCode.OK, issueKey);
        Need(corrected.GetProperty("issueState").GetString() == "AwaitingCorrection",
            "The discrepancy did not retain its owned correction history.");
        var afterBlocked = await send(client, HttpMethod.Get, invoicePath + "/principal-matching/status",
            fixture.InvoiceMaker);
        Need(afterBlocked.GetProperty("state").GetString() == "ComparisonBlocked",
            "Blocked original mapping did not retain its state before correction.");
        var second = await Map(facts.Single(x => x.GetProperty("acceptedQuantity").GetString() == "0.75"),
            afterBlocked.GetProperty("mappingRevision").GetInt32(),
            afterBlocked.GetProperty("stateVersion").GetInt64());
        Need(second.Response.GetProperty("mappingRevision").GetInt32() == 2,
            "Corrected mapping did not retain a second version.");
        var secondReview = await Review(second.Response);
        var qualified = await Compare(second.Response, secondReview.Decision);
        Need(qualified.Response.GetProperty("state").GetString() == "Compared"
            && qualified.Response.GetProperty("commercialClaimRecorded").GetBoolean()
            && !qualified.Response.GetProperty("canPostToFinance").GetBoolean(),
            "Complete second acceptance did not produce only a typed commercial claim.");

        stage("P31FreshOwnerHistoryAndNoFinanceEffects");
        await using (var db = fixture.Procurement())
        {
            var claim = await db.PrincipalInvoiceClaims.AsNoTracking()
                .SingleAsync(x => x.SupplierInvoiceId == invoiceId);
            Need(claim.PrincipalAcceptanceFactId == facts.Single(x =>
                    x.GetProperty("acceptedQuantity").GetString() == "0.75")
                    .GetProperty("principalAcceptanceFactId").GetGuid()
                && claim.Quantity == 0.5m && claim.NetAmount == 50m,
                "Fresh owner reload lost exact corrected acceptance, quantity or amount.");
            Need(await db.PrincipalInvoiceMappingVersions.CountAsync(x => x.SupplierInvoiceId == invoiceId) == 2
                && await db.PrincipalInvoiceUnitReviewSources.CountAsync(x => x.SupplierInvoiceId == invoiceId) == 2
                && await db.PrincipalInvoiceDiscrepancies.AnyAsync(x => x.SupplierInvoiceId == invoiceId)
                && !await db.FinanceInvoiceHandoffs.AnyAsync(x => x.SupplierInvoiceId == invoiceId)
                && !await db.FinancePaymentStatusRefs.AnyAsync(),
                "Typed correction history or no-Finance ownership boundary was not retained.");
        }
        result["p31InvoiceId"] = invoiceId;
        result["p31FirstBlockedThenExactClaimVerified"] = true;
        result["p31CoreDocumentRealScannerVerified"] = true;
        result["p31FinancePostingVerified"] = false;
        result["status"] = "PASS_PRIVATE_P31_TYPED_INVOICE_HTTP_ONLY_NO_FINANCE";
    }

    private static async Task PublishP31Policy(HttpClient client, PrincipalSend send,
        PrincipalApprovalFixture fixture, string key, string rules)
    {
        var from = DateTimeOffset.UtcNow.AddSeconds(30);
        var created = await send(client, HttpMethod.Post, PolicyRoot, fixture.ConfigMaker,
            new { policyKey = key, effectiveFrom = from, effectiveTo = (DateTimeOffset?)null,
                rulesJson = rules }, HttpStatusCode.Created);
        var path = PolicyRoot + "/" + created.GetProperty("id").GetGuid();
        var submitted = await send(client, HttpMethod.Post, path + "/submit-review", fixture.ConfigMaker,
            new { version = created.GetProperty("version").GetInt64(), reason = "Private P31 rule review" });
        var published = await send(client, HttpMethod.Post, path + "/publish", fixture.ConfigReviewer,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Independent private P31 policy review" });
        Need(published.GetProperty("state").GetString() == "Published",
            "P31 policy was not independently published through actual routes.");
        var wait = from - DateTimeOffset.UtcNow;
        Need(wait <= TimeSpan.FromSeconds(30), "Unexpected P31 policy activation wait.");
        if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(50));
    }

    private static async Task<JsonElement> UploadP31Original(HttpClient client,
        PrincipalApprovalFixture fixture, Guid invoiceId, byte[] bytes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            InvoiceRoot + "/" + invoiceId + "/documents");
        request.Headers.Add("X-Private-P25-Subject", fixture.InvoiceMaker.ToString("D"));
        request.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
        request.Headers.Add("X-Shell-Location", fixture.Branch.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        request.Headers.Add("X-Document-Name", Uri.EscapeDataString("private-p31-original.pdf"));
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(request);
        Need(response.StatusCode == HttpStatusCode.OK,
            "Actual Core document upload and scanner did not complete successfully.");
        using var parsed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return parsed.RootElement.Clone();
    }

    private static async Task DownloadP31Original(HttpClient client,
        PrincipalApprovalFixture fixture, string path, byte[] expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Private-P25-Subject", fixture.InvoiceReviewer.ToString("D"));
        request.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
        request.Headers.Add("X-Shell-Location", fixture.Branch.ToString("D"));
        using var response = await client.SendAsync(request);
        Need(response.StatusCode == HttpStatusCode.OK
            && response.Headers.CacheControl?.NoStore == true
            && (await response.Content.ReadAsByteArrayAsync()).SequenceEqual(expected),
            "Purpose-only reviewer could not download exact pinned, scanned Core document bytes.");
    }
}
