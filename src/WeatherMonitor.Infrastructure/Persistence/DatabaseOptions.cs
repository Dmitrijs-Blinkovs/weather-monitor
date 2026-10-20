namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>Bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations when the host starts.</summary>
    public bool ApplyMigrationsOnStartup { get; init; }

    /// <summary>
    /// Optional Entra identity that gets read-only access (db_datareader) after the migrations, e.g. the web app's
    /// managed identity in Azure. Requires the migrating identity to be the SQL Entra administrator.
    /// </summary>
    public ReadOnlyIdentityOptions? ReadOnlyIdentity { get; init; }
}

public sealed class ReadOnlyIdentityOptions
{
    /// <summary>Display name of the identity, used as the database user name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Client (application) ID of the identity; becomes the user's SID, so no directory lookup is needed.</summary>
    public Guid ClientId { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Name) && ClientId != Guid.Empty;
}
