using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Independent owning-store READ and RECORD profiles. A read grant can
/// recover history after capture is switched off; it never implies INSERT rights.
/// Native constants may only be implemented after genuine scaffold/Up capture.</summary>
public sealed class ProcurementManualQuotationOptions
{
    // Retained READ/recovery depend on current permission and native admission,
    // not these new-work switches. A new release cannot inherit M4's write flag.
    public bool RecordEnabled { get; set; }
    public bool CaptureEnabled { get; set; }
}

public interface IProcurementManualQuotationReadiness
{
    // A denial-only startup short circuit. Even a captured contract never
    // substitutes for same-transaction role/schema inspection below.
    bool HasCapturedContract => false;
    Task<bool> CanReadAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}

public sealed class UnavailableProcurementManualQuotationReadiness : IProcurementManualQuotationReadiness
{
    public Task<bool> CanReadAsync(ProcurementDbContext owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); return Task.FromResult(false);
    }
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); return Task.FromResult(false);
    }
}
