using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ActivationWs.Storage;

/// <summary>
/// SQLite backed <see cref="IActivationStore"/>. A connection is opened per operation and pooled by
/// Microsoft.Data.Sqlite. The database runs in WAL mode so reporting reads do not block activation
/// writes, which suits a single, non clustered IIS host holding up to a few hundred thousand records.
/// </summary>
public sealed partial class SqliteActivationStore : IActivationStore {
    // Round-trip, culture invariant, sortable UTC timestamps.
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    private const string SelectColumns =
        "InstallationId, ExtendedProductId, HostName, ConfirmationId, CreatedAtUtc, LastRequestedAtUtc, RequestCount";

    private readonly string _connectionString;
    private readonly string? _databaseDirectory;
    private readonly ILogger<SqliteActivationStore> _logger;

    public SqliteActivationStore(
        IOptions<ActivationStoreOptions> options,
        IHostEnvironment environment,
        ILogger<SqliteActivationStore> logger) {
        _logger = logger;
        (_connectionString, _databaseDirectory) = ResolveConnectionString(options.Value.ConnectionString, environment.ContentRootPath);
    }

    /// <summary>
    /// Resolves a relative <c>Data Source</c> against the content root.
    /// Returns the resolved connection string and the directory that must exist before the database
    /// file can be opened (or <see langword="null"/> for in-memory databases).
    /// Directory creation is intentionally deferred to <see cref="InitializeAsync"/> so that a
    /// permission failure is absorbed by the best-effort initializer rather than crashing startup.
    /// </summary>
    private static (string ConnectionString, string? Directory) ResolveConnectionString(string connectionString, string contentRootPath) {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        bool isFilePath =
            !string.IsNullOrEmpty(builder.DataSource) &&
            !builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) &&
            builder.Mode != SqliteOpenMode.Memory;

        if (isFilePath && !Path.IsPathRooted(builder.DataSource)) {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        }

        string? directory = isFilePath && Path.GetDirectoryName(builder.DataSource) is { Length: > 0 } dir
            ? dir
            : null;

        return (builder.ToString(), directory);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken) {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // busy_timeout lets concurrent writers wait for a lock instead of failing immediately.
        await using (SqliteCommand pragma = connection.CreateCommand()) {
            pragma.CommandText = "PRAGMA busy_timeout = 5000;";
            await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        // Create the database directory here rather than in the constructor so that a permission
        // failure is caught by ActivationStoreInitializer's best-effort handler instead of
        // propagating out of the DI container and crashing the host.
        if (_databaseDirectory is not null) {
            Directory.CreateDirectory(_databaseDirectory);
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS Activations (
                InstallationId     TEXT    NOT NULL,
                ExtendedProductId  TEXT    NOT NULL,
                HostName           TEXT    NOT NULL,
                ConfirmationId     TEXT    NOT NULL,
                CreatedAtUtc       TEXT    NOT NULL,
                LastRequestedAtUtc TEXT    NOT NULL,
                RequestCount       INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (InstallationId, ExtendedProductId)
            );

            CREATE INDEX IF NOT EXISTS IX_Activations_HostName
                ON Activations (HostName);

            CREATE INDEX IF NOT EXISTS IX_Activations_LastRequestedAtUtc
                ON Activations (LastRequestedAtUtc DESC);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        LogInitialized();
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default) {
        try {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return true;
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            LogPingFailed(ex);
            return false;
        }
    }

    public async Task<StoredActivation?> FindAsync(
        string installationId,
        string extendedProductId,
        CancellationToken cancellationToken = default) {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {SelectColumns} FROM Activations " +
            "WHERE InstallationId = $iid AND ExtendedProductId = $epid LIMIT 1;";
        command.Parameters.AddWithValue("$iid", installationId);
        command.Parameters.AddWithValue("$epid", extendedProductId);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            return Map(reader);
        }

        return null;
    }

    public async Task<StoredActivation> AddAsync(
        StoredActivation activation,
        CancellationToken cancellationToken = default) {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();

        // Concurrent first-requests for the same key can race; ON CONFLICT keeps the first Confirmation
        // ID and simply records another request, so a UNIQUE clash never produces a duplicate row.
        command.CommandText =
            $"""
            INSERT INTO Activations
                (InstallationId, ExtendedProductId, HostName, ConfirmationId, CreatedAtUtc, LastRequestedAtUtc, RequestCount)
            VALUES
                ($iid, $epid, $host, $cid, $created, $last, 1)
            ON CONFLICT (InstallationId, ExtendedProductId) DO UPDATE SET
                LastRequestedAtUtc = MAX(LastRequestedAtUtc, excluded.LastRequestedAtUtc),
                RequestCount = RequestCount + 1
            RETURNING {SelectColumns};
            """;
        command.Parameters.AddWithValue("$iid", activation.InstallationId);
        command.Parameters.AddWithValue("$epid", activation.ExtendedProductId);
        command.Parameters.AddWithValue("$host", activation.HostName);
        command.Parameters.AddWithValue("$cid", activation.ConfirmationId);
        command.Parameters.AddWithValue("$created", Format(activation.CreatedAtUtc));
        command.Parameters.AddWithValue("$last", Format(activation.LastRequestedAtUtc));

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return Map(reader);
    }

    public async Task<StoredActivation?> TouchAsync(
        string installationId,
        string extendedProductId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default) {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            UPDATE Activations SET
                LastRequestedAtUtc = MAX(LastRequestedAtUtc, $last),
                RequestCount = RequestCount + 1
            WHERE InstallationId = $iid AND ExtendedProductId = $epid
            RETURNING {SelectColumns};
            """;
        command.Parameters.AddWithValue("$iid", installationId);
        command.Parameters.AddWithValue("$epid", extendedProductId);
        command.Parameters.AddWithValue("$last", Format(requestedAtUtc));

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            return Map(reader);
        }

        return null;
    }

    public async Task<ActivationRecordPage> QueryAsync(
        string? host,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) {
        // Offset is computed as long so a large page can never wrap to a negative SQLite offset.
        long offset = (long)(page - 1) * pageSize;
        string? hostFilter = NormalizeHostFilter(host);

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        int totalCount;
        await using (SqliteCommand countCommand = connection.CreateCommand()) {
            countCommand.CommandText = hostFilter is null
                ? "SELECT COUNT(*) FROM Activations;"
                : "SELECT COUNT(*) FROM Activations WHERE HostName LIKE $host ESCAPE '\\';";
            if (hostFilter is not null) {
                countCommand.Parameters.AddWithValue("$host", hostFilter);
            }

            object? scalar = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            totalCount = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        var items = new List<StoredActivation>();
        if (totalCount > 0 && offset < totalCount) {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT {SelectColumns} FROM Activations " +
                (hostFilter is null ? "" : "WHERE HostName LIKE $host ESCAPE '\\' ") +
                "ORDER BY LastRequestedAtUtc DESC, InstallationId ASC " +
                "LIMIT $limit OFFSET $offset;";
            if (hostFilter is not null) {
                command.Parameters.AddWithValue("$host", hostFilter);
            }
            command.Parameters.AddWithValue("$limit", pageSize);
            command.Parameters.AddWithValue("$offset", offset);

            await using SqliteDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                items.Add(Map(reader));
            }
        }

        return new ActivationRecordPage(items, totalCount);
    }

    public async IAsyncEnumerable<StoredActivation> StreamAsync(
        string? host,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        string? hostFilter = NormalizeHostFilter(host);

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {SelectColumns} FROM Activations " +
            (hostFilter is null ? "" : "WHERE HostName LIKE $host ESCAPE '\\' ") +
            "ORDER BY LastRequestedAtUtc DESC, InstallationId ASC;";
        if (hostFilter is not null) {
            command.Parameters.AddWithValue("$host", hostFilter);
        }

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            yield return Map(reader);
        }
    }

    /// <summary>Builds a substring LIKE filter, escaping the LIKE wildcards in the user's input.</summary>
    private static string? NormalizeHostFilter(string? host) {
        if (string.IsNullOrWhiteSpace(host)) {
            return null;
        }

        string escaped = host.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static StoredActivation Map(SqliteDataReader reader) => new(
        InstallationId: reader.GetString(0),
        ExtendedProductId: reader.GetString(1),
        HostName: reader.GetString(2),
        ConfirmationId: reader.GetString(3),
        CreatedAtUtc: ParseTimestamp(reader.GetString(4)),
        LastRequestedAtUtc: ParseTimestamp(reader.GetString(5)),
        RequestCount: reader.GetInt32(6));

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Activation store initialized.")]
    private partial void LogInitialized();

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Activation store health check failed; the store is currently unavailable.")]
    private partial void LogPingFailed(Exception exception);
}
