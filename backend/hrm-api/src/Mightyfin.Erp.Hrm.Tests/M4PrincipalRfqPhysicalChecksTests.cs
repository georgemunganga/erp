using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M4PrincipalRfqPhysicalChecksTests
{
    [Fact]
    public void Exactly_six_genuine_checks_and_three_restored_spellings_are_admitted()
    {
        Assert.Equal(6, M4PrincipalRfqPhysicalChecks.Native.Count);
        Assert.Equal(3, M4PrincipalRfqPhysicalChecks.Restored.Count);
        foreach (var (name, definition) in M4PrincipalRfqPhysicalChecks.Native)
        {
            Assert.True(M4PrincipalRfqPhysicalChecks.Matches(name, definition));
            Assert.False(M4PrincipalRfqPhysicalChecks.Matches(name, definition + " AND TRUE"));
            var altered = definition.Contains("schema_version", StringComparison.Ordinal)
                ? definition.Replace("schema_version", "schema_revision", StringComparison.Ordinal)
                : definition.Replace("execution_version", "execution_revision", StringComparison.Ordinal);
            Assert.False(M4PrincipalRfqPhysicalChecks.Matches(name, altered));
        }
        foreach (var (name, definition) in M4PrincipalRfqPhysicalChecks.Restored)
        {
            Assert.True(M4PrincipalRfqPhysicalChecks.Matches(name, definition));
            Assert.NotEqual(M4PrincipalRfqPhysicalChecks.Native[name], definition);
        }
        Assert.False(M4PrincipalRfqPhysicalChecks.Matches("unknown", "CHECK (TRUE)"));
        Assert.False(M4PrincipalRfqPhysicalChecks.Matches("ck_source_policy_identity",
            M4PrincipalRfqPhysicalChecks.Native["ck_sourcing_owned_draft"]));
    }

    [Fact]
    public void Finite_publication_execution_requires_separate_current_role_and_append_only_evidence()
    {
        const string role = "private_m4_execution_role";
        var read = ProcurementPrincipalSourcingPublicationSchemaReadiness.PrivilegeSql(false, role);
        var write = ProcurementPrincipalSourcingPublicationSchemaReadiness.PrivilegeSql(true, role);
        Assert.Contains("session_user=current_user", read);
        Assert.Contains("current_user='private_m4_execution_role'", read);
        foreach (var table in new[] { "sourcing_publication_openings", "sourcing_publication_decisions",
            "sourcing_publication_receipts", "audit_events", "outbox_events" })
        {
            Assert.Contains("NOT has_any_column_privilege(current_user,'procurement." + table + "','UPDATE')", read);
            Assert.Contains("NOT has_table_privilege(current_user,'procurement." + table + "','DELETE')", read);
            Assert.Contains("has_table_privilege(current_user,'procurement." + table + "','INSERT')", write);
        }
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.sourcing_publication_openings','INSERT')", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.sourcing_invitations','INSERT')", write);
        Assert.DoesNotContain("'core_approval.cases','INSERT'", write);
    }
}
