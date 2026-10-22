using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Primitives;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Pure host intent contract tests. No authentication, business validation,
/// database/default/catalog read, receipt, route, migration or transaction proof.</summary>
public sealed class PurchaseRequestDraftCommandIntentTests
{
    private static readonly Guid Actor=Guid.Parse("11111111-2222-4333-8444-555555555555"),Company=Guid.Parse("22222222-3333-4444-8555-666666666666"),
        Branch=Guid.Parse("33333333-4444-4555-8666-777777777777"),Target=Guid.Parse("44444444-5555-4666-8777-888888888888"),
        Key=Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static ProcurementRequestScope Scope(Guid? actor=null,Guid? company=null,Guid? branch=null,Guid? department=null,Guid? alias=null)
    {
        var id=actor??Actor;var scope=new ProcurementRequestScope();scope.Set("intent-tenant",company??Company,id.ToString("D"),branch,department,false,"intent-tests");
        scope.AssignPrincipal(id,alias);return scope;
    }
    private static JsonElement Element(JsonNode node)=>JsonSerializer.SerializeToElement(node);
    private static JsonElement Raw(string json){using var document=JsonDocument.Parse(json);return document.RootElement.Clone();}
    private static JsonObject Body()=>JsonNode.Parse("""
        {"purpose":"  Original purpose  ","currencyCode":" zmw ","neededBy":"2030-01-02","costCenterRef":" HEAD_CC ",
        "projectRef":" HEAD_PROJECT ","deliveryLocationRef":" SITE ","deliveryAddressText":" ADDRESS ",
        "fundingSourceRef":" SOURCE ","notesToApprover":" NOTES ","lines":[{
        "id":null,"description":" Original line ","unitCode":" each ","requestedQuantity":"1.0000","estimatedUnitPrice":"5.1234",
        "catalogItemId":null,"catalogEntryId":null,"preferredSupplierId":null,"categoryCode":" goods ","purchaseType":" normal ",
        "neededBy":"2030-01-03","discountPercent":null,"taxCodeRef":" TAX ","costCenterRef":" LINE_CC ","projectRef":" LINE_PROJECT ","specificationDocumentRefId":null}]}
        """)!.AsObject();
    private static string CreateFingerprint(JsonNode body,ProcurementRequestScope? scope=null)=>PurchaseRequestDraftCommandIntents.Create(scope??Scope(),Key.ToString("D"),Element(body)).Fingerprint;
    private static EncodedPurchaseRequestDraftCommand<ProcurementRequestRoutes.UpdateRequest> Update(JsonObject body,ProcurementRequestScope? scope=null)
    {body["version"]=5;return PurchaseRequestDraftCommandIntents.Update(scope??Scope(),Target,Key.ToString("D"),Element(body));}

    [Fact]
    public void Allowlist_is_exact_actual_existing_DTO_contract_and_never_accepts_server_catalog_version()
    {
        static string[] Names(Type type)=>type.GetProperties(BindingFlags.Instance|BindingFlags.Public)
            .Select(x=>JsonNamingPolicy.CamelCase.ConvertName(x.Name)).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(Names(typeof(ProcurementRequestRoutes.NewRequest)),PurchaseRequestDraftCommandIntents.CreateFields.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));
        Assert.Equal(Names(typeof(ProcurementRequestRoutes.UpdateRequest)),PurchaseRequestDraftCommandIntents.UpdateFields.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));
        Assert.Equal(Names(typeof(ProcurementRequestRoutes.RequestLineInput)),PurchaseRequestDraftCommandIntents.LineFields.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));
        Assert.Equal(Names(typeof(ProcurementRequestRoutes.VersionCommand)),PurchaseRequestDraftCommandIntents.SubmitFields.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain("catalogPublishedVersion",PurchaseRequestDraftCommandIntents.LineFields);
    }
    [Theory]
    [InlineData("purpose")][InlineData("currencyCode")][InlineData("neededBy")][InlineData("costCenterRef")][InlineData("projectRef")]
    [InlineData("deliveryLocationRef")][InlineData("deliveryAddressText")][InlineData("fundingSourceRef")][InlineData("notesToApprover")]
    public void Every_accepted_header_field_changes_intent(string field)
    {
        var body=Body();var original=CreateFingerprint(body);
        body[field]=field switch {"neededBy"=>"2030-02-03","currencyCode"=>"USD",_=>body[field]!.GetValue<string>()+" changed"};
        Assert.NotEqual(original,CreateFingerprint(body));
    }
    [Theory]
    [InlineData("id")][InlineData("description")][InlineData("unitCode")][InlineData("requestedQuantity")][InlineData("estimatedUnitPrice")]
    [InlineData("catalogItemId")][InlineData("catalogEntryId")][InlineData("preferredSupplierId")][InlineData("categoryCode")]
    [InlineData("purchaseType")][InlineData("neededBy")][InlineData("discountPercent")][InlineData("taxCodeRef")]
    [InlineData("costCenterRef")][InlineData("projectRef")][InlineData("specificationDocumentRefId")]
    public void Every_accepted_line_field_changes_intent_without_generated_catalog_or_classification_lookup(string field)
    {
        var body=Body();var original=Update(body).Fingerprint;var line=body["lines"]![0]!.AsObject();
        line[field]=field switch {"id" or "catalogItemId" or "catalogEntryId" or "preferredSupplierId" or "specificationDocumentRefId"=>Target.ToString("D"),
            "requestedQuantity"=>"2","estimatedUnitPrice"=>"6.1234","discountPercent"=>"0.0000","neededBy"=>"2030-02-03",
            "categoryCode"=>"OTHER","purchaseType"=>"OTHER",_=>line[field]!.GetValue<string>()+"changed"};
        Assert.NotEqual(original,Update(body).Fingerprint);
    }
    [Fact]
    public void Original_body_is_not_rewritten_existing_DTO_and_all_exact_canonical_fields_are_retained()
    {
        var body=Body();var raw=body.ToJsonString();var encoded=PurchaseRequestDraftCommandIntents.Create(Scope(branch:Branch),Key.ToString("D"),Element(body));
        Assert.IsType<ProcurementRequestRoutes.NewRequest>(encoded.AcceptedInput);Assert.Equal(raw,body.ToJsonString());
        Assert.Equal(Key,encoded.CommandKey);Assert.Equal(1,encoded.Intent.FingerprintSchemaVersion);Assert.Equal("Create",encoded.Intent.Operation);
        Assert.Null(encoded.Intent.TargetId);Assert.Null(encoded.Intent.ExpectedVersion);Assert.Equal(Actor,encoded.Intent.ActorPrincipalId);
        Assert.Equal(Company,encoded.Intent.LegalEntityId);Assert.Equal(Branch,encoded.Intent.ContextBranchRef);
        Assert.Equal("Original purpose",encoded.Intent.Input.GetProperty("purpose").GetString());
        var line=encoded.Intent.Input.GetProperty("lines")[0];Assert.Equal("Original line",line.GetProperty("description").GetString());
        Assert.Equal("each",line.GetProperty("unitCode").GetString());Assert.Equal("GOODS",line.GetProperty("categoryCode").GetString());
        Assert.Equal("NORMAL",line.GetProperty("purchaseType").GetString());Assert.Equal("1",line.GetProperty("requestedQuantity").GetString());
        Assert.Equal("5.1234",line.GetProperty("estimatedUnitPrice").GetString());Assert.Equal("2030-01-03",line.GetProperty("neededBy").GetString());
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest(encoded.IntentJson),encoded.Fingerprint);
    }
    [Fact]
    public void Currency_omission_is_stable_org_default_for_Create_and_retain_request_for_Update_without_loading_either()
    {
        var missing=Body();missing.Remove("currencyCode");var nullable=Body();nullable["currencyCode"]=null;var blank=Body();blank["currencyCode"]="   ";
        Assert.Equal(CreateFingerprint(missing),CreateFingerprint(nullable));Assert.Equal(CreateFingerprint(missing),CreateFingerprint(blank));
        var create=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Element(missing));
        Assert.Equal("OrganizationDefault",create.Intent.Input.GetProperty("currency").GetProperty("mode").GetString());
        Assert.Equal(Update(missing).Fingerprint,Update(nullable).Fingerprint);Assert.Equal(Update(missing).Fingerprint,Update(blank).Fingerprint);
        Assert.Equal("RetainRequestCurrency",Update(missing).Intent.Input.GetProperty("currency").GetProperty("mode").GetString());
        Assert.NotEqual(create.Fingerprint,CreateFingerprint(Body()));Assert.NotEqual(Update(missing).Fingerprint,Update(Body()).Fingerprint);
        var explicitCode=Body();explicitCode["currencyCode"]="ZMW";Assert.Equal(CreateFingerprint(Body()),CreateFingerprint(explicitCode));
    }
    [Fact]
    public void Optional_missing_null_and_clean_blank_are_equivalent_but_null_discount_is_not_zero_and_line_order_is_significant()
    {
        var missing=Body();missing.Remove("notesToApprover");var nullable=Body();nullable["notesToApprover"]=null;var blank=Body();blank["notesToApprover"]="  ";
        Assert.Equal(CreateFingerprint(missing),CreateFingerprint(nullable));Assert.Equal(CreateFingerprint(nullable),CreateFingerprint(blank));
        var zero=Body();zero["lines"]![0]!["discountPercent"]="0.0000";Assert.NotEqual(CreateFingerprint(Body()),CreateFingerprint(zero));
        var ordered=Body();var other=ordered["lines"]![0]!.DeepClone();other["description"]="Other line";ordered["lines"]!.AsArray().Add(other);
        var reversed=ordered.DeepClone().AsObject();var array=reversed["lines"]!.AsArray();var first=array[0];array.RemoveAt(0);array.Add(first);
        Assert.NotEqual(CreateFingerprint(ordered),CreateFingerprint(reversed));
    }
    [Fact]
    public void Numeric_and_string_decimal_tokens_canonicalize_exactly_and_never_round_wide_originals()
    {
        var raw=Body().ToJsonString();
        raw=raw.Replace("\"5.1234\"","\"99999999999999.9999\"");
        var strings=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw(raw));
        var numbers=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw(raw.Replace("\"99999999999999.9999\"","99999999999999.9999").Replace("\"1.0000\"","1")));
        Assert.Equal(strings.Fingerprint,numbers.Fingerprint);
        Assert.Equal("99999999999999.9999",strings.Intent.Input.GetProperty("lines")[0].GetProperty("estimatedUnitPrice").GetString());
    }
    [Fact]
    public void Scope_actor_target_expected_version_are_bound_but_key_and_optional_employee_alias_are_not_substitute_input()
    {
        var body=Body();var original=CreateFingerprint(body);
        Assert.NotEqual(original,CreateFingerprint(body,Scope(actor:Target)));Assert.NotEqual(original,CreateFingerprint(body,Scope(company:Target)));
        Assert.NotEqual(original,CreateFingerprint(body,Scope(branch:Branch)));Assert.NotEqual(original,CreateFingerprint(body,Scope(department:Target)));
        Assert.Equal(original,CreateFingerprint(body,Scope(alias:Target)));
        var one=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Element(body));
        var two=PurchaseRequestDraftCommandIntents.Create(Scope(),Guid.NewGuid().ToString("D"),Element(body));Assert.Equal(one.Fingerprint,two.Fingerprint);
        var update=Update(body);body["version"]=6;
        Assert.NotEqual(update.Fingerprint,PurchaseRequestDraftCommandIntents.Update(Scope(),Target,Key.ToString("D"),Element(body)).Fingerprint);
        body["version"]=5;Assert.NotEqual(update.Fingerprint,PurchaseRequestDraftCommandIntents.Update(Scope(),Company,Key.ToString("D"),Element(body)).Fingerprint);
        var submitted=PurchaseRequestDraftCommandIntents.Submit(Scope(),Target,Key.ToString("D"),Raw("{\"version\":5}"));
        Assert.IsType<ProcurementRequestRoutes.VersionCommand>(submitted.AcceptedInput);Assert.Equal(5,submitted.Intent.ExpectedVersion);
        Assert.NotEqual(update.Fingerprint,submitted.Fingerprint);
    }
    [Theory]
    [InlineData("{\"purpose\":\"a\",\"purpose\":\"b\"}")]
    [InlineData("{\"purpose\":\"a\",\"Purpose\":\"b\"}")]
    [InlineData("{\"lines\":[{\"id\":null,\"ID\":null}]}")]
    [InlineData("{\"lines\":[{\"requestedQuantity\":1,\"requestedQuantity\":2}]}")]
    public void Duplicate_and_case_alias_collisions_are_rejected_before_actual_deserialization(string json)
        =>Assert.Equal("RequestDraftCommandDuplicateField",Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw(json))).Message);
    [Theory]
    [InlineData("{\"id\":null}")][InlineData("{\"status\":\"Draft\"}")][InlineData("{\"branchRef\":null}")]
    [InlineData("{\"requesterPrincipalId\":null}")][InlineData("{\"version\":1}")]
    [InlineData("{\"lines\":[{\"catalogPublishedVersion\":1}]}")][InlineData("{\"lines\":[{\"approvedQuantity\":1}]}")]
    public void Server_derived_or_unknown_fields_are_not_accepted_as_mutable_intent(string json)
        =>Assert.Equal("RequestDraftCommandUnknownField",Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw(json))).Message);
    [Fact]
    public void Single_alternate_field_case_remains_compatible_with_current_Web_DTO_but_Submit_has_only_version()
    {
        var body=Body();var upper=JsonNode.Parse(body.ToJsonString().Replace("\"purpose\"","\"Purpose\"").Replace("\"estimatedUnitPrice\"","\"EstimatedUnitPrice\""))!;
        Assert.Equal(CreateFingerprint(body),CreateFingerprint(upper));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Submit(Scope(),Target,Key.ToString("D"),Raw("{\"version\":1,\"purpose\":\"ignored?\"}")));
    }
    [Theory]
    [InlineData("")][InlineData("00000000-0000-0000-0000-000000000000")][InlineData("AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE")]
    [InlineData(" aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee")][InlineData("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee ")]
    [InlineData("aaaaaaaabbbb4ccc8dddeeeeeeeeeeee")][InlineData("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee,aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee")]
    public void Key_requires_one_canonical_nonzero_lowercase_D_value(string key)
        =>Assert.Equal("RequestDraftCommandKeyInvalid",Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),key,Element(Body()))).Message);
    [Fact]
    public void Missing_or_multiple_keys_missing_principal_unresolved_scope_and_zero_target_fail_closed()
    {
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),StringValues.Empty,Element(Body())));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),new StringValues(new[]{Key.ToString("D"),Key.ToString("D")}),Element(Body())));
        var missing=new ProcurementRequestScope();missing.Set("intent-tenant",Company,Actor.ToString("D"),null,null,false,"test");
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(missing,Key.ToString("D"),Element(Body())));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(new ProcurementRequestScope(),Key.ToString("D"),Element(Body())));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Update(Scope(),Guid.Empty,Key.ToString("D"),Raw("{\"version\":1}")));
    }
    [Theory]
    [InlineData("{\"lines\":[{\"requestedQuantity\":0.00001}]}")]
    [InlineData("{\"lines\":[{\"estimatedUnitPrice\":\"100000000000000\"}]}")]
    [InlineData("{\"lines\":[{\"discountPercent\":\"0.00001\"}]}")]
    [InlineData("{\"neededBy\":\"not-date\"}")][InlineData("{\"lines\":[null]}")][InlineData("{\"lines\":{}}")]
    public void Existing_exact_decimal_date_and_object_shape_contract_rejects_structural_bad_input(string json)
        =>Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw(json)));
    [Fact]
    public void No_new_note_cap_or_second_business_validator_is_introduced()
    {
        var body=Body();body["notesToApprover"]=new string('x',1_048_577);
        var encoded=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Element(body));
        Assert.Equal(1_048_577,encoded.Intent.Input.GetProperty("notesToApprover").GetString()!.Length);
        // Still delegated to existing owning Validate/ValidateReferences on a NEW
        // execution: this structurally parseable body is NOT business-valid.
        var invalidBusiness=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Raw("{\"purpose\":null,\"currencyCode\":\"ZZ\",\"lines\":[]}"));
        Assert.Empty(invalidBusiness.AcceptedInput.Lines!);
    }
}
