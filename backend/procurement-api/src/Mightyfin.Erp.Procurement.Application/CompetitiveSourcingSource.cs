using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record CompetitiveSourceAvailability(decimal AvailableQuantity, decimal ApprovedUnitPrice,
    decimal ApprovedLineBase, string CurrencyCode, int MinimumQuoteCount, Guid SourceSubmissionId,
    Guid SourceCaseId, Guid MethodId, long MethodRevision);

/// <summary>Competitive demand eligibility only. This never creates a reservation, publishes
/// an RFQ or grants reviewer authority. Current canonical identity, Finance, catalog and source
/// locks remain the responsibility of the governed command adapter.</summary>
public static class CompetitiveSourcingSource
{
    public static CompetitiveSourceAvailability Check(ApprovedPurchaseSource source,
        string tenant, Guid company, DateTimeOffset at)
    {
        ApprovedPurchaseDemand.Verify(source, tenant, company, at);
        if (source.Line.Route is not ("Unassigned" or "PendingSourcing"))
            Fail("SourcingSourceRouteNotCompetitive");
        var method = source.Method;
        if (method.Id == Guid.Empty || method.TenantId != tenant || method.LegalEntityId != company
            || method.Status != "Active" || method.ExecutionRoute != "CompetitiveSourcing")
            Fail("SourcingSourceMethodNotCompetitive");
        var requiredQuotes = QuoteRequirement(source);
        var balance = PurchaseDemandAllocationBalance.Evaluate(source.Request, source.Line,
            source.Allocations, source.AllocationOwners ?? [], tenant, company);
        return new(balance.AvailableQuantity, source.Line.EstimatedUnitPrice,
            ExactDecimalArithmetic.Multiply(source.Line.ApprovedQuantity, source.Line.EstimatedUnitPrice),
            source.Request.CurrencyCode, requiredQuotes, source.Submission.Id, source.CoreCase.Id,
            method.Id, method.Version);
    }

    private static int QuoteRequirement(ApprovedPurchaseSource source)
    {
        try
        {
            using var document = JsonDocument.Parse(source.Evaluation.DecisionJson);
            var lines = document.RootElement.GetProperty("lines").EnumerateArray()
                .Where(x => x.GetProperty("lineId").GetGuid() == source.Line.Id).ToArray();
            if (lines.Length != 1) Fail("SourcingSourcePolicyMismatch");
            var decision = lines[0].GetProperty("decision");
            if (decision.GetProperty("status").GetString() != "Match"
                || !decision.TryGetProperty("sourcingMethodCode", out var method)
                || method.ValueKind != JsonValueKind.String || method.GetString() != source.Method.Code
                || !decision.TryGetProperty("minimumQuoteCount", out var quotes)
                || quotes.ValueKind != JsonValueKind.Number || !quotes.TryGetInt32(out var count)
                || count is < 0 or > 20)
                Fail("SourcingSourcePolicyMismatch");
            return decision.GetProperty("minimumQuoteCount").GetInt32();
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException)
        { throw new InvalidOperationException("SourcingSourcePolicyMismatch", error); }
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
