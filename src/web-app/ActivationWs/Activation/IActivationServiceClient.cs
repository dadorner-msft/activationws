namespace ActivationWs.Activation;

/// <summary>
/// Client for the Microsoft Activation Service.
/// </summary>
public interface IActivationServiceClient {
    /// <summary>
    /// Requests a Confirmation ID (CID) for the given Installation ID and Extended Product ID.
    /// </summary>
    Task<string> GetConfirmationIdAsync(
        string installationId,
        string extendedProductId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the number of remaining activations for the given Extended Product ID.
    /// </summary>
    Task<int> GetRemainingActivationsAsync(
        string extendedProductId,
        CancellationToken cancellationToken = default);
}
