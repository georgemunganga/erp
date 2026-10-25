namespace Mightyfin.Erp.Hrm.Api;

public enum CoreHostMode { Api, MigrationOwner, OutboxPublisher }

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
            if ((option.StartsWith("--apply-migration", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(option, ownerFlag, StringComparison.Ordinal))
                || (option.StartsWith("--run-outbox", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(option, publisherFlag, StringComparison.Ordinal)))
                throw new InvalidOperationException("A startup mode flag is invalid; use the exact supported option.");
        }
        var owner = options.Contains(ownerFlag, StringComparer.Ordinal);
        var publisher = options.Contains(publisherFlag, StringComparer.Ordinal);
        if (owner && publisher)
            throw new InvalidOperationException("Migration-only and outbox-publisher modes are mutually exclusive.");
        return owner ? CoreHostMode.MigrationOwner : publisher ? CoreHostMode.OutboxPublisher : CoreHostMode.Api;
    }

    public static bool MayApplyCoreMigrations(CoreHostMode mode, IConfiguration configuration,
        bool development, IEnumerable<string> arguments)
        => mode != CoreHostMode.OutboxPublisher
            && CoreSchemaStartup.MayApplyMigrations(configuration, development, arguments);

    public static bool MayBootstrapLocalIdentity(CoreHostMode mode) => mode == CoreHostMode.Api;
}
