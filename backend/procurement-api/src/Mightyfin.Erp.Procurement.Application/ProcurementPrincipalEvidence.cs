namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Structural identity checks only. Current Core accounts, permissions and scope
/// must still be resolved by the owning server; these helpers confer no authority.</summary>
public static class ProcurementPrincipalEvidence
{
    public static bool Canonical(string? principal) => Guid.TryParseExact(principal, "D", out var id)
        && id != Guid.Empty && principal == id.ToString("D");
    public static bool OptionalWorker(Guid? worker) => worker != Guid.Empty;
    public static bool Independent(string first, Guid? firstWorker, string second, Guid? secondWorker) =>
        Canonical(first) && Canonical(second) && first != second && OptionalWorker(firstWorker) && OptionalWorker(secondWorker)
        && (firstWorker is null || secondWorker is null || firstWorker != secondWorker);
    public static bool WorkerBinding(int schema, Guid? retained, Guid? current) => schema switch
    {
        1 => retained is not null && retained != Guid.Empty && retained == current,
        2 => OptionalWorker(retained) && OptionalWorker(current),
        _ => false,
    };
}
