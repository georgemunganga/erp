using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Minimal API first buffers raw JSON losslessly, before catalog locks. Convert
/// existing typed contracts with their exact-decimal attributes before any domain mutation.</summary>
internal static class ProcurementOriginalValueInput
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal static (T? Value,IResult? Error) Read<T>(JsonElement body) where T:class
    {
        try
        {
            var value=body.Deserialize<T>(Json);
            if(value is not null) return(value,null);
        }
        catch(JsonException) { }
        return(null,Results.UnprocessableEntity(new { code="validation-failed",
            message="Enter valid details and exact amounts within their allowed range: up to four decimal places, or six for item unit conversions. No values were rounded or saved." }));
    }
}
