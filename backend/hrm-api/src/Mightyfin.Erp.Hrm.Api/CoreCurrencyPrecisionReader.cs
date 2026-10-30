using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core-owned immutable projection; consumers map it into their own contracts.</summary>
public sealed record CoreCurrencyPrecisionSnapshot(
    string CurrencyCode, Guid ReferenceId, int Revision, int MinorUnits, string Rounding,
    string PayloadSha256, DateTimeOffset PublishedAtUtc, DateTimeOffset ObservedAtUtc);

/// <summary>
/// Reads Core precision on the caller's existing connection and transaction. The
/// caller owns authorization and must pass its authenticated tenant/company scope.
/// </summary>
public static class CoreCurrencyPrecisionReader
{
    /// <summary>Checks the selected Core branch in the same snapshot as the
    /// Procurement invoice and currency reference. This is a scoped existence
    /// check, not a replacement for the authenticated shell branch grant.</summary>
    public static async Task<bool> BranchAvailableForCompanyAsync(DbConnection connection,
        DbTransaction transaction, string tenantId, Guid companyId, Guid branchId,
        DateTimeOffset observedAtUtc, CancellationToken ct)
    {
        RequireBoundary(connection, transaction, tenantId, companyId, observedAtUtc);
        if (branchId == Guid.Empty) throw new InvalidOperationException("CoreCurrencyBranchBoundaryInvalid");
        await using var command = Command(connection, transaction,
            "SELECT EXISTS(SELECT 1 FROM hrm.work_locations WHERE tenant_id=@tenant " +
            "AND legal_entity_id=@company AND id=@branch AND NOT is_archived)",
            ("tenant", tenantId), ("company", companyId), ("branch", branchId));
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct)) return false;
        var available = row.GetBoolean(0);
        if (await row.ReadAsync(ct)) throw new InvalidOperationException("CoreCurrencyBranchAmbiguous");
        return available;
    }

    public static async Task<CoreCurrencyPrecisionSnapshot> ResolveForCompanyAsync(
        DbConnection connection, DbTransaction transaction, string tenantId, Guid companyId,
        string? requestedCurrencyCode, DateTimeOffset observedAtUtc, CancellationToken ct)
    {
        RequireBoundary(connection, transaction, tenantId, companyId, observedAtUtc);
        await using var company = Command(connection, transaction,
            "SELECT currency FROM hrm.legal_entities WHERE tenant_id=@tenant AND id=@company AND NOT is_archived",
            ("tenant", tenantId), ("company", companyId));
        var stored = await company.ExecuteScalarAsync(ct) as string;
        if (!Canonical(stored)) throw new InvalidOperationException("CoreCurrencyCompanyUnavailable");
        var code = string.IsNullOrWhiteSpace(requestedCurrencyCode) ? stored! : requestedCurrencyCode;
        if (!Canonical(code)) throw new InvalidOperationException("CoreCurrencyCodeInvalid");
        return await ReadCurrentAsync(connection, transaction, tenantId, code, observedAtUtc, ct);
    }

    public static async Task<CoreCurrencyPrecisionSnapshot> ReadHistoricalAsync(
        DbConnection connection, DbTransaction transaction, string tenantId, Guid referenceId,
        DateTimeOffset observedAtUtc, CancellationToken ct)
    {
        RequireBoundary(connection, transaction, tenantId, referenceId, observedAtUtc);
        await using var command = Command(connection, transaction,
            "SELECT id,currency_code,revision,minor_units,rounding,payload_sha256,published_at_utc," +
            "source_reference,publication_reason " +
            "FROM hrm.currency_precision_versions WHERE tenant_id=@tenant AND id=@id AND NOT is_archived",
            ("tenant", tenantId), ("id", referenceId));
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct)) throw new InvalidOperationException("CoreCurrencyReferenceUnavailable");
        var snapshot = Snapshot(row, observedAtUtc);
        if (snapshot.PublishedAtUtc > observedAtUtc)
            throw new InvalidOperationException("CoreCurrencyReferenceNotYetPublished");
        if (await row.ReadAsync(ct)) throw new InvalidOperationException("CoreCurrencyReferenceAmbiguous");
        return snapshot;
    }

    private static async Task<CoreCurrencyPrecisionSnapshot> ReadCurrentAsync(
        DbConnection connection, DbTransaction transaction, string tenantId, string code,
        DateTimeOffset observedAtUtc, CancellationToken ct)
    {
        await using var command = Command(connection, transaction,
            "SELECT id,currency_code,revision,minor_units,rounding,payload_sha256,published_at_utc," +
            "source_reference,publication_reason,retired_at_utc " +
            "FROM hrm.currency_precision_versions WHERE tenant_id=@tenant AND currency_code=@code " +
            "AND published_at_utc<=@observed AND NOT is_archived ORDER BY revision DESC LIMIT 2",
            ("tenant", tenantId), ("code", code), ("observed", observedAtUtc));
        await using var row = await command.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct)) throw new InvalidOperationException("CoreCurrencyReferenceUnavailable");
        var snapshot = Snapshot(row, observedAtUtc);
        var retired = !row.IsDBNull(9) && row.GetFieldValue<DateTimeOffset>(9) <= observedAtUtc;
        if (retired) throw new InvalidOperationException("CoreCurrencyReferenceRetired");
        if (await row.ReadAsync(ct) && row.GetInt32(2) == snapshot.Revision)
            throw new InvalidOperationException("CoreCurrencyReferenceAmbiguous");
        return snapshot;
    }

    private static CoreCurrencyPrecisionSnapshot Snapshot(DbDataReader row, DateTimeOffset observed)
    {
        var result = new CoreCurrencyPrecisionSnapshot(row.GetString(1), row.GetGuid(0), row.GetInt32(2),
            row.GetInt32(3), row.GetString(4), row.GetString(5), row.GetFieldValue<DateTimeOffset>(6), observed);
        if (!Canonical(result.CurrencyCode) || result.Revision < 1 || result.MinorUnits is < 0 or > 4 ||
            result.Rounding is not ("ToEven" or "AwayFromZero") || !Hex64(result.PayloadSha256))
            throw new InvalidOperationException("CoreCurrencyReferenceInvalid");
        var payload = JsonSerializer.Serialize(new { result.CurrencyCode, result.Revision,
            result.MinorUnits, result.Rounding, SourceReference = row.GetString(7),
            PublicationReason = row.GetString(8), result.PublishedAtUtc },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if (digest != result.PayloadSha256)
            throw new InvalidOperationException("CoreCurrencyReferenceDigestInvalid");
        return result;
    }

    private static bool Canonical(string? code) => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');
    private static bool Hex64(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void RequireBoundary(DbConnection connection, DbTransaction transaction,
        string tenantId, Guid identity, DateTimeOffset observedAtUtc)
    {
        if (connection.State != ConnectionState.Open || transaction.Connection != connection ||
            transaction.IsolationLevel != IsolationLevel.Serializable ||
            string.IsNullOrWhiteSpace(tenantId) || identity == Guid.Empty ||
            observedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("CoreCurrencyReadBoundaryInvalid");
    }

    private static DbCommand Command(DbConnection connection, DbTransaction transaction,
        string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 10;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
