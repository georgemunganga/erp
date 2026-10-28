using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>One company-wide independent Core reviewer for the immutable
/// Procurement invoice-history baseline. This policy never grants billing
/// capacity; Core's terminal decision and a current owner reconciliation do.</summary>
public static class InvoiceProcurementBaselineReviewPolicyRules
{
    public const string PolicyKey="INVOICE_OWNER_BASELINE_REVIEW";
    public const string TransactionType="InvoiceOwnerBaselineReview";

    public sealed record Document(int SchemaVersion,string TransactionType,string ReviewerPrincipalId);
    public sealed record Person(string TenantId,Guid LegalEntityId,string PrincipalId,
        Guid? WorkerAliasId,bool Active,bool OrganisationWide,IReadOnlySet<string> Permissions);

    public static bool IsDocument(string? json)
    {
        if(string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json)>4096)return false;
        try
        {
            using var parsed=JsonDocument.Parse(json);
            return parsed.RootElement.ValueKind==JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("transactionType",out var type)
                && type.ValueKind==JsonValueKind.String && type.GetString()==TransactionType;
        }
        catch(JsonException){return false;}
    }

    public static IReadOnlyList<string> Validate(string? json)
    {
        try { _=Parse(json);return []; }
        catch(InvalidOperationException){return ["InvoiceOwnerBaselineNamedReviewerRequired"];}
    }

    public static Document Parse(string? json)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json)>4096
                || ProcurementEvidenceEncoding.PolicyJsonDigest(json).Length!=64)
                throw new InvalidOperationException();
            using var parsed=JsonDocument.Parse(json);
            var root=parsed.RootElement;
            var fields=root.EnumerateObject().Select(x=>x.Name).Order(StringComparer.Ordinal).ToArray();
            if(root.ValueKind!=JsonValueKind.Object
                || !fields.SequenceEqual(new[]{"reviewerPrincipalId","schemaVersion","transactionType"})
                || root.GetProperty("schemaVersion").GetInt32()!=1
                || root.GetProperty("transactionType").GetString()!=TransactionType
                || !ProcurementPrincipalEvidence.Canonical(root.GetProperty("reviewerPrincipalId").GetString()))
                throw new InvalidOperationException();
            return new(1,TransactionType,root.GetProperty("reviewerPrincipalId").GetString()!);
        }
        catch(Exception error) when(error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {throw new InvalidOperationException("InvoiceOwnerBaselineReviewPolicyInvalid",error);}
    }

    public static Person Resolve(PolicyVersion policy,string tenant,Guid company,
        string makerPrincipalId,Guid? makerWorkerAliasId,Person reviewer,DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(reviewer);
        var document=Parse(policy.RulesJson);
        if(company==Guid.Empty || string.IsNullOrWhiteSpace(tenant) || at==default
            || !ProcurementPrincipalEvidence.Canonical(makerPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(makerWorkerAliasId)
            || policy.PolicyKey!=PolicyKey || policy.TenantId!=tenant || policy.LegalEntityId!=company
            || policy.State!="Published" || policy.PolicyVersionNumber<1
            || policy.PublishedAt is not { } published || published>at
            || policy.EffectiveFrom==default || policy.EffectiveFrom>at
            || policy.EffectiveTo is { } end && end<=at
            || !ProcurementPrincipalEvidence.Canonical(policy.CreatedBy)
            || !ProcurementPrincipalEvidence.Canonical(policy.PublishedBy)
            || policy.CreatedBy==policy.PublishedBy
            || string.IsNullOrWhiteSpace(policy.PublicationReason)
            || reviewer.TenantId!=tenant || reviewer.LegalEntityId!=company
            || reviewer.PrincipalId!=document.ReviewerPrincipalId
            || !reviewer.Active || !reviewer.OrganisationWide
            || reviewer.Permissions is null
            || !reviewer.Permissions.Contains("procurement-access")
            || !reviewer.Permissions.Contains(InvoiceProcurementBaselineEvidence.ReviewPermission)
            || !ProcurementPrincipalEvidence.OptionalWorker(reviewer.WorkerAliasId)
            || !ProcurementPrincipalEvidence.Independent(makerPrincipalId,makerWorkerAliasId,
                reviewer.PrincipalId,reviewer.WorkerAliasId))
            throw new InvalidOperationException("InvoiceOwnerBaselineReviewerUnavailable");
        return reviewer;
    }
}
