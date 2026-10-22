using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Host-owned structural input/intent adapter only. It does not authenticate,
/// validate business rules, query defaults/catalogs, acquire locks or execute a command.
/// No routes/capabilities/registration use it until the recovery adapter is accepted.</summary>
internal static class PurchaseRequestDraftCommandIntents
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal static readonly FrozenSet<string> CreateFields=new[]{"purpose","currencyCode","neededBy","costCenterRef","projectRef",
        "deliveryLocationRef","deliveryAddressText","fundingSourceRef","notesToApprover","lines"}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    internal static readonly FrozenSet<string> UpdateFields=CreateFields.Append("version").ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    internal static readonly FrozenSet<string> SubmitFields=new[]{"version"}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    internal static readonly FrozenSet<string> LineFields=new[]{"id","description","unitCode","requestedQuantity","estimatedUnitPrice",
        "catalogItemId","catalogEntryId","preferredSupplierId","categoryCode","purchaseType","neededBy","discountPercent",
        "taxCodeRef","costCenterRef","projectRef","specificationDocumentRefId"}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static EncodedPurchaseRequestDraftCommand<ProcurementRequestRoutes.NewRequest> Create(
        ProcurementRequestScope scope,StringValues keys,JsonElement body)
    {
        var key=Key(keys);ValidateShape(body,CreateFields);
        var input=Read<ProcurementRequestRoutes.NewRequest>(body);
        return Encode(scope,key,"Create",null,null,input,Normalize(input,"OrganizationDefault"));
    }
    internal static EncodedPurchaseRequestDraftCommand<ProcurementRequestRoutes.UpdateRequest> Update(
        ProcurementRequestScope scope,Guid targetId,StringValues keys,JsonElement body)
    {
        var key=Key(keys);Require(targetId!=Guid.Empty,"RequestDraftCommandTargetInvalid");ValidateShape(body,UpdateFields);
        var input=Read<ProcurementRequestRoutes.UpdateRequest>(body);
        return Encode(scope,key,"Update",targetId,input.Version,input,Normalize(input,"RetainRequestCurrency"));
    }
    internal static EncodedPurchaseRequestDraftCommand<ProcurementRequestRoutes.VersionCommand> Submit(
        ProcurementRequestScope scope,Guid targetId,StringValues keys,JsonElement body)
    {
        var key=Key(keys);Require(targetId!=Guid.Empty,"RequestDraftCommandTargetInvalid");ValidateObject(body,SubmitFields);
        var input=Read<ProcurementRequestRoutes.VersionCommand>(body);
        return Encode(scope,key,"Submit",targetId,input.Version,input,new {version=input.Version});
    }
    private static Guid Key(StringValues values)
    {
        if(values.Count!=1||!PurchaseRequestDraftCommandReceipts.TryCommandKey(values[0],out var key))
            throw new InvalidOperationException("RequestDraftCommandKeyInvalid");
        return key;
    }
    private static T Read<T>(JsonElement body) where T:class
    {
        // Reuse the actual route DTO/converters, including18,4 and nullable7,4.
        var parsed=ProcurementOriginalValueInput.Read<T>(body);
        if(parsed.Error is not null||parsed.Value is null)throw new InvalidOperationException("RequestDraftCommandInputInvalid");
        return parsed.Value;
    }
    private static void ValidateObject(JsonElement value,IReadOnlySet<string> allowed)
    {
        Require(value.ValueKind==JsonValueKind.Object,"RequestDraftCommandObjectRequired");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var property in value.EnumerateObject())
        {
            Require(seen.Add(property.Name),"RequestDraftCommandDuplicateField");
            Require(allowed.Contains(property.Name),"RequestDraftCommandUnknownField");
        }
    }
    private static void ValidateShape(JsonElement body,IReadOnlySet<string> allowed)
    {
        ValidateObject(body,allowed);
        foreach(var property in body.EnumerateObject().Where(x=>x.Name.Equals("lines",StringComparison.OrdinalIgnoreCase)))
        {
            // Missing/null lines preserve the existing DTO's meaning; existing
            // owning Validate remains responsible for required1–100 business lines.
            if(property.Value.ValueKind==JsonValueKind.Null)continue;
            Require(property.Value.ValueKind==JsonValueKind.Array,"RequestDraftCommandLinesShapeInvalid");
            foreach(var line in property.Value.EnumerateArray())ValidateObject(line,LineFields);
        }
    }
    private static object Normalize(ProcurementRequestRoutes.NewRequest input,string omittedCurrencyMode)
    {
        // Same current route Trim/Clean rules; no company/current-record lookup.
        // Omitted/default intent is stable even if settings change before replay.
        var currency=string.IsNullOrWhiteSpace(input.CurrencyCode)
            ?new {mode=omittedCurrencyMode,code=(string?)null}
            :new {mode="Explicit",code=(string?)input.CurrencyCode.Trim().ToUpperInvariant()};
        try
        {
            return new {purpose=input.Purpose?.Trim(),currency,neededBy=Date(input.NeededBy),
                costCenterRef=Clean(input.CostCenterRef),projectRef=Clean(input.ProjectRef),deliveryLocationRef=Clean(input.DeliveryLocationRef),
                deliveryAddressText=Clean(input.DeliveryAddressText),fundingSourceRef=Clean(input.FundingSourceRef),notesToApprover=Clean(input.NotesToApprover),
                lines=input.Lines?.Select(line=>new {id=Id(line.Id),description=line.Description?.Trim(),unitCode=line.UnitCode?.Trim(),
                    requestedQuantity=ProcurementEvidenceEncoding.Decimal(line.RequestedQuantity),estimatedUnitPrice=ProcurementEvidenceEncoding.Decimal(line.EstimatedUnitPrice),
                    catalogItemId=Id(line.CatalogItemId),catalogEntryId=Id(line.CatalogEntryId),preferredSupplierId=Id(line.PreferredSupplierId),
                    categoryCode=ProcurementPolicyCode.NormalizeOptional(line.CategoryCode),purchaseType=ProcurementPolicyCode.NormalizeOptional(line.PurchaseType),
                    neededBy=Date(line.NeededBy),discountPercent=line.DiscountPercent.HasValue?ProcurementEvidenceEncoding.Decimal(line.DiscountPercent.Value):null,
                    taxCodeRef=Clean(line.TaxCodeRef),costCenterRef=Clean(line.CostCenterRef),projectRef=Clean(line.ProjectRef),
                    specificationDocumentRefId=Id(line.SpecificationDocumentRefId)}).ToArray()};
        }
        catch(ArgumentException){throw new InvalidOperationException("RequestDraftCommandInputInvalid");}
    }
    private static EncodedPurchaseRequestDraftCommand<T> Encode<T>(ProcurementRequestScope scope,Guid key,string operation,
        Guid? target,long? expected,T input,object normalized) where T:class
    {
        Require(!string.IsNullOrWhiteSpace(scope.TenantId)&&scope.LegalEntityId!=Guid.Empty
            &&scope.PrincipalId is {} principal&&principal!=Guid.Empty&&ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            &&scope.SubjectId==principal.ToString("D")&&scope.WorkLocationId!=Guid.Empty&&scope.OrgUnitId!=Guid.Empty
            &&(!scope.IsConfined||scope.WorkLocationId is not null),"RequestDraftCommandScopeInvalid");
        var intent=new PurchaseRequestDraftCommandIntentV1(PurchaseRequestDraftCommandReceipts.FingerprintSchemaVersion,operation,
            scope.TenantId,scope.LegalEntityId,scope.PrincipalId!.Value,scope.WorkLocationId,scope.OrgUnitId,target,expected,
            JsonSerializer.SerializeToElement(normalized,Json));
        var encoded=JsonSerializer.Serialize(intent,Json);
        return new(key,input,intent,encoded,PurchaseRequestDraftCommandReceipts.Fingerprint(encoded));
    }
    private static string? Id(Guid? value)=>value?.ToString("D");
    private static string? Date(DateOnly? value)=>value?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static void Require(bool value,string code){if(!value)throw new InvalidOperationException(code);}
}

// Versioned HASHING DTO only, never an additional public request body/master.
internal sealed record PurchaseRequestDraftCommandIntentV1(int FingerprintSchemaVersion,string Operation,string TenantId,
    Guid LegalEntityId,Guid ActorPrincipalId,Guid? ContextBranchRef,Guid? ContextDepartmentRef,Guid? TargetId,long? ExpectedVersion,JsonElement Input);
internal sealed record EncodedPurchaseRequestDraftCommand<T>(Guid CommandKey,T AcceptedInput,PurchaseRequestDraftCommandIntentV1 Intent,
    string IntentJson,string Fingerprint) where T:class;
