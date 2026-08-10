namespace ActivationWs.Activation;

/// <summary>Outcome of a confirmation id request.</summary>
/// <param name="ConfirmationId">The Confirmation ID.</param>
/// <param name="FromCache">
/// <see langword="true"/> when the Confirmation ID was served from the local store without calling
/// the Microsoft Activation Service.
/// </param>
public sealed record ConfirmationIdResult(
    string ConfirmationId,
    bool FromCache = false);

/// <summary>
/// Issues Confirmation IDs, reusing a locally cached value where one already exists and otherwise
/// calling the Microsoft Activation Service.
/// </summary>
public interface IConfirmationIdService {
    Task<ConfirmationIdResult> GetOrCreateAsync(
        string installationId,
        string extendedProductId,
        string hostName,
        CancellationToken cancellationToken = default);
}
