using System.Net;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

internal static partial class PrincipalApprovalHttpVerification
{
    /// <summary>Private, opt-in P30 source proof. The dispatcher owns the exclusive
    /// proc_p30_http_rfq_* guard, normal Program host configuration and request count.
    /// No publication, vendor approval, Core vote or quotation outcome is seeded.</summary>
    private static async Task VerifyP30RfqPublicationAsync(PrincipalApprovalFixture fixture,
        string owner, PrincipalApprovalRuntimePrivileges.Connections roles,
        Dictionary<string, object?> result, Action<string> stage, PrincipalSend send,
        Action countRequest, string rules)
    {
        Need(fixture.PrincipalRfqProfile && result.TryGetValue("positiveApprovalGraph", out var approved)
            && approved is true && result["positiveApprovalRequestId"] is Guid,
            "P30 RFQ requires a genuine P25 competitive approval graph.");
        var requestId = (Guid)result["positiveApprovalRequestId"]!;
        stage("P30RfqSyntheticCoreMasterSetup");
        await using (var core = fixture.Core())
        {
            Need(!await core.UserBranchAssignments.AnyAsync(x => x.UserId == fixture.ConfigMaker
                    || x.UserId == fixture.ConfigReviewer)
                && await core.UserBranchAssignments.CountAsync(x => x.UserId == fixture.Buyer
                    && x.LocationId == fixture.Branch) == 1
                && !await core.Workers.AnyAsync(),
                "Private SOURCING policy actors must be Core companywide, while the workerless buyer remains branch-confined.");
            // Synthetic Core roles are fixture master data. They do not seed a
            // policy, supplier decision, RFQ approval, contact or bid outcome.
            foreach (var (role, actions) in new[]
            {
                ("p29_rfq_buyer", "procurement-sourcing-submit"),
                ("p25_config_maker", "procurement-vendor-create,procurement-vendor-edit"),
                ("p25_config_reviewer", "procurement-vendor-approve,procurement-sourcing-publish"),
            })
            {
                var current = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == role);
                current.PermissionsCsv += "," + actions;
            }
            await core.SaveChangesAsync();
            Need(!await core.Workers.AnyAsync(), "P30 RFQ must remain workerless.");
        }

        stage("P30RfqActualReadiness");
        await using var host = new PrincipalApprovalHost(fixture, owner, roles.Record, roles.Execution,
            actualSchema: true, privateManualEvidence: true);
        host.PrepareEntryPointEnvironment();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        using (var registration = host.Services.CreateScope())
        {
            var ready = registration.ServiceProvider.GetRequiredService<IProcurementPrincipalSourcingPublicationReadiness>();
            Need(ready is not UnavailablePrincipalSourcingPublicationReadiness,
                "P30 publication needs the actual owner-schema readiness provider.");
            await using var executionStore = fixture.Procurement(roles.Execution);
            Need(await ready.IsReadyAsync(executionStore, default),
                "Actual P30 publication schema and finite role are not ready.");
            result["principalRfqPublicationPhysicalReadiness"] = true;
        }
        var meta = await send(client, HttpMethod.Get, "/api/procurement/v1/meta", fixture.Buyer);
        result["principalRfqPublicationMetaCapabilities"] = meta.GetProperty("capabilities").Clone();
        Need(meta.GetProperty("capabilities").GetProperty("principalSourcingPublication").GetBoolean(),
            "P30 publication was not admitted by normal metadata.");
        const string sourcing = "/api/procurement/v1/sourcing";
        var selectable = await send(client, HttpMethod.Get,
            sourcing + "/approved-principal-sources?page=1&pageSize=25", fixture.Buyer);
        var source = selectable.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("purchaseRequestId").GetGuid() == requestId);
        var lineId = source.GetProperty("purchaseRequestLineId").GetGuid();
        var draftBody = new { title = "Private P30 RFQ publication proof", sources = new[] {
            new { purchaseRequestId = requestId, purchaseRequestLineId = lineId,
                expectedRequestVersion = source.GetProperty("expectedRequestVersion").GetInt64(),
                quantity = "1.0000" } } };
        var draft = await send(client, HttpMethod.Post,
            sourcing + "/from-approved-principal-sources", fixture.Buyer, draftBody,
            HttpStatusCode.Created, Guid.NewGuid());
        var eventId = draft.GetProperty("eventId").GetGuid();
        var eventPath = sourcing + "/" + eventId;
        var original = await send(client, HttpMethod.Get, eventPath + "/principal-source-draft", fixture.Buyer);
        Need(original.GetProperty("status").GetString() == "Draft"
            && original.GetProperty("lines").EnumerateArray().Single()
                .GetProperty("allocationState").GetString() == "Reserved",
            "P30 RFQ needs a retained, genuinely approved demand hold.");

        stage("P30RfqSupplierApiLifecycle");
        var suppliers = new List<Guid>();
        for (var ordinal = 1; ordinal <= 2; ordinal++)
        {
            var vendor = await send(client, HttpMethod.Post, "/api/procurement/v1/vendors",
                fixture.ConfigMaker, new { legalName = $"Private RFQ Supplier {ordinal}",
                    displayName = $"Private RFQ Supplier {ordinal}", countryCode = "ZM",
                    categoryCode = "PRIVATE_P25", currencyCode = "ZMW",
                    primaryContact = new { name = $"Private Contact {ordinal}", isPrimary = true } },
                HttpStatusCode.Created);
            var id = vendor.GetProperty("id").GetGuid();
            var submitted = await send(client, HttpMethod.Post,
                $"/api/procurement/v1/vendors/{id}/submit", fixture.ConfigMaker,
                new { version = vendor.GetProperty("version").GetInt64(),
                    businessReason = "Synthetic private competitive-source supplier" });
            var active = await send(client, HttpMethod.Post,
                $"/api/procurement/v1/vendors/{id}/approve", fixture.ConfigReviewer,
                new { version = submitted.GetProperty("version").GetInt64(),
                    reason = "Independent synthetic supplier review", duplicatesReviewed = true });
            Need(active.GetProperty("status").GetString() == "Active"
                && active.GetProperty("isOrderable").GetBoolean(),
                "Supplier must become orderable through its real API.");
            suppliers.Add(id);
        }

        stage("P30RfqSourcingPolicyApiLifecycle");
        const string policyRoot = "/api/procurement/v1/configuration/policies";
        var effectiveFrom = DateTimeOffset.UtcNow.AddSeconds(30);
        var policy = await send(client, HttpMethod.Post, policyRoot, fixture.ConfigMaker,
            new { policyKey = "SOURCING", effectiveFrom, effectiveTo = (DateTimeOffset?)null,
                rulesJson = rules }, HttpStatusCode.Created);
        var policyId = policy.GetProperty("id").GetGuid();
        var policyPath = policyRoot + "/" + policyId;
        var review = await send(client, HttpMethod.Post, policyPath + "/submit-review", fixture.ConfigMaker,
            new { version = policy.GetProperty("version").GetInt64(),
                reason = "Private named Core sourcing publication review" });
        var published = await send(client, HttpMethod.Post, policyPath + "/publish", fixture.ConfigReviewer,
            new { version = review.GetProperty("version").GetInt64(),
                reason = "Independent private sourcing policy publication" });
        Need(published.GetProperty("state").GetString() == "Published",
            "SOURCING schema2 must publish through its real identity lifecycle.");
        var wait = effectiveFrom - DateTimeOffset.UtcNow;
        Need(wait <= TimeSpan.FromSeconds(30), "Unexpected private sourcing policy activation wait.");
        if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(50));

        stage("P30RfqPublicationOpenAndIndependentReview");
        var openKey = Guid.NewGuid();
        var openBody = new { expectedEventVersion = original.GetProperty("version").GetInt64(),
            originalDraftFingerprint = original.GetProperty("originalDraftFingerprint").GetString(),
            supplierIds = suppliers.ToArray() };
        // This private probe captures only bounded internal invariant codes for
        // this one request. The public API intentionally keeps its generic 409.
        var openFailureCodes = new List<string>();
        var openFailureSites = new List<object>();
        EventHandler<FirstChanceExceptionEventArgs> onOpenFailure = (_, args) =>
        {
            var error = args.Exception;
            var type = error.GetType().Name;
            if (type is not ("InvalidOperationException" or "ArgumentException" or
                "DbUpdateException" or "DbUpdateConcurrencyException" or
                "InvalidCastException" or "OverflowException" or "PostgresException")) return;
            var code = error.Message;
            var internalCode = code.Length is >= 4 and <= 96 && Regex.IsMatch(code,
                @"\A(?:Rfq|Sourcing|SharedCoreApproval|Approval)[A-Za-z0-9]{1,90}\z");
            var category = type == "DbUpdateConcurrencyException" ? "Concurrency"
                : type == "OverflowException" ? "Overflow"
                : error is PostgresException pg && pg.SqlState.StartsWith("23", StringComparison.Ordinal)
                    ? "DatabaseConstraint"
                : type == "PostgresException" ? "DatabaseError"
                : code.Contains("Collection was modified", StringComparison.OrdinalIgnoreCase)
                ? "CollectionChanged"
                : code.Contains("Sequence contains more than one", StringComparison.OrdinalIgnoreCase)
                    ? "SequenceMultiple"
                : code.Contains("Sequence contains no elements", StringComparison.OrdinalIgnoreCase)
                    ? "SequenceEmpty"
                : code.Contains("cannot be tracked", StringComparison.OrdinalIgnoreCase)
                    ? "EfTracking"
                : code.Contains("could not be translated", StringComparison.OrdinalIgnoreCase)
                    ? "EfTranslation"
                : internalCode ? "InternalCode" : "Other";
            var declaring = error.TargetSite?.DeclaringType?.FullName;
            var method = error.TargetSite?.Name;
            var safeSite = declaring is { Length: <= 180 } &&
                Regex.IsMatch(declaring, @"\A(?:System|Microsoft\.EntityFrameworkCore|Mightyfin\.Erp|Npgsql)[A-Za-z0-9_.+\x60]*\z")
                && method is { Length: <= 100 } &&
                Regex.IsMatch(method, @"\A[A-Za-z_][A-Za-z0-9_]*\z");
            var sqlState = error is PostgresException postgres &&
                Regex.IsMatch(postgres.SqlState, @"\A[A-Z0-9]{5}\z")
                ? postgres.SqlState : null;
            lock (openFailureCodes)
            {
                if (internalCode && openFailureCodes.Count < 8 &&
                    !openFailureCodes.Any(x => string.Equals(x, code, StringComparison.Ordinal)))
                    openFailureCodes.Add(code);
                var site = new { type, category,
                    declaringType = safeSite ? declaring : "Unclassified",
                    method = safeSite ? method : "Unclassified", sqlState };
                if (category != "Other" && openFailureSites.Count == 8)
                {
                    var ordinary = openFailureSites.FindIndex(x =>
                        JsonSerializer.Serialize(x).Contains("\"category\":\"Other\"", StringComparison.Ordinal));
                    if (ordinary >= 0) openFailureSites.RemoveAt(ordinary);
                }
                if (openFailureSites.Count < (category == "Other" ? 4 : 8)
                    && !openFailureSites.Any(x =>
                        string.Equals(JsonSerializer.Serialize(x), JsonSerializer.Serialize(site), StringComparison.Ordinal)))
                    openFailureSites.Add(site);
            }
        };
        JsonElement opened;
        AppDomain.CurrentDomain.FirstChanceException += onOpenFailure;
        try
        {
            opened = await send(client, HttpMethod.Post, eventPath + "/principal-publication-reviews",
                fixture.Buyer, openBody, HttpStatusCode.Created, openKey);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= onOpenFailure;
            lock (openFailureCodes)
            {
                result["principalPublicationOpenInternalCodes"] = openFailureCodes.ToArray();
                result["principalPublicationOpenFailureSites"] = openFailureSites.ToArray();
            }
        }
        Need(opened.GetProperty("status").GetString() == "PublicationReview"
            && opened.GetProperty("supplierDeliveryState").GetString() == "NotStarted",
            "Opening must retain an internal review, not supplier delivery.");
        var openReplay = await send(client, HttpMethod.Post, eventPath + "/principal-publication-reviews",
            fixture.Buyer, openBody, HttpStatusCode.Created, openKey);
        Need(openReplay.GetRawText() == opened.GetRawText(), "Publication Open replay changed its result.");
        var openRecovered = await send(client, HttpMethod.Get,
            sourcing + "/principal-publication-commands/" + openKey, fixture.Buyer,
            null, HttpStatusCode.Created);
        Need(openRecovered.GetRawText() == opened.GetRawText(),
            "Actor-owned publication Open recovery changed historical bytes.");
        var assigned = await send(client, HttpMethod.Get,
            sourcing + "/principal-publication-reviews/assigned?page=1&pageSize=25", fixture.ConfigReviewer);
        var task = assigned.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("eventId").GetGuid() == eventId);
        var assignment = task.GetProperty("assignments").EnumerateArray().Single();
        var caseId = task.GetProperty("caseId").GetGuid();
        var decisionKey = Guid.NewGuid();
        var decisionPath = eventPath + $"/principal-publication-reviews/{caseId}/assignments/"
            + assignment.GetProperty("id").GetGuid() + "/decisions";
        var decisionBody = new { expectedEventVersion = task.GetProperty("eventVersion").GetInt64(),
            expectedCaseVersion = task.GetProperty("caseVersion").GetInt64(),
            expectedAssignmentVersion = assignment.GetProperty("version").GetInt64(),
            action = "Approve", reason = "Independent internal publication only" };
        // Reuse the same bounded classifier for exactly this one positive Decide
        // request. Open's captured arrays above are immutable copies in result.
        JsonElement decided;
        lock (openFailureCodes)
        {
            openFailureCodes.Clear();
            openFailureSites.Clear();
        }
        AppDomain.CurrentDomain.FirstChanceException += onOpenFailure;
        try
        {
            decided = await send(client, HttpMethod.Post, decisionPath, fixture.ConfigReviewer,
                decisionBody, HttpStatusCode.OK, decisionKey);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= onOpenFailure;
            lock (openFailureCodes)
            {
                result["principalPublicationDecisionInternalCodes"] = openFailureCodes.ToArray();
                result["principalPublicationDecisionFailureSites"] = openFailureSites.ToArray();
            }
        }
        Need(decided.GetProperty("status").GetString() == "Published"
            && decided.GetProperty("supplierDeliveryState").GetString() == "ReadyForManualDelivery"
            && !decided.GetProperty("financeReservationChanged").GetBoolean(),
            "Core approval must authorize internal publication only.");
        var decisionRecovered = await send(client, HttpMethod.Get,
            sourcing + "/principal-publication-commands/" + decisionKey, fixture.ConfigReviewer);
        Need(decisionRecovered.GetRawText() == decided.GetRawText(),
            "Independent reviewer recovery changed historical decision bytes.");
        var retained = await send(client, HttpMethod.Get, eventPath + "/principal-publication", fixture.Buyer);
        var invitations = retained.GetProperty("invitations").EnumerateArray().ToArray();
        Need(invitations.Length == 2 && invitations.All(x => x.GetProperty("status").GetString() == "ReadyForManualDelivery"),
            "Shortlist must remain manually deliverable after approval.");

        stage("P30RfqManualContactAndQuoteDraft");
        var invitation = invitations[0];
        var invitationId = invitation.GetProperty("id").GetGuid();
        var contactKey = Guid.NewGuid();
        var contact = await send(client, HttpMethod.Post,
            eventPath + $"/principal-invitations/{invitationId}/manual-contact", fixture.Buyer,
            new { channel = "EmailOutsideSystem", reference = "PRIVATE-MANUAL-CONTACT",
                reportedContactAt = DateTimeOffset.UtcNow }, HttpStatusCode.Created, contactKey);
        Need(contact.GetProperty("status").GetString() == "ManualContactRecorded"
            && !contact.GetProperty("emailSentBySystem").GetBoolean(),
            "Manual contact must not claim system email delivery.");
        var contactRecovered = await send(client, HttpMethod.Get,
            sourcing + "/principal-manual-contact-commands/" + contactKey, fixture.Buyer,
            null, HttpStatusCode.Created);
        Need(contactRecovered.GetRawText() == contact.GetRawText(),
            "Actor-owned manual contact recovery changed historical bytes.");
        var publishedLine = retained.GetProperty("lines").EnumerateArray().Single();
        var quoteLines = new[] { new { sourcingLineId = publishedLine.GetProperty("id").GetGuid(),
            quantity = "1.0000", unitPrice = "4.9999", taxAmount = "0.0000",
            deliveryAmount = "0.0000", leadTimeText = "Private manual estimate", notes = "Not supplier-submitted" } };
        var quoteBody = new { invitationId, lines = quoteLines };
        var quoteKey = Guid.NewGuid();
        var quote = await send(client, HttpMethod.Post, eventPath + "/principal-manual-quotes",
            fixture.Buyer, quoteBody, HttpStatusCode.Created, quoteKey);
        var bidId = quote.GetProperty("bidId").GetGuid();
        Need(quote.GetProperty("status").GetString() == "Draft"
            && !quote.GetProperty("supplierSubmitted").GetBoolean(),
            "Manual quotation must remain an internal Draft.");
        var quoteReplay = await send(client, HttpMethod.Post, eventPath + "/principal-manual-quotes",
            fixture.Buyer, quoteBody, HttpStatusCode.Created, quoteKey);
        Need(quoteReplay.GetRawText() == quote.GetRawText(), "Quote Create replay changed its result.");
        var revisedKey = Guid.NewGuid();
        var revised = await QuotePutAsync(client, eventPath + $"/principal-manual-quotes/{bidId}",
            fixture, new { expectedBidVersion = quote.GetProperty("bidVersion").GetInt64(),
                lines = new[] { new { sourcingLineId = quoteLines[0].sourcingLineId,
                    quantity = "1.0000", unitPrice = "4.5000", taxAmount = "0.0000",
                    deliveryAmount = "0.0000", leadTimeText = "Revised manual estimate", notes = "Draft only" } } },
            revisedKey, countRequest);
        Need(revised.GetProperty("status").GetString() == "Draft"
            && revised.GetProperty("draftRevision").GetInt32() == 2,
            "Manual quote revision must retain a second Draft version.");
        var cancelKey = Guid.NewGuid();
        var cancelled = await send(client, HttpMethod.Post,
            eventPath + $"/principal-manual-quotes/{bidId}/cancel", fixture.Buyer,
            new { expectedBidVersion = revised.GetProperty("bidVersion").GetInt64() },
            HttpStatusCode.OK, cancelKey);
        Need(cancelled.GetProperty("status").GetString() == "Cancelled"
            && !cancelled.GetProperty("supplierSubmitted").GetBoolean(),
            "Cancelled manual Draft cannot become a supplier bid submission.");
        var quoteRecovered = await send(client, HttpMethod.Get,
            sourcing + "/principal-manual-quote-commands/" + quoteKey, fixture.Buyer,
            null, HttpStatusCode.Created);
        Need(quoteRecovered.GetRawText() == quote.GetRawText(),
            "Actor-owned quote receipt recovery changed historical Create bytes.");

        stage("P30RfqScopedNegativesAndOwnerReload");
        var foreignBranch = Guid.NewGuid();
        await using (var core = fixture.Core())
        {
            core.WorkLocations.Add(new Mightyfin.Erp.Hrm.Domain.Entities.WorkLocation
            {
                Id = foreignBranch, Code = "P30_OTHER", Name = "Private unassigned P30 branch",
                LegalEntityId = fixture.Company,
            });
            await core.SaveChangesAsync();
        }
        await using var ownerConnection = new Npgsql.NpgsqlConnection(owner);
        await ownerConnection.OpenAsync();
        var footprint = await PrincipalFootprintAsync(ownerConnection, rowsOnly: true);
        await send(client, HttpMethod.Get, eventPath + "/principal-publication", fixture.ConfigMaker,
            null, HttpStatusCode.Forbidden);
        await send(client, HttpMethod.Get, eventPath + "/principal-publication", fixture.Ungranted,
            null, HttpStatusCode.Forbidden);
        await send(client, HttpMethod.Get,
            sourcing + "/principal-manual-quote-commands/" + quoteKey, fixture.ConfigMaker,
            null, HttpStatusCode.Forbidden);
        using (var offBranch = new HttpRequestMessage(HttpMethod.Get,
            eventPath + "/principal-publication"))
        {
            offBranch.Headers.Add("X-Private-P25-Subject", fixture.Buyer.ToString("D"));
            offBranch.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
            offBranch.Headers.Add("X-Shell-Location", foreignBranch.ToString("D"));
            countRequest();
            using var denied = await client.SendAsync(offBranch);
            Need(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                "An unassigned Core branch must not reveal the RFQ publication.");
        }
        Need(await PrincipalFootprintAsync(ownerConnection, rowsOnly: true) == footprint,
            "Denied RFQ reads changed persisted private records.");
        await using var db = fixture.Procurement();
        await using var cases = fixture.Approvals();
        await using var coreReload = fixture.Core();
        var ev = await db.SourcingEvents.SingleAsync(x => x.Id == eventId);
        var policyRow = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
        var policyPins = await db.SourcingPolicyIdentityEvidenceRecords
            .Where(x => x.PolicyVersionId == policyId).ToArrayAsync();
        var opening = await db.SourcingPublicationOpenings.SingleAsync(x => x.SourcingEventId == eventId);
        var workflow = await cases.Cases.SingleAsync(x => x.Id == caseId);
        var inviteRows = await db.SourcingInvitations.Where(x => x.SourcingEventId == eventId).ToArrayAsync();
        var contacts = await db.SourcingManualContacts.Where(x => x.SourcingEventId == eventId).ToArrayAsync();
        var bid = await db.SupplierBids.SingleAsync(x => x.Id == bidId);
        var quoteVersions = await db.SourcingManualQuoteVersions
            .Where(x => x.SupplierBidId == bidId).OrderBy(x => x.Revision).ToArrayAsync();
        Need(!await coreReload.Workers.AnyAsync() && ev.Status == "Published"
            && policyRow.State == "Published" && policyPins.Length == 3
            && policyPins.All(x => x.SchemaVersion == 2)
            && opening.CoreApprovalCaseId == caseId && workflow.State == "Approved"
            && inviteRows.Length == 2 && inviteRows.Count(x => x.Status == "ManualContactRecorded") == 1
            && contacts.Length == 1 && contacts[0].ActorPrincipalId == fixture.Buyer.ToString("D")
            && bid.Status == "Cancelled" && bid.SourceChannel == "Manual"
            && bid.SubmittedAt is null && quoteVersions.Length == 3
            && quoteVersions.Select(x => x.Operation).SequenceEqual(new[] { "Create", "Revise", "Cancel" })
            && !await db.SourcingAwards.AnyAsync() && !await db.PurchaseOrders.AnyAsync()
            && !await db.FinanceInvoiceHandoffs.AnyAsync() && !await db.FinancePaymentStatusRefs.AnyAsync(),
            "Independent owner reload found missing RFQ provenance or invented downstream authority.");
        result["p30RfqPublication"] = new { status = "PASS_PRIVATE_P30_RFQ_INTERNAL_PUBLICATION_ONLY",
            requestId, lineId, eventId, policyId, caseId, bidId, openKey, decisionKey,
            contactKey, quoteKey, revisedKey, cancelKey, suppliers = suppliers.ToArray(),
            coreWorkerless = true, supplierEmailSentBySystem = false, supplierBidSubmitted = false,
            awardCreated = false, orderCreated = false, financePostingCreated = false };
        result["status"] = "PASS_PRIVATE_P30_RFQ_INTERNAL_PUBLICATION_ONLY";
    }

    private static async Task<JsonElement> QuotePutAsync(HttpClient client, string path,
        PrincipalApprovalFixture fixture, object body, Guid key, Action countRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        request.Headers.Add("X-Private-P25-Subject", fixture.Buyer.ToString("D"));
        request.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
        request.Headers.Add("X-Shell-Location", fixture.Branch.ToString("D"));
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        request.Content = JsonContent.Create(body);
        countRequest();
        using var response = await client.SendAsync(request);
        Need(response.StatusCode == HttpStatusCode.OK, "Actual manual quotation PUT failed.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    // PRIVATE FIXTURE: production SOURCING coverage requires one unrestricted
    // fallback. A branch-specific rule alone is incomplete configuration.
    internal static string BuildP30RfqSourcingRules(Guid reviewer) => JsonSerializer.Serialize(new
        {
            schemaVersion = 2, policyKey = "SOURCING", transactionType = "SourcingPublication",
            action = "SourcingEvent.Publish", permission = "procurement-sourcing-publish",
            currencyCode = "ZMW", amountBasis = "ApprovedLineBase",
            rules = new[] { new { code = "PRIVATE_RFQ", priority = 0,
                when = new { minimumAmountInclusive = (string?)null,
                    maximumAmountExclusive = (string?)null, categoryCodes = Array.Empty<string>(),
                    purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>() },
                result = new { publication = new { minimumQuoteCount = 2,
                    minimumInvitedSuppliers = 2, requirementsPackRequired = false,
                    evaluationCriteriaRequired = false, closingDateRequired = false },
                    approvalStages = new[] { new { code = "PRIVATE_REVIEW", assignee = "ProcurementManager",
                        mode = "One", principalIds = new[] { reviewer.ToString("D") } } },
                    audience = new { supplierLifecycle = "RecordedActiveDecision", categoryMode = "Ignore",
                        qualificationTypes = Array.Empty<string>(), qualificationDocuments = "NotRequired",
                        validThrough = "PreparationInstant", allowNoQualificationExpiry = false,
                        allowOpenEndedSupplierValidity = false, contactSelection = "OneActiveSelected",
                        contactRequiredFields = Array.Empty<string>() } } } }
        });

    internal static void PreflightP30RfqSourcingRules(string rules)
    {
        Need(SourcingPublicationPolicyRules.Validate(rules).Count == 0,
            "PrivateSourcingPolicyValidationFailed");
        Need(SourcingPublicationPolicyRules.AnalyzeCoverage(rules).Count == 0,
            "PrivateSourcingPolicyCoverageFailed");
    }
}
