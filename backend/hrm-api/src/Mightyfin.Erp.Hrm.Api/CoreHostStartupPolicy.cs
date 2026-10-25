namespace Mightyfin.Erp.Hrm.Api;

public enum CoreHostMode { Api, MigrationOwner, OutboxPublisher }

/// <summary>Separates the ordinary host, schema owner and outbox process before database startup.</summary>
public static class CoreHostStartupPolicy
{
    public static CoreHostMode Parse(IEnumerable<string> arguments)
    {
        var owner = arguments.Contains("--apply-migrations-only", StringComparer.Ordinal);
        var publisher = arguments.Contains("--run-outbox-publisher", StringComparer.Ordinal);
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
