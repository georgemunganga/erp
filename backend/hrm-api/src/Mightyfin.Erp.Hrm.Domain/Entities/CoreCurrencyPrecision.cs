namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Core's published calculation reference. Only retirement metadata may change.</summary>
public sealed class CoreCurrencyPrecisionVersion : Entity
{
    public string CurrencyCode { get; set; } = null!;
    public int Revision { get; set; }
    public int MinorUnits { get; set; }
    public string Rounding { get; set; } = null!;
    public string SourceReference { get; set; } = null!;
    public string PublicationReason { get; set; } = null!;
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset? RetiredAtUtc { get; set; }
    public string? RetirementReason { get; set; }
    public string PayloadSha256 { get; set; } = null!;
}

/// <summary>Original Core configuration command result, retained byte for byte.</summary>
public sealed class CoreCurrencyPrecisionCommandReceipt : Entity
{
    public string Operation { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public Guid ActorPrincipalId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VersionId { get; set; }
    public string RequestSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public string ResponseSha256 { get; set; } = null!;
    public int ResponseStatus { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}
