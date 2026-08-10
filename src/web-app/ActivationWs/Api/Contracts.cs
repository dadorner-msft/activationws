using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Api;

/// <summary>
/// Request payload for obtaining a Confirmation ID (CID).
/// </summary>
/// <param name="InstallationId">
/// The Installation ID (IID).
/// </param>
/// <param name="ExtendedProductId">
/// The Extended Product ID (Extended PID).
/// </param>
/// <param name="HostName">
/// Name of the machine being activated. It is not sent to Microsoft, but is stored locally so
/// activations can be traced back to a host and counted per host.
/// </param>
public sealed record ConfirmationIdRequest(
    [property: Required]
    [property: RegularExpression(ValidationPatterns.InstallationId, ErrorMessage = "The Installation ID must consist of exactly 63 digits.")]
    string InstallationId,

    [property: Required]
    [property: RegularExpression(ValidationPatterns.ExtendedProductId, ErrorMessage = "The Extended Product ID has an unexpected format.")]
    string ExtendedProductId,

    [property: Required]
    [property: MaxLength(253)]
    string HostName);

/// <summary>
/// Request payload for querying the number of remaining activations of a Multiple Activation Key.
/// </summary>
/// <param name="ExtendedProductId">The Extended Product ID (Extended PID) of the installation.</param>
public sealed record ActivationCountRequest(
    [property: Required]
    [property: RegularExpression(ValidationPatterns.ExtendedProductId, ErrorMessage = "The Extended Product ID has an unexpected format.")]
    string ExtendedProductId);

/// <summary>Successful Confirmation ID response.</summary>
/// <param name="ConfirmationId">The Confirmation ID (CID).</param>
/// <param name="FromCache">
/// <see langword="true"/> when the Confirmation ID was served from the local store without contacting
/// the Microsoft Activation Service; <see langword="false"/> when it was freshly issued.
/// </param>
public sealed record ConfirmationIdResponse(string ConfirmationId, bool FromCache);

/// <summary>Successful activation count response.</summary>
/// <param name="RemainingActivations">Number of activations still available for the key.</param>
public sealed record ActivationCountResponse(int RemainingActivations);

/// <summary>A stored activation as shown on the reporting page.</summary>
/// <param name="InstallationId">Installation ID.</param>
/// <param name="HostName">Host the activation was requested for.</param>
/// <param name="ExtendedProductId">Extended Product ID the Confirmation ID is bound to.</param>
/// <param name="ConfirmationId">The issued Confirmation ID.</param>
/// <param name="CreatedAtUtc">When the Confirmation ID was first obtained from Microsoft.</param>
/// <param name="LastRequestedAtUtc">When the record was last requested.</param>
/// <param name="RequestCount">How often the Confirmation ID has been handed out.</param>
public sealed record ActivationRecordResponse(
    string InstallationId,
    string HostName,
    string ExtendedProductId,
    string ConfirmationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastRequestedAtUtc,
    int RequestCount);

/// <summary>One page of activation records.</summary>
/// <param name="Items">Records on the requested page, most recently requested first.</param>
/// <param name="Page">One based number of the returned page.</param>
/// <param name="PageSize">Maximum number of records per page.</param>
/// <param name="TotalCount">Total number of matching records.</param>
/// <param name="TotalPages">Total number of pages, at least 1.</param>
public sealed record ActivationRecordPageResponse(
    IReadOnlyList<ActivationRecordResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal static class ValidationPatterns {
    /// <summary>Regex for Installation ID.</summary>
    public const string InstallationId = @"^\d{63}$";

    /// <summary>Regex for Extended Product ID.</summary>
    public const string ExtendedProductId = @"^[0-9A-Za-z]+(?:[-.][0-9A-Za-z]+)+$";
}
