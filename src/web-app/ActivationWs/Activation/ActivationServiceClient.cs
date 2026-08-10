using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace ActivationWs.Activation;

/// <summary>
/// Typed <see cref="HttpClient"/> that talks to the Microsoft Activation Service SOAP endpoint.
/// </summary>
public sealed partial class ActivationServiceClient : IActivationServiceClient {
    private const int RequestTypeConfirmationId = 1;
    private const int RequestTypeRemainingActivations = 2;

    /// <summary>Key used to derive the HMAC-SHA256 digest of the activation request.</summary>
    private static ReadOnlySpan<byte> MacKey =>
    [
        254,  49, 152, 117, 251,  72, 132, 134,
        156, 243, 241, 206, 153, 168, 144, 100,
        171,  87,  31, 202,  71,   4,  80,  88,
         48,  36, 226,  20,  98, 135, 121, 160,
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
          0,   0,   0,   0,   0,   0,   0,   0,
    ];

    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace XmlSchemaInstanceNs = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XmlSchemaNs = "http://www.w3.org/2001/XMLSchema";

    // These identifiers are part of Microsoft's wire contract and must not be renamed.
    private static readonly XNamespace ServiceNs = "http://www.microsoft.com/BatchActivationService";
    private static readonly XNamespace RequestNs = "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0";
    private static readonly XNamespace ResponseNs = "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0";

    private readonly HttpClient _httpClient;
    private readonly ActivationServiceOptions _options;
    private readonly ILogger<ActivationServiceClient> _logger;

    public ActivationServiceClient(
        HttpClient httpClient,
        IOptions<ActivationServiceOptions> options,
        ILogger<ActivationServiceClient> logger) {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetConfirmationIdAsync(
        string installationId,
        string extendedProductId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(extendedProductId);

        XDocument response = await SendAsync(
            RequestTypeConfirmationId,
            installationId,
            extendedProductId,
            cancellationToken).ConfigureAwait(false);

        string responseType = RequireValue(response, "ResponseType");
        if (responseType != "1") {
            throw Unexpected($"Expected a Confirmation ID response, but the remote server returned response type '{responseType}'.");
        }

        return RequireValue(response, "CID");
    }

    public async Task<int> GetRemainingActivationsAsync(
        string extendedProductId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(extendedProductId);

        XDocument response = await SendAsync(
            RequestTypeRemainingActivations,
            installationId: null,
            extendedProductId,
            cancellationToken).ConfigureAwait(false);

        string responseType = RequireValue(response, "ResponseType");
        if (responseType != "2") {
            throw Unexpected($"Expected an activation count response, but the remote server returned response type '{responseType}'.");
        }

        string remaining = RequireValue(response, "ActivationRemaining");
        return int.TryParse(remaining, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw Unexpected($"The remote server returned a non-numeric activation count ('{remaining}').");
    }

    /// <summary>
    /// Posts the SOAP envelope and returns the decoded inner <c>ResponseXml</c> document.
    /// </summary>
    private async Task<XDocument> SendAsync(
        int requestType,
        string? installationId,
        string extendedProductId,
        CancellationToken cancellationToken) {
        XDocument soapRequest = CreateSoapRequest(requestType, installationId, extendedProductId);

        using var content = new StringContent(
            soapRequest.Declaration + Environment.NewLine + soapRequest,
            Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint) { Content = content };
        request.Headers.Add("SOAPAction", _options.SoapAction);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

        using HttpResponseMessage httpResponse = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode) {
            LogTransportFailure((int)httpResponse.StatusCode);

            if (httpResponse.StatusCode == HttpStatusCode.ProxyAuthenticationRequired) {
                throw new ActivationServiceException(
                    ActivationFailure.ProxyAuthenticationRequired,
                    "The proxy rejected the request because it requires authentication. Check the Proxy section of the configuration.");
            }

            throw new ActivationServiceException(
                ActivationFailure.UnexpectedResponse,
                $"The remote server returned HTTP status {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}).");
        }

        await using Stream stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        XDocument soapResponse;
        try {
            soapResponse = await XDocument
                .LoadAsync(stream, LoadOptions.None, cancellationToken)
                .ConfigureAwait(false);
        } catch (XmlException ex) {
            throw new ActivationServiceException(
                ActivationFailure.UnexpectedResponse,
                "The remote server returned a response that is not valid XML.",
                innerException: ex);
        }

        return ParseSoapResponse(soapResponse);
    }

    private static XDocument CreateSoapRequest(int requestType, string? installationId, string extendedProductId) {
        var activationRequest = new XElement(
            RequestNs + "ActivationRequest",
            new XElement(RequestNs + "VersionNumber", "2.0"),
            new XElement(RequestNs + "RequestType", requestType),
            new XElement(
                RequestNs + "Requests",
                new XElement(
                    RequestNs + "Request",
                    new XElement(RequestNs + "PID", extendedProductId),
                    requestType == RequestTypeConfirmationId
                        ? new XElement(RequestNs + "IID", installationId)
                        : null)));

        // The service expects the UTF-16 encoded request, Base64 encoded, plus a Base64 HMAC-SHA256 digest of the same bytes.
        byte[] bytes = Encoding.Unicode.GetBytes(activationRequest.ToString(SaveOptions.DisableFormatting));
        string requestXml = Convert.ToBase64String(bytes);
        string digest = Convert.ToBase64String(HMACSHA256.HashData(MacKey, bytes));

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "no"),
            new XElement(
                SoapNs + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", SoapNs),
                new XAttribute(XNamespace.Xmlns + "xsi", XmlSchemaInstanceNs),
                new XAttribute(XNamespace.Xmlns + "xsd", XmlSchemaNs),
                new XElement(
                    SoapNs + "Body",
                    new XElement(
                        ServiceNs + "BatchActivate",
                        new XElement(
                            ServiceNs + "request",
                            new XElement(ServiceNs + "Digest", digest),
                            new XElement(ServiceNs + "RequestXml", requestXml))))));
    }

    private XDocument ParseSoapResponse(XDocument soapResponse) {
        XElement? responseXmlElement = soapResponse.Descendants(ServiceNs + "ResponseXml").FirstOrDefault();
        if (responseXmlElement is null) {
            throw Unexpected("The remote server returned a response without an activation payload.");
        }

        XDocument responseXml;
        try {
            responseXml = XDocument.Parse(responseXmlElement.Value);
        } catch (XmlException ex) {
            throw new ActivationServiceException(
                ActivationFailure.UnexpectedResponse,
                "The remote server returned an activation payload that is not valid XML.",
                innerException: ex);
        }

        string? errorCode = responseXml.Descendants(ResponseNs + "ErrorCode").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(errorCode)) {
            LogRemoteError(errorCode);
            throw ToException(errorCode);
        }

        return responseXml;
    }

    private static ActivationServiceException ToException(string errorCode) => errorCode switch {
        "0x7F" => new ActivationServiceException(
            ActivationFailure.ActivationLimitExceeded,
            "The Multiple Activation Key has exceeded its activation limit.",
            errorCode),
        "0x67" => new ActivationServiceException(
            ActivationFailure.ProductKeyBlocked,
            "The product key has been blocked.",
            errorCode),
        "0x68" => new ActivationServiceException(
            ActivationFailure.InvalidProductKey,
            "Invalid product key.",
            errorCode),
        "0x86" => new ActivationServiceException(
            ActivationFailure.InvalidKeyType,
            "Invalid key type.",
            errorCode),
        "0x90" => new ActivationServiceException(
            ActivationFailure.InvalidInstallationId,
            "The Installation ID is invalid. Please check it and try again.",
            errorCode),
        _ => new ActivationServiceException(
            ActivationFailure.RemoteError,
            $"The remote server reported an error ({errorCode}).",
            errorCode),
    };

    private static string RequireValue(XDocument responseXml, string elementName) {
        string? value = responseXml.Descendants(ResponseNs + elementName).FirstOrDefault()?.Value;
        return string.IsNullOrWhiteSpace(value)
            ? throw Unexpected($"The remote server returned a response without a '{elementName}' element.")
            : value;
    }

    private static ActivationServiceException Unexpected(string message) =>
        new(ActivationFailure.UnexpectedResponse, message);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Microsoft Activation Service returned error code {ErrorCode}.")]
    private partial void LogRemoteError(string errorCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Microsoft Activation Service returned HTTP status {StatusCode}.")]
    private partial void LogTransportFailure(int statusCode);
}
