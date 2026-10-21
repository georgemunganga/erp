using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private readonly Dictionary<string, Guid> receivingBrowserActors = [];
    private (Guid OrderId, Guid LineId, Guid IssuedId)? receivingBrowserGoods;
    private (Guid OrderId, Guid LineId, Guid IssuedId)? receivingBrowserService;

    private async Task<object> PrepareReceivingBrowserFixture()
    {
        Require(OrderDistinctRuntime, "Receiving browser proof requires distinct owner/runtime identities.");
        await Authority(OrderManager2, "BROWSER-RECEIVING-PO-M2", "PurchaseOrder.Approve");
        Guid supplierId;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier = new Supplier { Number = "BROWSER-RECEIVING", LegalName = "Browser receiving supplier", DisplayName = "Browser receiving supplier", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(supplier); await db.SaveChangesAsync(); supplierId = supplier.Id;
        }
        // All source approvals/funding/PO decisions/issue/delivery are real mapped HTTP
        // commands. No receipt, physical, inspection, acceptance or certificate is seeded.
        var goods = await DeliveredReceivingOrder(supplierId, "Goods");
        var service = await DeliveredReceivingOrder(supplierId, "Service");
        await PublishReceivingPolicy();
        receivingBrowserGoods = goods; receivingBrowserService = service;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            void Role(string key, string permissions) => core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = key, RoleName = "Synthetic " + key, Active = true, PermissionsCsv = "procurement-access," + permissions });
            Role("browser_receiving_receiver", "procurement-receipt-view,procurement-receipt-create,procurement-receipt-edit,procurement-receipt-record");
            Role("browser_receiving_inspector", "procurement-receipt-inspect");
            Role("browser_receiving_owner", "procurement-service-certify");
            Role("browser_receiving_acceptor", "procurement-receipt-view,procurement-receipt-accept");
            Role("browser_receiving_unassigned", "procurement-receipt-inspect,procurement-service-certify");
            Role("browser_receiving_reader", "procurement-receipt-view");
            foreach (var (principal, role) in new[] { (OrderRequester, "browser_receiving_receiver"), (OrderManager1, "browser_receiving_inspector"),
                (OrderManager2, "browser_receiving_owner"), (OrderBuyer, "browser_receiving_reader"), (OrderFinance, "browser_receiving_reader") })
            {
                (await core.LocalUsers.SingleAsync(x => x.Id == principal)).RolesCsv = role;
                // Finance is scoped by the common browser fixture immediately afterward.
                if (principal != OrderFinance && !await core.UserBranchAssignments.AnyAsync(x => x.UserId == principal && x.LocationId == OrderBranch && !x.IsArchived))
                    core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = principal, LocationId = OrderBranch });
            }
            foreach (var (name, role) in new[] { ("acceptor", "browser_receiving_acceptor"), ("unassigned", "browser_receiving_unassigned") })
            {
                var worker = new Worker { EmployeeNo = "HTTP-receiving-" + name, FirstName = name, LastName = "Receiving fixture", Status = "active", LocationId = OrderBranch };
                var user = new LocalUser { Email = "receiving-" + name + "@example.test", NormalizedEmail = ("receiving-" + name + "@example.test").ToUpperInvariant(),
                    DisplayName = name, PasswordHash = "synthetic-no-login-secret", WorkerId = worker.Id, RolesCsv = role };
                core.Workers.Add(worker); core.LocalUsers.Add(user); core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = OrderBranch });
                receivingBrowserActors[name] = user.Id;
            }
            await core.SaveChangesAsync();
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.GoodsReceipts.AnyAsync() && !await db.ServiceAcceptances.AnyAsync() && !await db.ReceivingSourceStates.AnyAsync()
                && !await db.ReceivingAcceptanceEffects.AnyAsync() && !await db.ReceiptInspections.AnyAsync(), "Receiving browser fixture must not seed operational receiving effects.");
            async Task<object> Descriptor((Guid OrderId, Guid LineId, Guid IssuedId) source)
            {
                var order = await db.PurchaseOrders.SingleAsync(x => x.Id == source.OrderId);
                var line = await db.PurchaseOrderLines.SingleAsync(x => x.Id == source.LineId);
                Require(order.Status == "Issued" && line.Quantity == 1 && line.UnitPrice == 100 && line.AcceptedQuantity == 0,
                    "Receiving browser source is not untouched actual issued authority.");
                return new { orderId = source.OrderId, orderNumber = order.Number, purchaseOrderLineId = source.LineId, issuedVersionId = source.IssuedId,
                    line.Description, line.UnitCode, currencyCode = "ZMW", siteId = OrderBranch, authorizedQuantity = "1", netUnitPrice = "100" };
            }
            return new { goods = await Descriptor(goods), service = await Descriptor(service), evidencePrefix = "RCP:",
                physicalQuantity = "2", qualifiedQuantity = "2", goodsAcceptQuantity = "1", partialServiceQuantity = "0.5",
                serviceNotes = new string('n', 1500), policyScope = "Synthetic independently published branch receiving policy", noReceivingEffectsSeeded = true };
        }
    }

    private static void VerifyReceivingBrowserContexts(JsonElement actors)
    {
        foreach (var (name, grants) in new[] {
            ("requester", "procurement-access,procurement-receipt-view,procurement-receipt-create,procurement-receipt-edit,procurement-receipt-record"),
            ("manager1", "procurement-access,procurement-receipt-inspect"), ("manager2", "procurement-access,procurement-service-certify"),
            ("acceptor", "procurement-access,procurement-receipt-view,procurement-receipt-accept"),
            ("unassigned", "procurement-access,procurement-receipt-inspect,procurement-service-certify"),
            ("buyer", "procurement-access,procurement-receipt-view"), ("finance", "procurement-access,procurement-receipt-view") })
        {
            var permissions = actors.GetProperty(name).GetProperty("context").GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            Require(permissions.SetEquals(grants.Split(',')), "Receiving browser actor has unexpected authority: " + name);
        }
    }

    private async Task VerifyReceivingBrowserReadBoundaries()
    {
        foreach (var principal in new[] { OrderManager1, OrderManager2, receivingBrowserActors["unassigned"] })
        {
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, Root + "/receiving/worklist", principal);
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, Root + "/receiving/sources", principal);
        }
        foreach (var (principal, path) in new[] { (OrderManager1, "/receiving/inspection-tasks"), (OrderManager2, "/receiving/service-tasks"),
            (receivingBrowserActors["unassigned"], "/receiving/inspection-tasks"), (receivingBrowserActors["unassigned"], "/receiving/service-tasks") })
        {
            var queue = await Get(Root + path, principal);
            Require(queue.GetProperty("total").GetInt32() == 0 && queue.GetProperty("items").GetArrayLength() == 0,
                "Unstarted or unnamed receiving task fixture exposed unexpected records.");
        }
        var sources = await Get(Root + "/receiving/sources?pageSize=100", OrderRequester);
        foreach (var source in new[] { receivingBrowserGoods!.Value, receivingBrowserService!.Value })
            Require(sources.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("orderId").GetGuid() == source.OrderId
                && x.GetProperty("purchaseOrderLineId").GetGuid() == source.LineId && x.GetProperty("issuedVersionId").GetGuid() == source.IssuedId),
                "Receiver source picker lost actual scoped issued lineage.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        Require(!await db.GoodsReceipts.AnyAsync() && !await db.ServiceAcceptances.AnyAsync() && !await db.ReceivingSourceStates.AnyAsync()
            && !await db.ReceivingAcceptanceEffects.AnyAsync(), "Receiving preview/bootstrap fabricated operational evidence.");
    }

    public async Task VerifyReceivingBrowserCompletion()
    {
        Require(receivingBrowserGoods.HasValue && receivingBrowserService.HasValue, "Receiving browser source was not prepared.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var goods = receivingBrowserGoods!.Value; var service = receivingBrowserService!.Value;
        var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        var acceptorWorker = (await core.LocalUsers.Where(x => x.Id == receivingBrowserActors["acceptor"]).Select(x => x.WorkerId).SingleAsync())!.Value;
        var serviceOwnerWorker = (await core.LocalUsers.Where(x => x.Id == OrderManager2).Select(x => x.WorkerId).SingleAsync())!.Value;
        var receipt = await db.GoodsReceipts.SingleAsync(x => x.PurchaseOrderId == goods.OrderId && x.ExecutionVersion == 1);
        var line = await db.GoodsReceiptLines.SingleAsync(x => x.GoodsReceiptId == receipt.Id);
        var inspection = await db.ReceiptInspections.SingleAsync(x => x.GoodsReceiptLineId == line.Id && x.ExecutionVersion == 1);
        var goodsState = await db.ReceivingSourceStates.SingleAsync(x => x.PurchaseOrderLineId == goods.LineId);
        var goodsEffect = await db.ReceivingAcceptanceEffects.SingleAsync(x => x.ReceivingSourceStateId == goodsState.Id);
        var certificates = await db.ServiceAcceptances.Where(x => x.SourcePurchaseOrderId == service.OrderId && x.ExecutionVersion == 1).ToArrayAsync();
        var serviceState = await db.ReceivingSourceStates.SingleAsync(x => x.PurchaseOrderLineId == service.LineId);
        var serviceEffects = await db.ReceivingAcceptanceEffects.Where(x => x.ReceivingSourceStateId == serviceState.Id).ToArrayAsync();
        Require(line.ReceivedQuantity == 2 && line.InitiallyRejectedQuantity == 0 && line.AcceptedQuantity == 1 && line.RejectedQuantity == 0
            && inspection.QualifiedQuantity == 2 && inspection.InspectorSubjectId == OrderManager1.ToString("D")
            && receipt.ReceiverSubjectId == OrderRequester.ToString("D") && goodsState.Sequence == 3
            && goodsState.CurrentAcceptedQuantity == 1 && goodsState.CurrentAcceptedNetAmount == 100
            && goodsEffect.QuantityDelta == 1 && goodsEffect.NetAmountDelta == 100
            && goodsEffect.ActorWorkerId == acceptorWorker, "Receiving browser goods conservation, independent actor or overdelivery evidence drifted.");
        Require(certificates.Length == 2 && certificates.All(x => x.Status == "Accepted" && x.AcceptedQuantity == .5m && x.AcceptedAmount == 50m
                && x.OwnerSubjectId == OrderManager2.ToString("D") && x.Reason == null)
            && certificates.Any(x => x.Notes == new string('n', 1500))
            && serviceState.Sequence == 2 && serviceState.CurrentAcceptedQuantity == 1 && serviceState.CurrentAcceptedNetAmount == 100
            && serviceEffects.Length == 2 && serviceEffects.All(x => x.QuantityDelta == .5m && x.NetAmountDelta == 50m && x.ActorWorkerId == serviceOwnerWorker),
            "Receiving browser partial service certificates, notes, CAS effects or independent owner drifted.");
        foreach (var source in new[] { goods, service })
        {
            var poLine = await db.PurchaseOrderLines.SingleAsync(x => x.Id == source.LineId);
            var prLine = await db.PurchaseRequestLines.SingleAsync(x => x.Id == poLine.PurchaseRequestLineId);
            Require(poLine.AcceptedQuantity == 1 && prLine.AcceptedQuantity == 1, "Receiving browser source counters are not conserved.");
        }
        var targets = certificates.Select(x => x.Id).Append(receipt.Id).ToArray();
        var effectTargets = targets.Append(line.Id).ToArray();
        Require(await db.CommercialCommandReceipts.CountAsync(x => x.Operation.StartsWith("Receiving") || x.Operation.StartsWith("Service")) == 8
            && await db.AuditEvents.CountAsync(x => targets.Contains(x.RecordId) && (x.Action.StartsWith("Receiving") || x.Action.StartsWith("Service"))) == 8
            && await db.OutboxEvents.CountAsync(x => effectTargets.Contains(x.AggregateId) && x.AggregateType == "Receiving") == 5,
            "Receiving browser replay duplicated command/audit/outbox evidence or left partial effects.");
        Require(!await db.SupplierInvoices.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync(), "Receiving browser manufactured invoice/AP authority.");
        Console.WriteLine("PASS browser receiving persisted DB: physical2/qualified2/accepted1/held1, two service0.5/net50 certificates, independent scoped actors, conserved PR/PO counters and exactly8 command/audit+5 outbox effects; no invoice/AP authority");
    }
}
