using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    public async Task VerifyPositiveReceiving()
    {
        Require(OrderDistinctRuntime, "Receiving proof requires separate bootstrap and finite runtime principals.");
        await Authority(OrderManager1, "RECEIVING-PR-M1"); await Authority(OrderManager2, "RECEIVING-PR-M2");
        await Authority(OrderManager2, "RECEIVING-PO-M2", "PurchaseOrder.Approve");
        Guid supplier;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = new Supplier { Number = "RECEIVING-SUPPLIER", LegalName = "Synthetic receiving supplier", DisplayName = "Synthetic receiving supplier", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(row); await db.SaveChangesAsync(); supplier = row.Id;
        }
        var goods = await DeliveredReceivingOrder(supplier, "Goods");
        var draftInput = new { purchaseOrderId = goods.OrderId, issuedVersionId = goods.IssuedId, siteId = OrderBranch,
            receivedAt = DateTimeOffset.UtcNow, deliveryNoteRef = "ACTUAL-OVER-DELIVERY", notes = "Truthfully observed two, only one authorised",
            lines = new[] { new { purchaseOrderLineId = goods.LineId, receivedQuantity = "2", rejectedQuantity = "0" } } };
        var draftKey = Guid.NewGuid(); var created = await Send(HttpMethod.Post, Root + "/receipts/drafts", OrderRequester, draftInput, draftKey);
        await Check(created, HttpStatusCode.OK); var createdText = await created.Content.ReadAsStringAsync();
        var draft = JsonDocument.Parse(createdText).RootElement.Clone(); var id = draft.GetProperty("id").GetGuid();
        var replayDraft = await Send(HttpMethod.Post, Root + "/receipts/drafts", OrderRequester, draftInput, draftKey);
        await Check(replayDraft, HttpStatusCode.OK); Require(await replayDraft.Content.ReadAsStringAsync() == createdText, "Receiving draft replay changed response.");
        var record = new { version = draft.GetProperty("version").GetInt64(), evidenceReference = "RCP:ACTUAL-DELIVERY" }; var recordKey = Guid.NewGuid();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/receipts/{id}/record", OrderRequester, record, recordKey);
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.ReceivingSourceStates.AnyAsync() && !await db.ReceivingAcceptanceEffects.AnyAsync(), "Missing policy fabricated receiving evidence.");
        }
        await PublishReceivingPolicy();
        var recordedResponse = await Send(HttpMethod.Post, $"{Root}/receipts/{id}/record", OrderRequester, record, recordKey);
        await Check(recordedResponse, HttpStatusCode.OK); var recordedText = await recordedResponse.Content.ReadAsStringAsync();
        var recorded = JsonDocument.Parse(recordedText).RootElement.Clone(); var line = recorded.GetProperty("lines")[0]; var lineId = line.GetProperty("id").GetGuid();
        Require(line.GetProperty("receivedQuantity").GetString() == "2" && line.GetProperty("acceptedQuantity").GetString() == "0", "Physical overdelivery was discarded or auto-accepted.");
        await WithoutReceivingBroadRead(async () =>
        {
            var tasks = await Get(Root + "/receiving/inspection-tasks?page=1&pageSize=1", OrderManager1);
            Require(tasks.GetProperty("total").GetInt32() == 1 && tasks.GetProperty("items")[0].GetProperty("receiptId").GetGuid() == id,
                "Assigned inspector queue missing exact scoped delivery.");
            var emptyPage = await Get(Root + "/receiving/inspection-tasks?page=2&pageSize=1", OrderManager1);
            Require(emptyPage.GetProperty("total").GetInt32() == 1 && emptyPage.GetProperty("items").GetArrayLength() == 0, "Assigned pagination changed scoped count.");
            var task = await Get($"{Root}/receipts/{id}/inspection-task", OrderManager1);
            Require(task.GetProperty("readPurpose").GetString() == "AssignedInspection" && !task.TryGetProperty("notes", out _)
                && task.GetProperty("lineReadiness")[0].GetProperty("actionCandidates")[0].GetProperty("candidate").GetBoolean(), "Purpose inspection leaked broad notes or lost current candidate.");
            var other = await Get(Root + "/receiving/inspection-tasks", OrderManager2);
            Require(other.GetProperty("total").GetInt32() == 0, "Unnamed inspector saw assigned delivery.");
            await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/receipts/{id}/inspection-task", OrderManager2, null);
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/receipts/{id}", OrderManager1, null);
        });
        var inspect = new { version = line.GetProperty("version").GetInt64(), sourceVersion = line.GetProperty("sourceVersion").GetInt64(), quantity = "2", evidenceReference = "RCP:INSPECTION" };
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/inspect", OrderRequester, inspect, Guid.NewGuid());
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/inspect", OrderManager1, inspect, Guid.NewGuid());
        var inspected = await Get($"{Root}/receipts/{id}", OrderManager2); line = inspected.GetProperty("lines")[0];
        var accept = new { version = line.GetProperty("version").GetInt64(), sourceVersion = line.GetProperty("sourceVersion").GetInt64(), quantity = "1", evidenceReference = "RCP:ACCEPTANCE" };
        var acceptKey = Guid.NewGuid(); var acceptedResponse = await Send(HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/accept", OrderManager2, accept, acceptKey);
        await Check(acceptedResponse, HttpStatusCode.OK); var acceptedText = await acceptedResponse.Content.ReadAsStringAsync();
        var accepted = JsonDocument.Parse(acceptedText).RootElement.Clone(); line = accepted.GetProperty("lines")[0];
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/accept", OrderManager2,
            new { version = line.GetProperty("version").GetInt64(), sourceVersion = line.GetProperty("sourceVersion").GetInt64(), quantity = "0.0001", evidenceReference = "RCP:OVER-CAPACITY" }, Guid.NewGuid());
        var acceptedReplay = await Send(HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/accept", OrderManager2, accept, acceptKey);
        await Check(acceptedReplay, HttpStatusCode.OK); Require(await acceptedReplay.Content.ReadAsStringAsync() == acceptedText, "Acceptance retry lost original response.");
        var recordedReplay = await Send(HttpMethod.Post, $"{Root}/receipts/{id}/record", OrderRequester, record, recordKey);
        await Check(recordedReplay, HttpStatusCode.OK); Require(await recordedReplay.Content.ReadAsStringAsync() == recordedText, "Later inspection/acceptance broke exact physical retry.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/receipts/{id}/record", OrderRequester,
            new { record.version, evidenceReference = "RCP:CHANGED-BODY" }, recordKey);
        await Expect(HttpStatusCode.Conflict, HttpMethod.Put, $"{Root}/receipts/{id}/draft", OrderRequester,
            new { version = accepted.GetProperty("version").GetInt64(), draft = draftInput }, Guid.NewGuid());
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var state = await db.ReceivingSourceStates.SingleAsync(x => x.PurchaseOrderLineId == goods.LineId);
            var effects = await db.ReceivingAcceptanceEffects.Where(x => x.ReceivingSourceStateId == state.Id).ToArrayAsync();
            var poLine = await db.PurchaseOrderLines.SingleAsync(x => x.Id == goods.LineId);
            var prLine = await db.PurchaseRequestLines.SingleAsync(x => x.Id == poLine.PurchaseRequestLineId);
            Require(state.Sequence == 3 && state.CurrentAcceptedQuantity == 1 && state.CurrentAcceptedNetAmount == 100 && effects.Length == 1
                && effects[0].GoodsReceiptLineId == lineId && effects[0].AcceptanceRecordId == lineId && effects[0].NetAmountDelta == 100
                && poLine.AcceptedQuantity == 1 && prLine.AcceptedQuantity == 1
                && await db.ReceiptInspections.CountAsync(x => x.GoodsReceiptLineId == lineId) == 1, "Receiving persisted capacity/sequence/effect counts drifted.");
        }
        var service = await DeliveredReceivingOrder(supplier, "Service");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var serviceDraftResponse = await Send(HttpMethod.Post, Root + "/service-acceptances/drafts", OrderRequester,
            new { purchaseOrderId = service.OrderId, issuedVersionId = service.IssuedId, purchaseOrderLineId = service.LineId,
                deliverableReference = "DELIVERABLE-1", periodFrom = today, periodTo = today, quantity = "1", notes = "Completed service" }, Guid.NewGuid());
        await Check(serviceDraftResponse, HttpStatusCode.OK); var certificate = await serviceDraftResponse.Content.ReadFromJsonAsync<JsonElement>();
        var serviceId = certificate.GetProperty("id").GetGuid();
        await WithoutReceivingBroadRead(async () =>
        {
            long before;
            using (var services = Scope(OrderBuyer)) before = await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().ReceivingSourceStates.LongCountAsync();
            var tasks = await Get(Root + "/receiving/service-tasks", OrderManager2);
            Require(tasks.GetProperty("total").GetInt32() == 1 && tasks.GetProperty("items")[0].GetProperty("id").GetGuid() == serviceId, "Service-only owner queue missing pending certificate.");
            var task = await Get($"{Root}/service-acceptances/{serviceId}/certification-task", OrderManager2);
            Require(task.GetProperty("capacity").GetProperty("verified").GetBoolean()
                && task.GetProperty("actionCandidates")[0].GetProperty("candidate").GetBoolean(), "Service preview lost exact current authority.");
            await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/service-acceptances/{serviceId}/certification-task", OrderManager1, null);
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/service-acceptances/{serviceId}", OrderManager2, null);
            using (var services = Scope(OrderBuyer)) Require(await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().ReceivingSourceStates.LongCountAsync() == before,
                "Read preview created a receiving source state.");
        });
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/service-acceptances/{serviceId}/certify", OrderManager2,
            new { version = certificate.GetProperty("version").GetInt64(), sourceVersion = 0, quantity = "1", evidenceReference = "RCP:SERVICE-COMPLETE" }, Guid.NewGuid());
        var finalCertificate = await Get($"{Root}/service-acceptances/{serviceId}", OrderManager2);
        Require(finalCertificate.GetProperty("acceptedQuantity").GetString() == "1" && finalCertificate.GetProperty("acceptedNetAmount").GetString() == "100", "Service exact certification mismatch.");
        var crossSite = await DeliveredReceivingOrder(supplier, "Service"); Guid otherSite;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var site = new Mightyfin.Erp.Hrm.Domain.Entities.WorkLocation { LegalEntityId = OrderCompany, Code = "RECEIVING-OTHER-SITE", Name = "Other configured receiving site" };
            core.WorkLocations.Add(site); await core.SaveChangesAsync(); otherSite = site.Id;
        }
        await PublishReceivingPolicy(otherSite);
        var wrongSiteDraft = await Send(HttpMethod.Post, Root + "/service-acceptances/drafts", OrderRequester,
            new { purchaseOrderId = crossSite.OrderId, issuedVersionId = crossSite.IssuedId, purchaseOrderLineId = crossSite.LineId,
                deliverableReference = "WRONG-SITE-DELIVERABLE", periodFrom = today, periodTo = today, quantity = "1" }, Guid.NewGuid());
        await Check(wrongSiteDraft, HttpStatusCode.OK); var wrongSiteRow = await wrongSiteDraft.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/service-acceptances/{wrongSiteRow.GetProperty("id").GetGuid()}/certify", OrderManager2,
            new { version = wrongSiteRow.GetProperty("version").GetInt64(), sourceVersion = 0, quantity = "1", evidenceReference = "RCP:WRONG-POLICY-SITE" }, Guid.NewGuid());
        await VerifyReceivingRetainedScope(crossSite.OrderId, wrongSiteRow.GetProperty("id").GetGuid(), otherSite);
        await VerifyConcurrentReceivingAcceptance(supplier);
        // Explicitly corrupt a separate linked source's historic counter as an owner-only fixture.
        // The HTTP runtime must never interpret absent new effects as a complete zero history.
        var legacy = await DeliveredReceivingOrder(supplier, "Goods");
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var currentLine = await db.PurchaseOrderLines.AsNoTracking().SingleAsync(x => x.Id == legacy.LineId);
            // A retained inactive legacy line for the same PR is deliberately outside the current sealed line set.
            db.AddScoped(new PurchaseOrderLine { PurchaseOrderId = legacy.OrderId, PurchaseRequestLineId = currentLine.PurchaseRequestLineId,
                IsActive = false, LineNumber = 2, Description = "Inherited inactive accepted line", UnitCode = "each", Quantity = 0.25m,
                UnitPrice = 100, AcceptedQuantity = 0.25m });
            await db.SaveChangesAsync();
        }
        var heldResponse = await Send(HttpMethod.Post, Root + "/receipts/drafts", OrderRequester,
            new { purchaseOrderId = legacy.OrderId, issuedVersionId = legacy.IssuedId, siteId = OrderBranch, receivedAt = DateTimeOffset.UtcNow,
                lines = new[] { new { purchaseOrderLineId = legacy.LineId, receivedQuantity = "1", rejectedQuantity = "0" } } }, Guid.NewGuid());
        await Check(heldResponse, HttpStatusCode.OK); var held = await heldResponse.Content.ReadFromJsonAsync<JsonElement>(); var heldId = held.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/receipts/{heldId}/record", OrderRequester,
            new { version = held.GetProperty("version").GetInt64(), evidenceReference = "RCP:HISTORIC-UNKNOWN-HELD" }, Guid.NewGuid());
        held = await Get($"{Root}/receipts/{heldId}", OrderManager1); var heldLine = held.GetProperty("lines")[0]; var heldLineId = heldLine.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/receipts/{heldId}/lines/{heldLineId}/inspect", OrderManager1,
            new { version = heldLine.GetProperty("version").GetInt64(), sourceVersion = heldLine.GetProperty("sourceVersion").GetInt64(), quantity = "1", evidenceReference = "RCP:INSPECT-HELD" }, Guid.NewGuid());
        held = await Get($"{Root}/receipts/{heldId}", OrderManager2); heldLine = held.GetProperty("lines")[0];
        Require(!held.GetProperty("lineReadiness")[0].GetProperty("capacity").GetProperty("verified").GetBoolean()
            && held.GetProperty("lineReadiness")[0].GetProperty("capacity").GetProperty("acceptableQuantity").ValueKind == JsonValueKind.Null,
            "Unknown historic acceptance preview pretended zero/available capacity.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/receipts/{heldId}/lines/{heldLineId}/accept", OrderManager2,
            new { version = heldLine.GetProperty("version").GetInt64(), sourceVersion = heldLine.GetProperty("sourceVersion").GetInt64(), quantity = "1", evidenceReference = "RCP:UNKNOWN-NOT-ZERO" }, Guid.NewGuid());
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager");
            role.PermissionsCsv = string.Join(',', role.PermissionsCsv.Split(',').Where(x => x != "procurement-receipt-accept")); await core.SaveChangesAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/accept", OrderManager2, accept, acceptKey);
        Console.WriteLine("PASS receiving: real goods/service lifecycle, overdelivery truth, exact evidence/replay/denied edits, missing policy and unknown historic capacity, current permission revocation");
    }
    private async Task VerifyReceivingRetainedScope(Guid orderId, Guid serviceId, Guid otherSite)
    {
        Guid legacyReceiptId, legacyServiceId;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var lineId = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == orderId && x.IsActive).Select(x => x.Id).SingleAsync();
            var receipt = new GoodsReceipt { PurchaseOrderId = orderId, Number = "LEGACY-UNKNOWN-" + Guid.NewGuid().ToString("N"),
                ReceiverSubjectId = OrderRequester.ToString("D"), ReceivedAt = DateTimeOffset.UtcNow, Status = "Draft" };
            var service = new ServiceAcceptance { PurchaseOrderLineId = lineId, OwnerSubjectId = OrderRequester.ToString("D"),
                Status = "PendingCertification", PeriodFrom = DateOnly.FromDateTime(DateTime.UtcNow), PeriodTo = DateOnly.FromDateTime(DateTime.UtcNow) };
            db.AddScoped(receipt); db.AddScoped(service); await db.SaveChangesAsync(); legacyReceiptId = receipt.Id; legacyServiceId = service.Id;
        }
        // Unrestricted, no-selected-branch readers can inspect unknown legacy records,
        // without acquiring issued/certified evidence. Merely selecting A must hide them.
        foreach (var target in new[] { (Path: $"/receipts/{legacyReceiptId}", Field: "physicalEvidenceRecorded"),
                     (Path: $"/service-acceptances/{legacyServiceId}", Field: "isCertifiedRecord") })
        {
            using var wide = await Send(HttpMethod.Get, Root + target.Path, OrderManager2, selectedBranch: false); await Check(wide, HttpStatusCode.OK);
            var value = await wide.Content.ReadFromJsonAsync<JsonElement>();
            Require(value.GetProperty("executionVersion").GetInt32() == 0 && !value.GetProperty(target.Field).GetBoolean()
                && !value.GetProperty("matchingAuthorityAvailable").GetBoolean(), "Legacy inspection manufactured final authority.");
            await Expect(HttpStatusCode.NotFound, HttpMethod.Get, Root + target.Path, OrderManager2);
        }
        foreach (var target in new[] { (View: "receipts", Id: legacyReceiptId), (View: "services", Id: legacyServiceId) })
        {
            using var wide = await Send(HttpMethod.Get, Root + "/receiving/worklist?view=" + target.View + "&pageSize=100", OrderManager2, selectedBranch: false);
            await Check(wide, HttpStatusCode.OK); var widePage = await wide.Content.ReadFromJsonAsync<JsonElement>();
            Require(widePage.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == target.Id), "Organisation-wide unverified history disappeared.");
            var selected = await Get(Root + "/receiving/worklist?view=" + target.View + "&pageSize=100", OrderManager2);
            Require(!selected.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == target.Id), "Selected A exposed unproved legacy scope.");
        }
        Guid assignmentId;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            Require(!await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderManager2 && !x.IsArchived), "Scope corruption fixture requires initially organisation-wide owner.");
            var assignment = new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = OrderManager2, LocationId = otherSite };
            core.UserBranchAssignments.Add(assignment); await core.SaveChangesAsync(); assignmentId = assignment.Id;
            // Owner-only corruption fixture: do not grant this ability to the runtime role.
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_orders SET branch_ref={otherSite} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={orderId}");
        }
        async Task<HttpResponseMessage> AtB(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Probe-User", OrderManager2.ToString("D"));
            request.Headers.Add("X-Shell-Entity", OrderCompany.ToString("D"));
            request.Headers.Add("X-Shell-Location", otherSite.ToString("D"));
            return await client.SendAsync(request);
        }
        try
        {
            foreach (var path in new[] { "/receiving/service-tasks?page=1&pageSize=1", "/receiving/worklist?view=services&page=1&pageSize=1", "/receiving/sources?page=1&pageSize=1" })
            {
                using var response = await AtB(Root + path); await Check(response, HttpStatusCode.OK);
                var page = await response.Content.ReadFromJsonAsync<JsonElement>();
                Require(page.GetProperty("total").GetInt32() == 0 && page.GetProperty("items").GetArrayLength() == 0,
                    "Current header B leaked an issued-A task/source/worklist row or count.");
            }
            foreach (var target in new[] { $"/receipts/{legacyReceiptId}", $"/service-acceptances/{legacyServiceId}" })
            {
                using var selected = await AtB(Root + target); await Check(selected, HttpStatusCode.NotFound);
                using var unselected = await Send(HttpMethod.Get, Root + target, OrderManager2, selectedBranch: false); await Check(unselected, HttpStatusCode.NotFound);
            }
            foreach (var view in new[] { "receipts", "services" })
            {
                using var response = await Send(HttpMethod.Get, Root + "/receiving/worklist?view=" + view + "&pageSize=1", OrderManager2, selectedBranch: false);
                await Check(response, HttpStatusCode.OK); var page = await response.Content.ReadFromJsonAsync<JsonElement>();
                Require(page.GetProperty("total").GetInt32() == 0 && page.GetProperty("items").GetArrayLength() == 0,
                    "B-only actor without a selected branch saw unproved A legacy rows or counts.");
            }
            using var purpose = await AtB($"{Root}/service-acceptances/{serviceId}/certification-task"); await Check(purpose, HttpStatusCode.NotFound);
            using var general = await AtB($"{Root}/service-acceptances/{serviceId}"); await Check(general, HttpStatusCode.Conflict);
        }
        finally
        {
            using var services = Scope(OrderBuyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var assignment = await core.UserBranchAssignments.SingleAsync(x => x.Id == assignmentId); assignment.IsArchived = true; await core.SaveChangesAsync();
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_orders SET branch_ref={OrderBranch} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={orderId}");
            Require(await db.GoodsReceipts.AnyAsync(x => x.Id == legacyReceiptId && x.ExecutionVersion == 0 && x.IssuedVersionId == null && x.IssuedBindingId == Guid.Empty)
                && await db.ServiceAcceptances.AnyAsync(x => x.Id == legacyServiceId && x.ExecutionVersion == 0 && x.CertifiedAt == null && x.CertificationEffectId == null),
                "Legacy scope recovery altered/backfilled or removed original records.");
        }
    }
    private async Task WithoutReceivingBroadRead(Func<Task> checks)
    {
        string original;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager"); original = role.PermissionsCsv;
            role.PermissionsCsv = string.Join(',', original.Split(',').Where(x => x is not ("procurement-receipt-view" or "procurement-po-read")));
            await core.SaveChangesAsync();
        }
        try { await checks(); }
        finally
        {
            using var services = Scope(OrderBuyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager"); role.PermissionsCsv = original;
            await core.SaveChangesAsync();
        }
    }
    private async Task VerifyConcurrentReceivingAcceptance(Guid supplier)
    {
        var source = await DeliveredReceivingOrder(supplier, "Goods");
        var receipts = new List<(Guid Receipt, Guid Line)>();
        for (var index = 0; index < 2; index++)
        {
            var draft = await Send(HttpMethod.Post, Root + "/receipts/drafts", OrderRequester,
                new { purchaseOrderId = source.OrderId, issuedVersionId = source.IssuedId, siteId = OrderBranch, receivedAt = DateTimeOffset.UtcNow,
                    lines = new[] { new { purchaseOrderLineId = source.LineId, receivedQuantity = "1", rejectedQuantity = "0" } } }, Guid.NewGuid());
            await Check(draft, HttpStatusCode.OK); var row = await draft.Content.ReadFromJsonAsync<JsonElement>(); var id = row.GetProperty("id").GetGuid();
            var recorded = await Send(HttpMethod.Post, $"{Root}/receipts/{id}/record", OrderRequester,
                new { version = row.GetProperty("version").GetInt64(), evidenceReference = "RCP:RACE-DELIVERY-" + index }, Guid.NewGuid());
            await Check(recorded, HttpStatusCode.OK); row = await recorded.Content.ReadFromJsonAsync<JsonElement>(); var line = row.GetProperty("lines")[0]; var lineId = line.GetProperty("id").GetGuid();
            await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/receipts/{id}/lines/{lineId}/inspect", OrderManager1,
                new { version = line.GetProperty("version").GetInt64(), sourceVersion = line.GetProperty("sourceVersion").GetInt64(), quantity = "1", evidenceReference = "RCP:RACE-INSPECTION-" + index }, Guid.NewGuid());
            receipts.Add((id, lineId));
        }
        var inputs = new List<object>();
        foreach (var target in receipts)
        {
            var detail = await Get($"{Root}/receipts/{target.Receipt}", OrderManager2); var line = detail.GetProperty("lines")[0];
            inputs.Add(new { version = line.GetProperty("version").GetInt64(), sourceVersion = line.GetProperty("sourceVersion").GetInt64(), quantity = "0.75", evidenceReference = "RCP:CONCURRENT-ACCEPT" });
        }
        var responses = await Task.WhenAll(receipts.Select((target, index) => Send(HttpMethod.Post,
            $"{Root}/receipts/{target.Receipt}/lines/{target.Line}/accept", OrderManager2, inputs[index], Guid.NewGuid())));
        Require(responses.Count(x => x.StatusCode == HttpStatusCode.OK) == 1 && responses.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent independent receipts did not produce exactly one accepted command and one safe conflict.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var state = await db.ReceivingSourceStates.SingleAsync(x => x.PurchaseOrderLineId == source.LineId);
        Require(state.CurrentAcceptedQuantity == 0.75m && await db.ReceivingAcceptanceEffects.CountAsync(x => x.ReceivingSourceStateId == state.Id) == 1
            && (await db.PurchaseOrderLines.SingleAsync(x => x.Id == source.LineId)).AcceptedQuantity == 0.75m,
            "Concurrent acceptance duplicated capacity or left a partial losing effect.");
    }

    private async Task PublishReceivingPolicy(Guid? serviceSite = null)
    {
        Guid inspector, owner;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            inspector = (await core.LocalUsers.Where(x => x.Id == OrderManager1).Select(x => x.WorkerId).SingleAsync())!.Value;
            owner = (await core.LocalUsers.Where(x => x.Id == OrderManager2).Select(x => x.WorkerId).SingleAsync())!.Value;
        }
        object Rule(string kind) => new { code = kind, categoryCode = "IT", purchaseType = kind, kind, siteIds = new[] { kind == "Service" ? serviceSite ?? OrderBranch : OrderBranch },
            inspectionRequired = kind == "Goods", checklistReference = kind == "Goods" ? "APPROVED-CHECKLIST" : null,
            inspectorWorkerIds = kind == "Goods" ? new[] { inspector } : [], serviceOwnerWorkerIds = kind == "Service" ? new[] { owner } : [],
            allowRequesterCertification = false, allowBuyerCertification = false, allowReceiverCertification = false, requireIndependentInspector = true,
            evidenceMode = "ControlledManualReference", evidenceReferencePrefix = "RCP:" };
        var starts = DateTimeOffset.UtcNow.AddSeconds(10);
        var created = await Send(HttpMethod.Post, Root + "/configuration/policies", OrderBuyer, new { policyKey = "RECEIVING", effectiveFrom = starts,
            rulesJson = JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "Receiving", currencyCode = "ZMW", rules = new[] { Rule("Goods"), Rule("Service") } }) });
        await Check(created, HttpStatusCode.Created); var policy = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = policy.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, $"{Root}/configuration/policies/{id}/submit-review", OrderBuyer,
            new { version = policy.GetProperty("version").GetInt64(), reason = "Synthetic receiving policy review" });
        await Check(submitted, HttpStatusCode.OK); policy = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/configuration/policies/{id}/publish", OrderBuyer,
            new { version = policy.GetProperty("version").GetInt64(), reason = "Maker must not publish own policy" });
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/configuration/policies/{id}/publish", OrderFinance,
            new { version = policy.GetProperty("version").GetInt64(), reason = "Independent synthetic receiving site, worker and checklist review" });
        var delay = starts - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero) await Task.Delay(delay + TimeSpan.FromMilliseconds(50));
    }
    private async Task<(Guid OrderId, Guid LineId, Guid IssuedId)> DeliveredReceivingOrder(Guid supplier, string kind)
    {
        var source = await NewApprovedOrderSource("Controlled receiving " + kind, purchaseType: kind);
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, OrderConversion(source, supplier), Guid.NewGuid());
        await Check(converted, HttpStatusCode.Created); var order = await converted.Content.ReadFromJsonAsync<JsonElement>(); var id = order.GetProperty("id").GetGuid();
        var funding = await Send(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            OrderFunding(order, "RECEIVING-COMMITMENT-" + id, await SourceReservation(source.GetProperty("id").GetGuid()), "RECEIVING-FUND-" + id), Guid.NewGuid());
        await Check(funding, HttpStatusCode.OK); order = await funding.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.Created, HttpMethod.Post, $"{Root}/orders/{id}/submit", OrderBuyer, new { expectedOrderVersion = order.GetProperty("version").GetInt64() }, Guid.NewGuid());
        var history = await Get($"{Root}/orders/{id}/commercial-history", OrderManager2); var workflow = history.GetProperty("cases")[0]; var task = workflow.GetProperty("stages")[0].GetProperty("assignments")[0];
        var decision = await Send(HttpMethod.Post, $"{Root}/orders/{id}/assignments/{task.GetProperty("id").GetGuid()}/decisions", OrderManager2,
            new { action = "Approve", expectedOrderVersion = history.GetProperty("order").GetProperty("version").GetInt64(), expectedCaseVersion = workflow.GetProperty("version").GetInt64(), expectedAssignmentVersion = task.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(decision, HttpStatusCode.OK); order = await decision.Content.ReadFromJsonAsync<JsonElement>();
        var issue = await Send(HttpMethod.Post, $"{Root}/orders/{id}/issue", OrderBuyer, new { expectedOrderVersion = order.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(issue, HttpStatusCode.OK); order = await issue.Content.ReadFromJsonAsync<JsonElement>();
        history = await Get($"{Root}/orders/{id}/commercial-history", OrderBuyer); var issuedId = history.GetProperty("issued")[0].GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/orders/{id}/delivery-confirmations", OrderManager1,
            new { expectedOrderVersion = order.GetProperty("version").GetInt64(), issuedVersionId = issuedId, deliveryReference = "RECEIVING-SENT-" + id,
                outcome = "Delivered", channel = "SecureEmail", evidenceReference = "SYNTHETIC-DELIVERY", observedAt = DateTimeOffset.UtcNow }, Guid.NewGuid());
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var lineId = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == id && x.IsActive).Select(x => x.Id).SingleAsync();
        return (id, lineId, issuedId);
    }
}
