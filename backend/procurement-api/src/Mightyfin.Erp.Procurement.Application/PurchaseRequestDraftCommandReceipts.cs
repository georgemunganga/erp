using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Structural completed-result evidence, not authentication or full RequestDetail
/// validation. Routes must still enforce current Core identity/actions/scope, the complete
/// existing DTO, command intent and atomic business/audit/receipt persistence.</summary>
public static class PurchaseRequestDraftCommandReceipts
{
    public const int FingerprintSchemaVersion = 1;
    public const int ResponseSchemaVersion = 1;
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Submit = "Submit";

    public static bool OperationSupported(string? operation) => operation is Create or Update or Submit;

    // Reuse the canonical nonzero Core/command GUID representation; header cardinality
    // remains an HTTP adapter concern. No UUID-version restriction is introduced.
    public static bool TryCommandKey(string? value, out Guid key)
    {
        key = default;
        return ProcurementPrincipalEvidence.Canonical(value) && Guid.TryParseExact(value, "D", out key);
    }

    /// <summary>Digest a caller-built explicit intent object using the existing lossless
    /// canonical codec. This is not a substitute for the owning route's field allowlist.</summary>
    public static string Fingerprint(string canonicalIntentJson)
    {
        var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(canonicalIntentJson);
        Require(digest.Length == 64, "RequestDraftCommandIntentInvalid");
        using var document = JsonDocument.Parse(canonicalIntentJson);
        Require(NoCaseAliases(document.RootElement), "RequestDraftCommandIntentInvalid");
        return digest;
    }

    public static PurchaseRequestDraftCommandReceipt Complete(PurchaseRequestDraftCommandMetadata metadata,
        string originalResultJson)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Require(metadata.CompletedAt != default, "RequestDraftCommandCompletionInvalid");
        var completed = ProcurementEvidenceEncoding.DatabaseTimestamp(metadata.CompletedAt)!.Value;
        var receipt = new PurchaseRequestDraftCommandReceipt
        {
            TenantId = metadata.TenantId, LegalEntityId = metadata.LegalEntityId,
            CreatedBy = metadata.ActorPrincipalId.ToString("D"), CreatedAt = completed,
            Operation = metadata.Operation, CommandKey = metadata.CommandKey,
            ActorPrincipalId = metadata.ActorPrincipalId,
            PurchaseRequestId = metadata.PurchaseRequestId,
            ContextBranchRef = metadata.ContextBranchRef, ContextDepartmentRef = metadata.ContextDepartmentRef,
            OriginalBranchRef = metadata.OriginalBranchRef, OriginalDepartmentRef = metadata.OriginalDepartmentRef,
            ExpectedVersion = metadata.ExpectedVersion, ResultVersion = metadata.ResultVersion,
            FingerprintSchemaVersion = FingerprintSchemaVersion, CommandFingerprint = metadata.CommandFingerprint,
            ResponseSchemaVersion = ResponseSchemaVersion, ResponseStatusCode = metadata.Operation == Create ? 201 : 200,
            ResponseJson = originalResultJson, ResponseSha256 = ProcurementEvidenceEncoding.Digest(originalResultJson),
            CompletedAt = completed,
        };
        ValidateCompleted(receipt);
        return receipt;
    }

    public static void ValidateCompleted(PurchaseRequestDraftCommandReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Require(receipt.Id != Guid.Empty && !string.IsNullOrWhiteSpace(receipt.TenantId)
            && receipt.LegalEntityId != Guid.Empty && receipt.ActorPrincipalId != Guid.Empty
            && receipt.PurchaseRequestId != Guid.Empty && receipt.CommandKey != Guid.Empty,
            "RequestDraftCommandIdentityInvalid");
        Require(OperationSupported(receipt.Operation), "RequestDraftCommandOperationInvalid");
        Require(OptionalId(receipt.ContextBranchRef) && OptionalId(receipt.ContextDepartmentRef)
            && OptionalId(receipt.OriginalBranchRef) && OptionalId(receipt.OriginalDepartmentRef),
            "RequestDraftCommandScopeInvalid");
        Require((receipt.ContextBranchRef is null || receipt.ContextBranchRef == receipt.OriginalBranchRef)
            && (receipt.ContextDepartmentRef is null || receipt.ContextDepartmentRef == receipt.OriginalDepartmentRef)
            && (receipt.Operation != Create || receipt.ContextBranchRef == receipt.OriginalBranchRef
                && receipt.ContextDepartmentRef == receipt.OriginalDepartmentRef), "RequestDraftCommandScopeInvalid");
        Require(receipt.FingerprintSchemaVersion == FingerprintSchemaVersion
            && receipt.ResponseSchemaVersion == ResponseSchemaVersion
            && Sha256(receipt.CommandFingerprint), "RequestDraftCommandFingerprintInvalid");
        Require(receipt.Operation == Create
                ? receipt.ExpectedVersion is null && receipt.ResultVersion == 1 && receipt.ResponseStatusCode == 201
                : receipt.ExpectedVersion is > 0 and < long.MaxValue
                    && receipt.ResultVersion == receipt.ExpectedVersion.Value + 1 && receipt.ResponseStatusCode == 200,
            "RequestDraftCommandVersionInvalid");
        Require(receipt.CompletedAt != default && receipt.CompletedAt.Offset == TimeSpan.Zero
            && receipt.CompletedAt.Ticks % 10 == 0, "RequestDraftCommandCompletionInvalid");
        Require(Sha256(receipt.ResponseSha256)
            && receipt.ResponseSha256 == ProcurementEvidenceEncoding.Digest(receipt.ResponseJson),
            "RequestDraftCommandResponseDigestInvalid");
        Require(ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson).Length == 64,
            "RequestDraftCommandResponseJsonInvalid");
        using var document = JsonDocument.Parse(receipt.ResponseJson);
        var result = document.RootElement;
        Require(NoCaseAliases(result), "RequestDraftCommandResponseJsonInvalid");
        Require(Id(result, "id") == receipt.PurchaseRequestId
            && NullableId(result, "requesterPrincipalId", out var requesterPrincipal)
            && (requesterPrincipal == receipt.ActorPrincipalId
                || receipt.Operation != Create && requesterPrincipal is null)
            && NullableId(result, "branchRef", out var branch) && branch == receipt.OriginalBranchRef
            && NullableId(result, "departmentRef", out var department) && department == receipt.OriginalDepartmentRef
            && NullableId(result, "requesterWorkerId", out _)
            && result.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt64(out var number)
            && number == receipt.ResultVersion
            && result.TryGetProperty("requestEditVersion", out var editVersion) && editVersion.ValueKind == JsonValueKind.Number
            && editVersion.TryGetInt32(out var edit)
            && edit == 1, "RequestDraftCommandResponseBindingInvalid");
        Require(result.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
            && (receipt.Operation == Create ? status.GetString() == "Draft"
                : receipt.Operation == Update ? status.GetString() is "Draft" or "ReturnedForChanges"
                : status.GetString() == "Submitted"), "RequestDraftCommandResponseStateInvalid");
        if (receipt.Operation == Submit)
            Require(result.TryGetProperty("approvalState", out var approval)
                && approval.ValueKind == JsonValueKind.String && approval.GetString() == "PendingPolicyAndBudget",
                "RequestDraftCommandResponseStateInvalid");
        Require(result.TryGetProperty("currencyCode", out var currency) && currency.ValueKind == JsonValueKind.String
            && currency.GetString() is { Length: 3 } code && code.All(c => c is >= 'A' and <= 'Z'),
            "RequestDraftCommandResponseCurrencyInvalid");
    }

    // Deliberately inspect only result linkage metadata, not business lines/fields.
    // Historic null requesterPrincipalId is not ownership: Update/Submit routes and
    // replay/GET must independently call existing scoped PurchaseRequestOwnership,
    // including its exact canonical CreatedBy fallback. Never backfill/adopt a record.
    // Case aliases are rejected recursively; the shared codec rejects exact duplicates,
    // unsupported root shapes and numbers whose original precision would be lost.
    private static bool NoCaseAliases(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return element.EnumerateObject().All(p => names.Add(p.Name) && NoCaseAliases(p.Value));
        }
        return element.ValueKind != JsonValueKind.Array || element.EnumerateArray().All(NoCaseAliases);
    }
    private static Guid Id(JsonElement result, string name) => result.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && ProcurementPrincipalEvidence.Canonical(value.GetString())
        && value.TryGetGuid(out var id) ? id : Guid.Empty;
    private static bool NullableId(JsonElement result, string name, out Guid? id)
    {
        id = null;
        if (!result.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.Null) return true;
        var parsed = Id(result, name);
        if (parsed == Guid.Empty) return false;
        id = parsed;
        return true;
    }
    private static bool OptionalId(Guid? value) => value != Guid.Empty;
    private static bool Sha256(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool condition, string error)
    { if (!condition) throw new InvalidOperationException(error); }
}

public sealed record PurchaseRequestDraftCommandMetadata(
    string TenantId, Guid LegalEntityId, string Operation, Guid CommandKey,
    Guid ActorPrincipalId, Guid PurchaseRequestId,
    Guid? ContextBranchRef, Guid? ContextDepartmentRef,
    Guid? OriginalBranchRef, Guid? OriginalDepartmentRef,
    long? ExpectedVersion, long ResultVersion, string CommandFingerprint, DateTimeOffset CompletedAt);
