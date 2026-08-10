using ActivationWs.Storage;

namespace ActivationWs.Activation;

/// <inheritdoc cref="IConfirmationIdService"/>
public sealed partial class ConfirmationIdService : IConfirmationIdService {
    private readonly IActivationServiceClient _client;
    private readonly IActivationStore _store;
    private readonly KeyedAsyncLock _locks;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConfirmationIdService> _logger;

    public ConfirmationIdService(
        IActivationServiceClient client,
        IActivationStore store,
        KeyedAsyncLock locks,
        TimeProvider timeProvider,
        ILogger<ConfirmationIdService> logger) {
        _client = client;
        _store = store;
        _locks = locks;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ConfirmationIdResult> GetOrCreateAsync(
        string installationId,
        string extendedProductId,
        string hostName,
        CancellationToken cancellationToken = default) {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        // 2. Look up an existing Confirmation ID. A store failure fails the request closed (throws
        //    StorageUnavailableException) rather than falling through to Microsoft: a lookup failure
        //    cannot be distinguished from "already activated", so contacting Microsoft again risks
        //    consuming a second activation for a Confirmation ID that was very likely already issued.
        if (await FindOrThrowAsync(installationId, extendedProductId, cancellationToken).ConfigureAwait(false) is { } cached) {
            await TryTouchAsync(installationId, extendedProductId, now, cancellationToken).ConfigureAwait(false);
            LogServedFromCache(hostName);
            return new ConfirmationIdResult(cached.ConfirmationId, FromCache: true);
        }

        // Coalesce concurrent first-requests for the same key so only one call reaches Microsoft.
        string lockKey = $"{installationId}|{extendedProductId}";
        using IDisposable _ = await _locks.LockAsync(lockKey, cancellationToken).ConfigureAwait(false);

        // Re-check inside the lock: another request may have populated the cache while we waited.
        if (await FindOrThrowAsync(installationId, extendedProductId, cancellationToken).ConfigureAwait(false) is { } racedIn) {
            await TryTouchAsync(installationId, extendedProductId, now, cancellationToken).ConfigureAwait(false);
            LogServedFromCache(hostName);
            return new ConfirmationIdResult(racedIn.ConfirmationId, FromCache: true);
        }

        // 3. Cache miss: obtain a Confirmation ID from Microsoft. This is the only step that must not
        //    be swallowed, because a failure here means no Confirmation ID was issued.
        string confirmationId = await _client
            .GetConfirmationIdAsync(installationId, extendedProductId, cancellationToken)
            .ConfigureAwait(false);

        // Persist with a separate token so client cancellation after Microsoft has already responded
        // cannot prevent the CID from being stored. AddAsync uses ON CONFLICT … RETURNING, so the
        // returned CID is the one that won the insert race (relevant during IIS overlapped recycling
        // when two workers may each obtain a different CID from Microsoft).
        string effectiveCid = await TryAddAsync(
            new StoredActivation(installationId, extendedProductId, hostName, confirmationId, now, now, RequestCount: 1),
            CancellationToken.None).ConfigureAwait(false);

        LogIssued(hostName);
        return new ConfirmationIdResult(effectiveCid, FromCache: false);
    }

    private async Task<StoredActivation?> FindOrThrowAsync(
        string installationId, string extendedProductId, CancellationToken cancellationToken) {
        try {
            return await _store.FindAsync(installationId, extendedProductId, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            // Fail closed: a lookup failure is indistinguishable from "already activated", so we must
            // not fall through to Microsoft and risk spending another activation. The request is
            // rejected (surfaced as 503) until durable storage is reachable again.
            LogStoreLookupFailed(ex);
            throw new StorageUnavailableException(
                "The activation store could not be reached, so the request was rejected to avoid consuming a duplicate activation.",
                ex);
        }
    }

    private async Task TryTouchAsync(
        string installationId, string extendedProductId, DateTimeOffset now, CancellationToken cancellationToken) {
        try {
            await _store.TouchAsync(installationId, extendedProductId, now, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            LogStorePersistFailed(ex);
        }
    }

    private async Task<string> TryAddAsync(StoredActivation activation, CancellationToken cancellationToken) {
        try {
            StoredActivation stored = await _store.AddAsync(activation, cancellationToken).ConfigureAwait(false);
            // Return the CID that is now in the database. Under a concurrent insert (e.g. two IIS
            // workers during overlapped recycling) ON CONFLICT keeps the first row, so the stored
            // CID may differ from the one passed in.
            return stored.ConfirmationId;
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            // Critical and distinct from a lookup failure: Microsoft has ALREADY issued (and charged)
            // a Confirmation ID that we could not persist. We cannot fail the request here without
            // hiding a CID the client is entitled to, so it is still returned. The activation is at
            // risk of being spent again on the next request for the same key, hence the critical log
            // to prompt manual reconciliation.
            LogIssuedButNotPersisted(ex);
            return activation.ConfirmationId;
        }
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Issued a new Confirmation ID for host {HostName}.")]
    private partial void LogIssued(string hostName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Served a cached Confirmation ID for host {HostName}.")]
    private partial void LogServedFromCache(string hostName);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Activation store lookup failed; the request was rejected to avoid consuming a duplicate activation.")]
    private partial void LogStoreLookupFailed(Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Critical,
        Message = "A Confirmation ID was issued by the Microsoft Activation Service but could not be persisted. " +
            "The activation has been spent; reconcile this manually before the next request for the same key.")]
    private partial void LogIssuedButNotPersisted(Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to persist activation data. The Confirmation ID was still returned to the client.")]
    private partial void LogStorePersistFailed(Exception exception);
}
