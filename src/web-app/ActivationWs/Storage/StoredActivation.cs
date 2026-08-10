namespace ActivationWs.Storage;

/// <summary>
/// A single activation stored in the local database.
/// </summary>
/// <param name="InstallationId">The Installation ID (IID) the Confirmation ID is bound to.</param>
/// <param name="ExtendedProductId">The Extended Product ID (Extended PID).</param>
/// <param name="HostName">Name of the machine the activation was requested for.</param>
/// <param name="ConfirmationId">The Confirmation ID (CID) obtained from Microsoft.</param>
/// <param name="CreatedAtUtc">When the Confirmation ID was first obtained.</param>
/// <param name="LastRequestedAtUtc">When the record was last handed out.</param>
/// <param name="RequestCount">How often the Confirmation ID has been handed out.</param>
public sealed record StoredActivation(
    string InstallationId,
    string ExtendedProductId,
    string HostName,
    string ConfirmationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastRequestedAtUtc,
    int RequestCount);

/// <summary>One page of stored activations together with the total match count.</summary>
/// <param name="Items">Records on the requested page, most recently requested first.</param>
/// <param name="TotalCount">Total number of matching records.</param>
public sealed record ActivationRecordPage(
    IReadOnlyList<StoredActivation> Items,
    int TotalCount);
