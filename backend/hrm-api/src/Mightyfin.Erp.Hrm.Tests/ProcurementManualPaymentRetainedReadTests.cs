using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Production-used retained query/activation and finite SQL contracts.
/// No PostgreSQL, cookies or positive physical readiness is asserted by these cases.</summary>
public sealed class ProcurementManualPaymentRetainedReadTests
{
    private const string Tenant = "payment-read-test";
    private static readonly Guid Company = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Principal = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Key = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid RequestId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private static ProcurementRequestScope Scope(Guid? branch = null, Guid? company = null,
        bool all = false, Guid? subject = null)
    {
        var actor = subject ?? Principal;
        var scope = new ProcurementRequestScope();
        scope.Set(Tenant, company ?? Company, actor.ToString("D"), all ? null : branch ?? Branch,
            null, false, "retained-read-test", explicitAllBranches: all);
        scope.AssignPrincipal(actor);
        return scope;
    }
    private static ProcurementPrincipalPeople.Person Reader(bool read = true) => new(Principal.ToString("D"),
        null, read ? new HashSet<string> { "procurement-payment-read" } : new HashSet<string>(),
        false, new HashSet<Guid> { Branch });
    private static ManualPaymentRequest Request() => new() { Id = RequestId, TenantId = Tenant,
        LegalEntityId = Company, BranchId = Branch, CreatorPrincipalId = Principal.ToString("D"),
        Status = "ConfirmedManually", CurrencyCode = "ZMW", Amount = 10m };
    private static ManualPaymentCommandReceipt Receipt() => new() { Id = Guid.NewGuid(), TenantId = Tenant,
        LegalEntityId = Company, BranchId = Branch, ManualPaymentRequestId = RequestId,
        CommandKey = Key, ActorPrincipalId = Principal.ToString("D"), Action = "Create", ResultVersion = 1,
        ResponseJson = "{\"status\":\"Draft\"}" };
    private static ProcurementManualPaymentRoutes.RetainedPaymentReceipt[] Query(ManualPaymentRequest request,
        ManualPaymentCommandReceipt receipt, ProcurementRequestScope? scope = null,
        ProcurementPrincipalPeople.Person? person = null, Guid? targetId = null)
        => ProcurementManualPaymentRoutes.RetainedReceiptQuery(new[] { request }.AsQueryable(),
            new[] { receipt }.AsQueryable(), scope ?? Scope(), person ?? Reader(), [Branch], targetId, Key).ToArray();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Read_configuration_requires_real_connection_independently_of_new_work(
        bool enabled, bool configured, bool expected)
        => Assert.Equal(expected, ProcurementManualPaymentRoutes.RetainedReadConfigured(new()
            { Enabled = enabled, ExecutionDatabaseConfigured = configured }));

    [Fact]
    public void Missing_options_or_connection_fail_closed()
    {
        Assert.False(ProcurementManualPaymentRoutes.RetainedReadConfigured(null));
        Assert.False(ProcurementManualPaymentRoutes.RetainedReadConfigured(new()));
    }

    [Fact]
    public void Current_read_only_workerless_actor_can_recover_own_original_result_after_record_advances()
    {
        var receipt = Receipt(); var request = Request();
        var found = Assert.Single(Query(request, receipt));
        Assert.Same(receipt, found.receipt); Assert.Same(request, found.request);
        Assert.Null(Reader().WorkerId);
        Assert.DoesNotContain("procurement-payment-create", Reader().Permissions);
        Assert.Equal("ConfirmedManually", request.Status);
        Assert.Equal(1, receipt.ResultVersion); // original Draft acknowledgment is not rewritten to latest status
    }

    [Fact]
    public void A_different_original_actor_cannot_be_recovered_by_a_current_reader()
    {
        var receipt = Receipt(); receipt.ActorPrincipalId = Guid.NewGuid().ToString("D");
        Assert.Empty(Query(Request(), receipt));
    }

    [Fact]
    public void Revoked_current_read_permission_denies_before_a_key_can_reveal_any_record()
        => Assert.Throws<UnauthorizedAccessException>(() => Query(Request(), Receipt(), person: Reader(false)));

    [Fact]
    public void Principal_projection_must_match_current_core_identity()
    {
        var scope = Scope(subject: Guid.NewGuid());
        Assert.Throws<UnauthorizedAccessException>(() => Query(Request(), Receipt(), scope));
    }

    [Fact]
    public void A_foreign_branch_stays_hidden_with_an_explicit_header_and_with_company_all()
    {
        var foreign = Guid.NewGuid(); var request = Request(); var receipt = Receipt();
        request.BranchId = receipt.BranchId = foreign;
        Assert.Empty(Query(request, receipt));
        Assert.Empty(Query(request, receipt, Scope(all: true)));
    }

    [Fact]
    public void A_company_all_reader_sees_only_currently_assigned_branch_records()
        => Assert.Single(Query(Request(), Receipt(), Scope(all: true)));

    [Fact]
    public void Receipt_scope_branch_target_and_key_are_bound_independently()
    {
        var request = Request(); var receipt = Receipt();
        receipt.TenantId = "foreign"; Assert.Empty(Query(request, receipt));
        receipt.TenantId = Tenant; receipt.LegalEntityId = Guid.NewGuid(); Assert.Empty(Query(request, receipt));
        receipt.LegalEntityId = Company; receipt.BranchId = Guid.NewGuid(); Assert.Empty(Query(request, receipt));
        receipt.BranchId = Branch; receipt.CommandKey = Guid.NewGuid(); Assert.Empty(Query(request, receipt));
        receipt.CommandKey = Key; Assert.Empty(Query(request, receipt, targetId: Guid.NewGuid()));
        Assert.Single(Query(request, receipt, targetId: RequestId));
    }

    [Fact]
    public void Request_scope_never_relies_only_on_context_query_filters()
    {
        var request = Request(); var receipt = Receipt();
        request.TenantId = "foreign"; Assert.Empty(Query(request, receipt));
        request.TenantId = Tenant; request.LegalEntityId = Guid.NewGuid(); Assert.Empty(Query(request, receipt));
    }

    [Fact]
    public void Finite_read_profile_requires_no_insert_update_policy_or_document_prerequisite()
    {
        var sql = ProcurementManualPaymentReadSchemaReadiness.PrivilegeSql();
        Assert.Contains("current_setting('transaction_read_only')='on'", sql);
        foreach (var table in ProcurementManualPaymentReadSchemaReadiness.RecordReadTables)
        {
            Assert.Contains("has_table_privilege(current_user,'" + table + "','SELECT')", sql);
            Assert.DoesNotContain(" AND has_table_privilege(current_user,'" + table + "','INSERT')", sql);
            Assert.DoesNotContain(" AND has_table_privilege(current_user,'" + table + "','UPDATE')", sql);
        }
        Assert.DoesNotContain("state='Published'", sql);
        Assert.DoesNotContain("core_document", sql);
        Assert.DoesNotContain("password", sql);
        Assert.DoesNotContain("auth_sessions", sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql, sql);
    }

    [Fact]
    public void Purpose_limited_label_columns_do_not_require_the_whole_user_or_supplier_record()
    {
        var columns = ProcurementManualPaymentReadSchemaReadiness.ReadColumns;
        Assert.Contains("display_name", columns["hrm.local_users"]);
        Assert.Contains("code", columns["hrm.work_locations"]); Assert.Contains("name", columns["hrm.work_locations"]);
        var sql = ProcurementManualPaymentReadSchemaReadiness.PrivilegeSql();
        Assert.Contains("has_column_privilege(current_user,'hrm.local_users','display_name','SELECT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'hrm.local_users','SELECT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.suppliers','SELECT')", sql);
    }
}
