namespace Mightyfin.Erp.Hrm.Api;

public enum CoreHostMode { Api, MigrationOwner, OutboxPublisher, InvitationDeliveryOperations }

/// <summary>Separates the ordinary host, schema owner and outbox process before database startup.</summary>
public static class CoreHostStartupPolicy
{
    public static CoreHostMode Parse(IEnumerable<string> arguments)
    {
        const string ownerFlag = "--apply-migrations-only";
        const string publisherFlag = "--run-outbox-publisher";
        var options = arguments.ToArray();
        foreach (var option in options)
        {
            if ((option.StartsWith("--apply-", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(option, ownerFlag, StringComparison.Ordinal))
                || (option.StartsWith("--run-", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(option, publisherFlag, StringComparison.Ordinal)
                    && !string.Equals(option, CoreCompanyInvitationOperations.Mode, StringComparison.Ordinal)))
                throw new InvalidOperationException("A startup mode flag is invalid; use the exact supported option.");
        }
        var owner = options.Contains(ownerFlag, StringComparer.Ordinal);
        var publisher = options.Contains(publisherFlag, StringComparer.Ordinal);
        var operations=options.Contains(CoreCompanyInvitationOperations.Mode,StringComparer.Ordinal);
        if ((owner?1:0)+(publisher?1:0)+(operations?1:0)>1)
            throw new InvalidOperationException("Process authority modes are mutually exclusive.");
        return owner ? CoreHostMode.MigrationOwner : publisher ? CoreHostMode.OutboxPublisher : operations?CoreHostMode.InvitationDeliveryOperations:CoreHostMode.Api;
    }

    public static bool MayApplyCoreMigrations(CoreHostMode mode, IConfiguration configuration,
        bool development, IEnumerable<string> arguments)
        => mode is CoreHostMode.Api or CoreHostMode.MigrationOwner
            && CoreSchemaStartup.MayApplyMigrations(configuration, development, arguments);

    public static bool MayBootstrapLocalIdentity(CoreHostMode mode) => mode == CoreHostMode.Api;
}
