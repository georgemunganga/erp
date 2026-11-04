namespace Mightyfin.Erp.Core.Workflows.Application;

public sealed record WorkflowConfigurationLink(string Label, string Path);

/// <summary>Discoverable configuration, not an approval definition, grant or readiness assertion.</summary>
public sealed record WorkflowConfigurationType(string Id, string ModuleId, string ModuleLabel,
    string RecordType, string RecordLabel, string Trigger, string TriggerLabel, string Description,
    string AvailabilityNote, IReadOnlyList<WorkflowConfigurationLink> Settings);

/// <summary>Modules register their own typed editors; Core does not copy module policies.</summary>
public interface IWorkflowConfigurationProvider
{
    IReadOnlyList<WorkflowConfigurationType> GetTypes();
}

public static class WorkflowConfigurationRegistry
{
    public static IReadOnlyList<WorkflowConfigurationType> Read(IEnumerable<IWorkflowConfigurationProvider> providers)
    {
        var types = providers.SelectMany(x => x.GetTypes()
            ?? throw new InvalidOperationException("Workflow configuration registration is invalid."))
            .Take(101).ToArray();
        if (types.Any(x => x is null))
            throw new InvalidOperationException("Workflow configuration registration is invalid.");
        if (types.Length > 100 || types.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != types.Length)
            throw new InvalidOperationException("Workflow configuration registration is invalid.");
        foreach (var type in types)
        {
            if (!Identifier(type.ModuleId) || !Identifier(type.RecordType) || !Identifier(type.Trigger)
                || type.Id != $"{type.ModuleId}.{type.RecordType}.{type.Trigger}"
                || !Text(type.ModuleLabel) || !Text(type.RecordLabel) || !Text(type.TriggerLabel)
                || !Text(type.Description) || !Text(type.AvailabilityNote)
                || type.Settings is null || type.Settings.Count is < 1 or > 4
                || type.Settings.Any(x => x is null)
                || type.Settings.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != type.Settings.Count
                || type.Settings.Any(x => !Text(x.Label) || !LocalSettingsPath(type.ModuleId, x.Path)))
                throw new InvalidOperationException("Workflow configuration registration is invalid.");
        }
        return types.OrderBy(x => x.ModuleLabel, StringComparer.Ordinal)
            .ThenBy(x => x.RecordLabel, StringComparer.Ordinal).ThenBy(x => x.TriggerLabel, StringComparer.Ordinal).ToArray();
    }

    private static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 60
        && value[0] is >= 'a' and <= 'z'
        && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500
        && value == value.Trim() && !value.Any(char.IsControl);
    private static bool LocalSettingsPath(string module, string value) => value is not null && value.Length <= 250
        && value.StartsWith($"/{module}/workspace/settings/", StringComparison.Ordinal)
        && !value.Contains("//", StringComparison.Ordinal)
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-')
        && value.Split('/').All(x => x is not ("." or ".."));
}
