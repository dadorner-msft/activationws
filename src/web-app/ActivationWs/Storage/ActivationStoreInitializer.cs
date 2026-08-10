namespace ActivationWs.Storage;

/// <summary>
/// Creates the activation store schema during application startup, before requests are served.
/// </summary>
public sealed partial class ActivationStoreInitializer : IHostedService {
    private readonly IActivationStore _store;
    private readonly ILogger<ActivationStoreInitializer> _logger;

    public ActivationStoreInitializer(
        IActivationStore store,
        ILogger<ActivationStoreInitializer> logger) {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        try {
            await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            // The store is a best-effort cache: a failed initialization must not stop the host, since
            // Confirmation IDs can still be issued directly from the Microsoft Activation Service.
            LogInitializationFailed(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Failed to initialize the activation store. Confirmation IDs will be issued without caching.")]
    private partial void LogInitializationFailed(Exception exception);
}
