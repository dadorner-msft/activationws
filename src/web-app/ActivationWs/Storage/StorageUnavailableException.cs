namespace ActivationWs.Storage;

/// <summary>
/// Raised when the activation store cannot be reached. Callers use this to fail closed: rather than
/// treating a store outage as a cache miss and contacting Microsoft again (which would consume another
/// activation for a Confirmation ID that was very likely already issued), the request is rejected so no
/// duplicate activation is spent while durable storage is unavailable.
/// </summary>
public sealed class StorageUnavailableException : Exception {
    public StorageUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}
