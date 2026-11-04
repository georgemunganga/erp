using Mightyfin.Erp.Core.Workflows.Application;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreWorkflowConfigurationTests
{
    [Fact]
    public void Installed_module_registers_separate_record_triggers_without_granting_execution()
    {
        var types = WorkflowConfigurationRegistry.Read([new ProcurementWorkflowConfigurationProvider()]);
        Assert.Equal(5, types.Count);
        Assert.Equal(2, types.Count(x => x.RecordType == "payment-request"));
        Assert.Contains(types, x => x.RecordType == "purchase-request" && x.Trigger == "submitted");
        Assert.Contains(types, x => x.RecordType == "purchase-plan");
        Assert.All(types, x => Assert.All(x.Settings, link =>
            Assert.StartsWith("/procurement/workspace/settings/", link.Path)));
        Assert.Contains("limited", types.Single(x => x.RecordType == "purchase-request").AvailabilityNote);
        Assert.Contains("invoice", types.Single(x => x.Id == "procurement.payment-request.submitted").AvailabilityNote);
    }

    [Fact]
    public void Missing_modules_are_empty_and_duplicate_registrations_fail_closed()
    {
        Assert.Empty(WorkflowConfigurationRegistry.Read([]));
        Assert.Throws<InvalidOperationException>(() => WorkflowConfigurationRegistry.Read(
            [new ProcurementWorkflowConfigurationProvider(), new ProcurementWorkflowConfigurationProvider()]));
    }

    [Theory]
    [InlineData("https://external.example/settings")]
    [InlineData("//external.example/settings")]
    [InlineData("/procurement/workspace/settings/../../users")]
    [InlineData("/core/workspace/settings/users")]
    [InlineData("/procurement/workspace/settings/%2f%2fexternal")]
    [InlineData("/procurement/workspace/settings/buying-rules?redirect=https://external.example")]
    public void Configuration_links_cannot_escape_the_registered_module(string path)
    {
        var valid = new ProcurementWorkflowConfigurationProvider().GetTypes()[0];
        Assert.Throws<InvalidOperationException>(() => WorkflowConfigurationRegistry.Read(
            [new Single(valid with { Settings = [new("Unsafe", path)] })]));
    }

    [Fact]
    public void Malformed_document_identity_and_empty_editor_lists_are_rejected()
    {
        var valid = new ProcurementWorkflowConfigurationProvider().GetTypes()[0];
        Assert.Throws<InvalidOperationException>(() => WorkflowConfigurationRegistry.Read(
            [new Single(valid with { Id = "procurement.other.submitted" })]));
        Assert.Throws<InvalidOperationException>(() => WorkflowConfigurationRegistry.Read(
            [new Single(valid with { Settings = [] })]));
    }

    private sealed class Single(WorkflowConfigurationType item) : IWorkflowConfigurationProvider
    {
        public IReadOnlyList<WorkflowConfigurationType> GetTypes() => [item];
    }
}
