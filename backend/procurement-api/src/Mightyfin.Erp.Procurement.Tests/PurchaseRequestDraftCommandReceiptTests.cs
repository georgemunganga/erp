using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestDraftCommandReceiptTests
{
    private static readonly Guid Actor = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Company = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Request = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Branch = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Department = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Other = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid Key = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Theory]
    [InlineData("Create", null, 1, "Draft", 201)]
    [InlineData("Update", 1L, 2L, "Draft", 200)]
    [InlineData("Update", 7L, 8L, "ReturnedForChanges", 200)]
    [InlineData("Submit", 3L, 4L, "Submitted", 200)]
    public void Completes_each_supported_operation_with_original_bytes_and_optional_employee(
        string operation, long? expected, long result, string state, int status)
    {
        var metadata = Metadata(operation) with { ExpectedVersion = expected, ResultVersion = result };
        var json = Result(metadata, state);
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(metadata, json);
        Assert.Equal(json, receipt.ResponseJson);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(json), receipt.ResponseSha256);
        Assert.Equal(status, receipt.ResponseStatusCode);
        Assert.Equal(Actor, receipt.ActorPrincipalId);
        Assert.Equal(Request, receipt.PurchaseRequestId);
        Assert.Equal(result, receipt.ResultVersion);
        Assert.Equal(TimeSpan.Zero, receipt.CompletedAt.Offset);
        Assert.Equal(0, receipt.CompletedAt.Ticks % 10);
        Assert.Equal(ProcurementEvidenceEncoding.DatabaseTimestamp(metadata.CompletedAt), receipt.CompletedAt);
        PurchaseRequestDraftCommandReceipts.ValidateCompleted(receipt);
    }

    [Fact]
    public void Original_result_is_stored_without_reformatting_rounding_or_new_size_limit()
    {
        var metadata = Metadata();
        var json = Result(metadata).Replace("\"notesToApprover\":null", "\"notesToApprover\":\"" + new string('x', 1_100_000) + "\"");
        json = " \n" + json + "\n";
        Assert.True(json.Length > 1_048_576);
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(metadata, json);
        Assert.Equal(json, receipt.ResponseJson);
        Assert.NotEqual(ProcurementEvidenceEncoding.Digest(json.Trim()), receipt.ResponseSha256);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
    [InlineData(" aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    [InlineData("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa ")]
    [InlineData("aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa,aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    [InlineData(null)]
    public void Command_key_rejects_zero_or_noncanonical_representations(string? value)
    {
        Assert.False(PurchaseRequestDraftCommandReceipts.TryCommandKey(value, out var key));
        Assert.Equal(Guid.Empty, key);
    }

    [Theory]
    [InlineData("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    [InlineData("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaaa")]
    public void Command_key_does_not_invent_a_uuid_version_requirement(string value)
    {
        Assert.True(PurchaseRequestDraftCommandReceipts.TryCommandKey(value, out var key));
        Assert.Equal(value, key.ToString("D"));
    }

    [Fact]
    public void Fingerprint_reuses_lossless_canonical_codec_and_preserves_array_order()
    {
        var first = PurchaseRequestDraftCommandReceipts.Fingerprint("{\"b\":1.00,\"a\":[\"1\",\"2\"]}");
        Assert.Equal(first, PurchaseRequestDraftCommandReceipts.Fingerprint("{\"a\":[\"1\",\"2\"],\"b\":1}"));
        Assert.NotEqual(first, PurchaseRequestDraftCommandReceipts.Fingerprint("{\"a\":[\"2\",\"1\"],\"b\":1}"));
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest("{\"b\":1.00,\"a\":[\"1\",\"2\"]}"), first);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"a\":1,\"A\":2}")]
    [InlineData("{\"a\":0.12345678901234567890123456789123456789}")]
    public void Fingerprint_rejects_ambiguous_unsupported_or_lossy_json(string json)
        => Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Fingerprint(json));

    [Theory]
    [InlineData("operation")]
    [InlineData("actor")]
    [InlineData("company")]
    [InlineData("request")]
    [InlineData("key")]
    [InlineData("tenant")]
    [InlineData("scope-zero")]
    [InlineData("scope-mismatch")]
    [InlineData("fingerprint")]
    [InlineData("create-expected-version")]
    [InlineData("create-result-version")]
    [InlineData("completion")]
    public void Invalid_completed_metadata_cannot_be_recorded(string kind)
    {
        var original = Metadata();
        var invalid = kind switch
        {
            "operation" => original with { Operation = "create" },
            "actor" => original with { ActorPrincipalId = Guid.Empty },
            "company" => original with { LegalEntityId = Guid.Empty },
            "request" => original with { PurchaseRequestId = Guid.Empty },
            "key" => original with { CommandKey = Guid.Empty },
            "tenant" => original with { TenantId = " " },
            "scope-zero" => original with { ContextBranchRef = Guid.Empty },
            "scope-mismatch" => original with { ContextBranchRef = Other },
            "fingerprint" => original with { CommandFingerprint = new string('A', 64) },
            "create-expected-version" => original with { ExpectedVersion = 1 },
            "create-result-version" => original with { ResultVersion = 2 },
            "completion" => original with { CompletedAt = default },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(invalid, Result(original)));
    }

    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(1L, 1L)]
    [InlineData(1L, 3L)]
    [InlineData(long.MaxValue, long.MaxValue)]
    public void Update_requires_exact_positive_cas_increment_without_overflow(long expected, long result)
    {
        var metadata = Metadata("Update") with { ExpectedVersion = expected, ResultVersion = result };
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(metadata, Result(metadata)));
    }

    [Fact]
    public void Company_wide_update_retains_original_branch_and_department_without_widening_record()
    {
        var metadata = Metadata("Update") with { ContextBranchRef = null, ContextDepartmentRef = null };
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(metadata, Result(metadata));
        Assert.Null(receipt.ContextBranchRef);
        Assert.Equal(Branch, receipt.OriginalBranchRef);
        Assert.Equal(Department, receipt.OriginalDepartmentRef);
    }

    [Fact]
    public void Historic_null_principal_is_structural_compatibility_not_authorization_or_backfill()
    {
        var metadata = Metadata("Update");
        var json = Result(metadata).Replace(Actor.ToString("D"), "PLACEHOLDER")
            .Replace("\"PLACEHOLDER\"", "null");
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(metadata, json);
        Assert.Contains("\"requesterPrincipalId\":null", receipt.ResponseJson);
        // The evidence helper has no account/record authority. Actual scoped ownership
        // remains the existing rule, and null by itself never grants ownership.
        var request = new PurchaseRequest { RequesterPrincipalId = null, CreatedBy = Other.ToString("D") };
        Assert.False(PurchaseRequestOwnership.IsOwner(request, Actor));
        request.CreatedBy = Actor.ToString("D");
        Assert.True(PurchaseRequestOwnership.IsOwner(request, Actor));
        Assert.Null(request.RequesterPrincipalId);
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(Metadata(), json));
    }

    [Fact]
    public void Optional_historical_worker_link_is_preserved_without_becoming_receipt_authority()
    {
        var metadata = Metadata("Update");
        var values = Values(metadata);
        values["requesterWorkerId"] = Other;
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(metadata, JsonSerializer.Serialize(values));
        Assert.Equal(Actor, receipt.ActorPrincipalId);
        Assert.Contains(Other.ToString("D"), receipt.ResponseJson);
        // No receipt worker field, employee lookup or worker equality grants ownership.
        Assert.False(PurchaseRequestOwnership.IsOwner(new PurchaseRequest
            { RequesterWorkerId = Other, CreatedBy = Other.ToString("D") }, Actor));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("requesterPrincipalId")]
    [InlineData("branchRef")]
    [InlineData("departmentRef")]
    [InlineData("requesterWorkerId")]
    [InlineData("requestEditVersion")]
    [InlineData("status")]
    [InlineData("currencyCode")]
    public void Metadata_mismatch_or_unsupported_shape_is_not_an_original_result(string field)
    {
        var metadata = Metadata();
        var values = Values(metadata);
        values[field] = field switch
        {
            "version" => "1", "requestEditVersion" => 2, "status" => "Approved", "currencyCode" => "zmw",
            "requesterWorkerId" => Guid.Empty, _ => Other,
        };
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(metadata, JsonSerializer.Serialize(values)));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{invalid}")]
    public void Malformed_or_nonobject_result_is_rejected(string json)
        => Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(Metadata(), json));

    [Fact]
    public void Exact_and_case_alias_duplicate_properties_cannot_hide_original_metadata()
    {
        var metadata = Metadata();
        var json = Result(metadata);
        foreach (var suffix in new[] { ",\"id\":\"" + Request + "\"}", ",\"ID\":\"" + Request + "\"}",
            ",\"extra\":{\"x\":1,\"x\":2}}", ",\"extra\":{\"x\":1,\"X\":2}}" })
            Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.Complete(metadata, json[..^1] + suffix));
    }

    [Fact]
    public void Retained_result_hash_and_schema_are_checked_without_reserializing_json()
    {
        var receipt = PurchaseRequestDraftCommandReceipts.Complete(Metadata(), Result(Metadata()));
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.ValidateCompleted(
            Retained(receipt, receipt.ResponseJson + " ")));
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.ValidateCompleted(
            Retained(receipt, receipt.ResponseJson, responseSchema: 2)));
        Assert.Throws<InvalidOperationException>(() => PurchaseRequestDraftCommandReceipts.ValidateCompleted(
            Retained(receipt, receipt.ResponseJson, fingerprintSchema: 2)));
    }

    private static PurchaseRequestDraftCommandMetadata Metadata(string operation = "Create") => new(
        "tenant-a", Company, operation, Key, Actor, Request, Branch, Department, Branch, Department,
        operation == "Create" ? null : 1, operation == "Create" ? 1 : 2,
        PurchaseRequestDraftCommandReceipts.Fingerprint("{\"intentVersion\":1}"),
        new DateTimeOffset(2026, 10, 22, 15, 30, 0, TimeSpan.FromHours(2)).AddTicks(17));

    // This is intentionally a metadata fixture, not a fake full RequestDetail adapter.
    private static Dictionary<string, object?> Values(PurchaseRequestDraftCommandMetadata metadata, string? status = null) => new()
    {
        ["id"] = metadata.PurchaseRequestId, ["version"] = metadata.ResultVersion,
        ["requesterPrincipalId"] = metadata.ActorPrincipalId, ["requesterWorkerId"] = null,
        ["branchRef"] = metadata.OriginalBranchRef, ["departmentRef"] = metadata.OriginalDepartmentRef,
        ["status"] = status ?? (metadata.Operation == "Submit" ? "Submitted" : "Draft"),
        ["approvalState"] = metadata.Operation == "Submit" ? "PendingPolicyAndBudget" : "NotSubmitted",
        ["currencyCode"] = "ZMW", ["requestEditVersion"] = 1, ["notesToApprover"] = null,
    };
    private static string Result(PurchaseRequestDraftCommandMetadata metadata, string? status = null)
        => JsonSerializer.Serialize(Values(metadata, status));

    private static PurchaseRequestDraftCommandReceipt Retained(PurchaseRequestDraftCommandReceipt source,
        string json, int responseSchema = 1, int fingerprintSchema = 1) => new()
    {
        TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
        Operation = source.Operation, CommandKey = source.CommandKey, ActorPrincipalId = source.ActorPrincipalId,
        PurchaseRequestId = source.PurchaseRequestId, ContextBranchRef = source.ContextBranchRef,
        ContextDepartmentRef = source.ContextDepartmentRef, OriginalBranchRef = source.OriginalBranchRef,
        OriginalDepartmentRef = source.OriginalDepartmentRef, ExpectedVersion = source.ExpectedVersion,
        ResultVersion = source.ResultVersion, CommandFingerprint = source.CommandFingerprint,
        FingerprintSchemaVersion = fingerprintSchema, ResponseSchemaVersion = responseSchema,
        ResponseStatusCode = source.ResponseStatusCode, ResponseJson = json, ResponseSha256 = source.ResponseSha256,
        CompletedAt = source.CompletedAt,
    };
}
