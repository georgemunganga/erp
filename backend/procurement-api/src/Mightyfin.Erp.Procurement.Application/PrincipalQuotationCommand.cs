using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed command content, not identity/authority supplied by a client.
/// Canonical evidence deliberately excludes the optional current employee alias.</summary>
public sealed record PrincipalQuotationCommand(string Operation, long ExpectedEventVersion,
    long ExpectedInvitationVersion, long? ExpectedBidVersion, string? Reason,
    DateTimeOffset? ReceivedAt, QuotationCommercialTerms? Offer,
    IReadOnlyList<PrincipalQuotationStorage.LineInput>? Lines);

public static class PrincipalQuotationCommandEvidence
{
    private sealed record WireLine(Guid SourcingLineId, string Quantity, string UnitPrice,
        string Discount, string DiscountValue, string TaxTreatment, string QuotedTaxAmount,
        string DeliveryAmount, string? SupplierOfferedTaxCode, string? LeadTimeText, string? Notes);
    private sealed record Wire(int SchemaVersion, string TenantId, Guid LegalEntityId,
        Guid BranchId, string ActorPrincipalId, Guid CommandKey, Guid SourcingEventId,
        Guid InvitationId, Guid? SupplierBidId, string Operation, long ExpectedEventVersion,
        long ExpectedInvitationVersion, long? ExpectedBidVersion, string? Reason,
        DateTimeOffset? ReceivedAt, QuotationCommercialTerms? Offer, IReadOnlyList<WireLine>? Lines);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PrincipalQuotationCommand Normalize(PrincipalQuotationCommand command)
    {
        if (command is null || command.Operation is not ("Create" or "Revise" or "Cancel")
            || command.Operation == "Create" && command.ExpectedBidVersion is not null
            || command.Operation != "Create" && command.ExpectedBidVersion is not > 0)
            throw new InvalidOperationException("QuotationCommandInvalid");
        var reason = command.Reason?.Trim();
        if (command.Operation == "Create")
        {
            if (reason is not null) throw new InvalidOperationException("QuotationCreateReasonUnexpected");
        }
        else if (reason is not { Length: > 0 and <= 1000 })
            throw new InvalidOperationException("QuotationChangeReasonRequired");
        if (command.Operation == "Cancel")
        {
            if (command.Offer is not null || command.Lines is not null || command.ReceivedAt is not null
                || command.ExpectedEventVersion != 0 || command.ExpectedInvitationVersion != 0)
                throw new InvalidOperationException("QuotationCancelMustPreserveTerms");
            return command with { Reason = reason };
        }
        if (command.ExpectedEventVersion < 1 || command.ExpectedInvitationVersion < 1
            || command.Offer is null || command.Lines is null || command.Lines.Count is < 1 or > 200
            || command.Lines.Any(x => x is null) || command.ReceivedAt == default(DateTimeOffset))
            throw new InvalidOperationException("QuotationCommandInvalid");
        PrincipalQuotationCommercialTerms.Validate(command.Offer);
        // Validate the complete pure commercial input before mutable owner lookup.
        // This does not confer source, supplier, document or Core permission.
        _ = PrincipalQuotationCommercialTerms.Calculate(command.Lines.Select(PrincipalQuotationStorage.Commercial).ToArray());
        foreach (var line in command.Lines)
            foreach (var amount in new[] { line.Quantity, line.UnitPrice, line.DiscountValue,
                line.QuotedTaxAmount, line.DeliveryAmount })
                if (!ExactDecimalJsonCodec.IsRepresentable(amount, 18, 4))
                    throw new InvalidOperationException("QuotationAmountPrecisionInvalid");
        var offer = command.Offer with
        {
            Documents = command.Offer.Documents!.OrderBy(x => x.DocumentId).ThenBy(x => x.VersionId).ToArray(),
        };
        return command with { Reason = reason, ReceivedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(command.ReceivedAt),
            Offer = offer, Lines = command.Lines.OrderBy(x => x.SourcingLineId).ToArray() };
    }

    public static string Build(string tenant, Guid company, Guid branch, string actor, Guid key,
        Guid sourcingId, Guid invitationId, Guid? bidId, PrincipalQuotationCommand command)
    {
        if (command is null || string.IsNullOrWhiteSpace(tenant) || tenant.Length > 128 || company == Guid.Empty
            || branch == Guid.Empty || !ProcurementPrincipalEvidence.Canonical(actor)
            || key == Guid.Empty || sourcingId == Guid.Empty || invitationId == Guid.Empty
            || bidId == Guid.Empty || command.Operation == "Create" && bidId is not null
            || command.Operation != "Create" && bidId is null)
            throw new InvalidOperationException("QuotationCommandScopeInvalid");
        var normalized = Normalize(command);
        var wire = new Wire(3, tenant, company, branch, actor, key, sourcingId, invitationId, bidId,
            normalized.Operation, normalized.ExpectedEventVersion, normalized.ExpectedInvitationVersion,
            normalized.ExpectedBidVersion, normalized.Reason, normalized.ReceivedAt, normalized.Offer,
            normalized.Lines?.Select(x => new WireLine(x.SourcingLineId, Exact(x.Quantity), Exact(x.UnitPrice),
                x.Discount.ToString(), Exact(x.DiscountValue), x.TaxTreatment.ToString(), Exact(x.QuotedTaxAmount),
                Exact(x.DeliveryAmount), x.SupplierOfferedTaxCode, x.LeadTimeText, x.Notes)).ToArray());
        var text = JsonSerializer.Serialize(wire, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > 8388608)
            throw new InvalidOperationException("QuotationRequestTooLarge");
        return text;
    }

    public static string FromStored(SourcingManualQuoteVersion row, PrincipalQuotationStorage.Restored stored)
    {
        var cancel = row.Operation == "Cancel";
        var lines = stored.Lines.Select(x => new PrincipalQuotationStorage.LineInput(x.SourcingLineId,
            x.Quantity, x.UnitPrice, Enum.Parse<QuotationDiscount>(x.DiscountKind!), x.DiscountValue,
            Enum.Parse<QuotationQuotedTax>(x.TaxTreatment!), x.TaxAmount, x.DeliveryAmount,
            x.SupplierOfferedTaxCode, x.LeadTimeText, x.Notes)).ToArray();
        var command = new PrincipalQuotationCommand(row.Operation, cancel ? 0 : row.EventVersionAtCapture,
            cancel ? 0 : row.InvitationVersionAtCapture, row.Operation == "Create" ? null : row.BidVersion - 1,
            row.Operation == "Create" ? null : stored.Bid.CorrectionReason,
            cancel ? null : stored.Bid.ReceivedAt, cancel ? null : stored.Offer, cancel ? null : lines);
        return Build(row.TenantId, row.LegalEntityId, row.BranchId, row.ActorPrincipalId, row.CommandKey,
            row.SourcingEventId, row.InvitationId, row.Operation == "Create" ? null : row.SupplierBidId, command);
    }
    private static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
}
