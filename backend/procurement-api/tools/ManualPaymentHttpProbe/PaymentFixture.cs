using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

internal sealed record PaymentPerson(Guid Id, string Email, string Password);
internal sealed record PaymentFixture(string Tenant, Guid Company, Guid Branch, Guid OtherBranch,
    Guid Invoice, Guid Supplier, IReadOnlyDictionary<string, PaymentPerson> People);

internal static class PaymentFixtureStore
{
    // Seed is kept as a single owner-only entry point. These fixture
    // credentials live in process memory and never enter a result file or console.
    internal static async Task<PaymentFixture> Seed(string owner, string tenant)
    {
        var company = new LegalEntity { TenantId = tenant, Code = "PAY-HTTP-" + tenant[^8..],
            RegisteredName = "Synthetic payment HTTP company", Currency = "ZMW", TimeZoneId = "UTC",
            IsDefault = true, CreatedAt = DateTimeOffset.UtcNow };
        var branch = new WorkLocation { TenantId = tenant, LegalEntityId = company.Id,
            Code = "PAY-A", Name = "Synthetic purchase branch", CreatedAt = DateTimeOffset.UtcNow };
        var other = new WorkLocation { TenantId = tenant, LegalEntityId = company.Id,
            Code = "PAY-B", Name = "Synthetic other branch", CreatedAt = DateTimeOffset.UtcNow };
        var roles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["maker"] = "procurement-access,procurement-payment-read,procurement-payment-create,procurement-payment-edit,procurement-payment-submit,procurement-payment-cancel,procurement-payment-approve",
            ["approver"] = "procurement-access,procurement-payment-read,procurement-payment-approve,procurement-payment-cancel",
            ["recorder"] = "procurement-access,procurement-payment-read,procurement-payment-record",
            ["confirmer"] = "procurement-access,procurement-payment-read,procurement-payment-confirm",
            ["policy_author"] = "procurement-access,procurement-configuration-read,procurement-configuration-edit",
            ["policy_publisher"] = "procurement-access,procurement-configuration-read,procurement-configuration-approve",
            ["outsider"] = "procurement-access,procurement-payment-read,procurement-payment-create"
        };
        var people = new Dictionary<string, PaymentPerson>(StringComparer.Ordinal);
        await using (var db = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(owner,
            x => x.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new ProbeTenant(tenant)))
        {
            db.LegalEntities.Add(company); db.WorkLocations.AddRange(branch, other);
            foreach (var (name, actions) in roles)
            {
                var role = "payment_http_" + name;
                var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var email = name + "." + tenant + "@example.invalid";
                var person = new LocalUser { TenantId = tenant, Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    DisplayName = "Synthetic " + name, PasswordHash = ProductionHash(password), RolesCsv = role,
                    WorkerId = null, MustChangePassword = false, CreatedAt = DateTimeOffset.UtcNow };
                db.LocalUsers.Add(person);
                db.TenantRoleAssignments.Add(new TenantRoleAssignment { TenantId = tenant, RoleKey = role,
                    RoleName = "Private payment fixture " + name, Category = "system", Active = true,
                    PermissionsCsv = actions, CreatedAt = DateTimeOffset.UtcNow });
                db.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = tenant,
                    UserId = person.Id, UserEmail = email, LocationId = name == "outsider" ? other.Id : branch.Id,
                    CreatedAt = DateTimeOffset.UtcNow });
                people.Add(name, new(person.Id, email, password));
            }
            await db.SaveChangesAsync();
        }
        var maker = people["maker"].Id;
        var supplier = new Supplier { TenantId = tenant, LegalEntityId = company.Id,
            Number = "PAY-SYN-001", LegalName = "Synthetic payment supplier", DisplayName = "Synthetic payment supplier",
            CountryCode = "ZM", Status = "Approved", IsOrderable = true,
            CreatedBy = maker.ToString("D"), CreatedAt = DateTimeOffset.UtcNow };
        var invoice = new SupplierInvoice { TenantId = tenant, LegalEntityId = company.Id, BranchRef = branch.Id,
            SupplierId = supplier.Id, InvoiceNumber = "PAY-HTTP-001", NormalizedInvoiceNumber = "PAY-HTTP-001",
            Status = "Received", IntakeChannel = "ManualEntry", CurrencyCode = "ZMW", InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            NetAmount = 1000m, TaxAmount = 0m, GrossAmount = 1000m, MatchingState = "NotCompared",
            CreatedBy = maker.ToString("D"), CreatedAt = DateTimeOffset.UtcNow };
        await using (var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(owner, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
            new ProbeProcurementScope(tenant, company.Id, branch.Id, maker)))
        {
            db.AddScoped(supplier); db.AddScoped(invoice);
            db.AddScoped(new SupplierInvoiceLine { TenantId = tenant, LegalEntityId = company.Id,
                SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Synthetic service invoice",
                Quantity = 1m, UnitPrice = 1000m, TaxAmount = 0m,
                CreatedBy = maker.ToString("D"), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        return new(tenant, company.Id, branch.Id, other.Id, invoice.Id, supplier.Id, people);
    }

    internal static async Task VerifyOwnerEvidence(string owner, PaymentFixture fixture)
    {
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(owner, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
            new ProbeProcurementScope(fixture.Tenant, fixture.Company, fixture.Branch, fixture.People["maker"].Id));
        var requests = await db.ManualPaymentRequests.AsNoTracking().Where(x => x.SupplierInvoiceId == fixture.Invoice).ToArrayAsync();
        if (requests.Count(x => x.Status == "ConfirmedManually") != 1
            || requests.Count(x => x.Status == "Cancelled") != 1
            || await db.FinancePaymentStatusRefs.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == fixture.Invoice))
            throw new InvalidOperationException("The owner evidence differs from the expected manual workflow and no Finance payment.");
        foreach (var request in requests)
        {
            var transitions = await db.ManualPaymentDecisions.AsNoTracking().CountAsync(x => x.ManualPaymentRequestId == request.Id);
            var receipts = await db.ManualPaymentReceipts.AsNoTracking().CountAsync(x => x.ManualPaymentRequestId == request.Id);
            if (transitions != request.Version || receipts != transitions)
                throw new InvalidOperationException("The manual payment transition/receipt history is incomplete.");
        }
    }

    private static string ProductionHash(string password)
    {
        // Invoke the actual ERP hasher without copying a password codec into this tool.
        var type = typeof(MonthlyLeaveAccrualWorker).Assembly.GetType("LocalPasswordHash", true)!;
        var method = type.GetMethod("Hash", BindingFlags.Public | BindingFlags.Static)
            ?? throw new ProbeUnavailable("LocalPasswordHasherUnavailable");
        return (string)(method.Invoke(null, [password]) ?? throw new ProbeUnavailable("LocalPasswordHasherUnavailable"));
    }
}
