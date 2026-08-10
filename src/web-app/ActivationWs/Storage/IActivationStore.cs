namespace ActivationWs.Storage;

/// <summary>
/// Local cache of issued Confirmation IDs, keyed by Installation ID and Extended Product ID.
/// </summary>
public interface IActivationStore {
    /// <summary>Creates the database schema if it does not yet exist.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the store can be reached. Returns <see langword="true"/> when a connection was
    /// opened and a trivial query succeeded, and <see langword="false"/> when the store is unavailable.
    /// </summary>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the stored activation for the given Installation ID and Extended Product ID, or
    /// <see langword="null"/> when none exists.
    /// </summary>
    Task<StoredActivation?> FindAsync(
        string installationId,
        string extendedProductId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a newly issued Confirmation ID. If a record for the same Installation ID and
    /// Extended Product ID already exists, its last-requested timestamp and request count are
    /// updated instead and the existing Confirmation ID is preserved. Returns the effective record.
    /// </summary>
    Task<StoredActivation> AddAsync(
        StoredActivation activation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last-requested timestamp and increments the request count of an existing record.
    /// Returns the updated record, or <see langword="null"/> when no matching record exists.
    /// </summary>
    Task<StoredActivation?> TouchAsync(
        string installationId,
        string extendedProductId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one page of stored activations, optionally filtered by host name.</summary>
    Task<ActivationRecordPage> QueryAsync(
        string? host,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Streams every matching activation for a CSV export, optionally filtered by host name.</summary>
    IAsyncEnumerable<StoredActivation> StreamAsync(
        string? host,
        CancellationToken cancellationToken = default);
}
