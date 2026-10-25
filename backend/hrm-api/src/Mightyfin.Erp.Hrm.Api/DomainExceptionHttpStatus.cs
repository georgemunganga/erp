using Microsoft.AspNetCore.Http;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>The HTTP status mapping used by the application's DomainException handler.</summary>
public static class DomainExceptionHttpStatus
{
    public static int For(string code) => code switch
    {
        "forbidden" => StatusCodes.Status403Forbidden,
        "reset-not-confirmed" => StatusCodes.Status400BadRequest,
        "setup-maintenance-required" => StatusCodes.Status503ServiceUnavailable,
        "unauthorized" => StatusCodes.Status401Unauthorized,
        "not-found" or "worker-not-found" or "payroll-run-not-found" or "candidate-not-found" or "pay-period-not-found" or "letter-not-found" or "hr-request-not-found" => StatusCodes.Status404NotFound,
        "conflict" or "employee-no-exists" or "run-already-exists" or "movement-not-allowed" or "offboarding-blocked" => StatusCodes.Status409Conflict,
        "legal-entity-code-taken" or "location-code-taken" or "org-unit-code-taken" or "leave-type-code-taken"
            or "unit-close-backdated" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
