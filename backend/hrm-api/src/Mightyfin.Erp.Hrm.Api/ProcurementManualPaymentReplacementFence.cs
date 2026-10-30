using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentCommandRoutes
{
    // Invoked after original-key replay and the existing invoice FOR UPDATE.
    // The additive replacement tables can be absent; a retained link must be
    // authenticated through the complete owner graph before it denies work.
    private static async Task RequireNoReplacedInvoiceSourceAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, Guid invoiceId, CancellationToken ct)
    {
        var linked = await db.ReadVerifiedInvoiceReplacementAsync(invoiceId,
            (requirement, token) => ProcurementInvoiceIntakeRoutes.RetainedReplacementReference(
                db, scope, requirement, token), ct);
        if (linked is { } verified && verified.Link.SourceInvoiceId == invoiceId)
            throw new PaymentProblem("payment-source-replaced", 409);
    }
}
