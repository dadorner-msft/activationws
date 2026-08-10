namespace ActivationWs.Activation;

/// <summary>
/// Classification of a failure returned by (or while talking to) the Microsoft Activation Service.
/// </summary>
public enum ActivationFailure {
    /// <summary>The remote service replied with an unexpected or unparsable payload.</summary>
    UnexpectedResponse,

    /// <summary>The supplied Installation ID is not valid.</summary>
    InvalidInstallationId,

    /// <summary>The supplied Extended Product ID is not valid.</summary>
    InvalidProductKey,

    /// <summary>The product key has been blocked by Microsoft.</summary>
    ProductKeyBlocked,

    /// <summary>The key type is not supported by this activation method.</summary>
    InvalidKeyType,

    /// <summary>The Multiple Activation Key has exceeded its activation limit.</summary>
    ActivationLimitExceeded,

    /// <summary>The remote service reported an error code that is not explicitly handled.</summary>
    RemoteError,

    /// <summary>The configured proxy refused the request because it requires authentication.</summary>
    ProxyAuthenticationRequired,
}

/// <summary>
/// Raised when the Microsoft Activation Service cannot fulfil a request.
/// </summary>
public sealed class ActivationServiceException : Exception {
    public ActivationServiceException(
        ActivationFailure failure,
        string message,
        string? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException) {
        Failure = failure;
        ErrorCode = errorCode;
    }

    /// <summary>Classification of the failure.</summary>
    public ActivationFailure Failure { get; }

    /// <summary>Raw error code reported by the remote service, if any (e.g. <c>0x7F</c>).</summary>
    public string? ErrorCode { get; }
}
