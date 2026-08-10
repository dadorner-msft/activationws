using ActivationWs.Activation;
using ActivationWs.Storage;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace ActivationWs.Api;

/// <summary>
/// Translates <see cref="ActivationServiceException"/> and transport failures into RFC 9457 problem details.
/// </summary>
public sealed class ActivationExceptionHandler : IExceptionHandler {
    private readonly IProblemDetailsService _problemDetailsService;

    public ActivationExceptionHandler(IProblemDetailsService problemDetailsService) =>
        _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) {
        (int status, string title, string detail) = exception switch {
            ActivationServiceException ase => (
                StatusFor(ase.Failure),
                TitleFor(ase.Failure),
                ase.Message),

            // Durable storage is unreachable. The request is rejected rather than served from Microsoft
            // so a Confirmation ID that was likely already issued is not activated a second time.
            StorageUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "Activation storage unavailable",
                "The activation database is currently unavailable. Activations are temporarily disabled."),

            TaskCanceledException or TimeoutException when !httpContext.RequestAborted.IsCancellationRequested => (
                StatusCodes.Status504GatewayTimeout,
                "Activation service timeout",
                "The Microsoft Activation Service did not respond in time. Please try again."),

            // An HTTPS target reports proxy rejections through the tunnel request rather than a 407 response.
            HttpRequestException {
                HttpRequestError: HttpRequestError.ProxyTunnelError,
                StatusCode: HttpStatusCode.ProxyAuthenticationRequired
            } => (
                StatusCodes.Status502BadGateway,
                "Proxy authentication required",
                "The proxy rejected the request because it requires authentication. " +
                "Check the Proxy section of the configuration."),

            HttpRequestException { HttpRequestError: HttpRequestError.ProxyTunnelError } ex => (
                StatusCodes.Status502BadGateway,
                "Proxy error",
                $"The proxy could not establish a connection to the Microsoft Activation Service ({ex.StatusCode})."),

            // The connection was established, only the certificate chain was rejected. A TLS inspecting
            // proxy is the usual cause, so the message points at the trust store rather than the network.
            HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => (
                StatusCodes.Status502BadGateway,
                "TLS handshake failed",
                "The TLS certificate of the Microsoft Activation Service could not be validated. " +
                "If outbound traffic passes through a TLS inspecting proxy, install its root certificate " +
                "in the Trusted Root Certification Authorities store of this machine."),

            HttpRequestException => (
                StatusCodes.Status502BadGateway,
                "Activation service unreachable",
                "The Microsoft Activation Service could not be reached. " +
                "A proxy server can be specified in the appsettings.json file, where necessary."),

            _ => default,
        };

        if (status == 0) {
            return false;
        }

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails {
            Status = status,
            Title = title,
            Detail = detail,
        };

        if (exception is ActivationServiceException { ErrorCode: { Length: > 0 } code }) {
            problemDetails.Extensions["errorCode"] = code;
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static int StatusFor(ActivationFailure failure) => failure switch {
        ActivationFailure.InvalidInstallationId => StatusCodes.Status400BadRequest,
        ActivationFailure.InvalidProductKey => StatusCodes.Status400BadRequest,
        ActivationFailure.InvalidKeyType => StatusCodes.Status400BadRequest,
        ActivationFailure.ProductKeyBlocked => StatusCodes.Status403Forbidden,
        ActivationFailure.ActivationLimitExceeded => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status502BadGateway,
    };

    private static string TitleFor(ActivationFailure failure) => failure switch {
        ActivationFailure.InvalidInstallationId => "Invalid installation id",
        ActivationFailure.InvalidProductKey => "Invalid product key",
        ActivationFailure.InvalidKeyType => "Invalid key type",
        ActivationFailure.ProductKeyBlocked => "Product key blocked",
        ActivationFailure.ActivationLimitExceeded => "Activation limit exceeded",
        ActivationFailure.ProxyAuthenticationRequired => "Proxy authentication required",
        ActivationFailure.RemoteError => "Activation service error",
        _ => "Unexpected activation service response",
    };
}
