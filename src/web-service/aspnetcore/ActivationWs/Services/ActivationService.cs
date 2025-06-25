using ActivationWs.Pages;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace ActivationWs.Services
{
    public static class ActivationService {
        // Key for HMAC/SHA256 signature.
        private static readonly byte[] macKey = new byte[64] {
            254,  49, 152, 117, 251,  72, 132, 134,
            156, 243, 241, 206, 153, 168, 144, 100,
            171,  87,  31, 202,  71,   4,  80,  88,
            48,   36, 226,  20,  98, 135, 121, 160,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0
        };

        private const string Action = "http://www.microsoft.com/BatchActivationService/BatchActivate"; // <- Reverting back to the original action
        private static readonly Uri uri = new Uri("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");

        private static readonly XNamespace soapSchemaNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace xmlSchemaInstanceNs = "http://www.w3.org/2001/XMLSchema-instance";
        private static readonly XNamespace xmlSchemaNs = "http://www.w3.org/2001/XMLSchema";
        private static readonly XNamespace batchActivationServiceNs = "http://www.microsoft.com/BatchActivationService";
        private static readonly XNamespace batchActivationRequestNs = "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0";
        private static readonly XNamespace batchActivationResponseNs = "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0";

        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<string> CallWebServiceAsync(int requestType, string installationId, string extendedProductId) {
            XDocument soapRequest = CreateSoapRequest(requestType, installationId, extendedProductId);

            try {
                XDocument soapResponse = await SendHttpRequestAsync(soapRequest);
                return ParseSoapResponse(soapResponse);

            } catch (HttpRequestException ex) {
                // Handle specific HTTP request exceptions
                throw new HttpRequestException("The Microsoft Batch Activation Service is unavailable.", ex, HttpStatusCode.ServiceUnavailable);
            }
        }

        private static XDocument CreateSoapRequest(int requestType, string installationId, string extendedProductId) {
            XElement activationRequest = new XElement(batchActivationRequestNs + "ActivationRequest",
                new XElement(batchActivationRequestNs + "VersionNumber", "2.0"),
                new XElement(batchActivationRequestNs + "RequestType", requestType),
                new XElement(batchActivationRequestNs + "Requests",
                    new XElement(batchActivationRequestNs + "Request",
                        new XElement(batchActivationRequestNs + "PID", extendedProductId),
                        requestType == 1 ? new XElement(batchActivationRequestNs + "IID", installationId) : null)
                )
            );

            byte[] bytes = Encoding.Unicode.GetBytes(activationRequest.ToString());
            string requestXml = Convert.ToBase64String(bytes);

            using (HMACSHA256 hMACSHA = new HMACSHA256(macKey)) {
                string digest = Convert.ToBase64String(hMACSHA.ComputeHash(bytes));

                return new XDocument(
                    new XDeclaration("1.0", "UTF-8", "no"),
                    new XElement(soapSchemaNs + "Envelope",
                        new XAttribute(XNamespace.Xmlns + "soap", soapSchemaNs),
                        new XAttribute(XNamespace.Xmlns + "xsi", xmlSchemaInstanceNs),
                        new XAttribute(XNamespace.Xmlns + "xsd", xmlSchemaNs),
                        new XElement(soapSchemaNs + "Body",
                            new XElement(batchActivationServiceNs + "BatchActivate",
                                new XElement(batchActivationServiceNs + "request",
                                    new XElement(batchActivationServiceNs + "Digest", digest),
                                    new XElement(batchActivationServiceNs + "RequestXml", requestXml)
                                )
                            )
                        )
                    )
                );
            }
        }

        private static async Task<XDocument> SendHttpRequestAsync(XDocument soapRequest) {
            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, uri)) {
                requestMessage.Content = new StringContent(soapRequest.ToString(), Encoding.UTF8, "text/xml");
                requestMessage.Headers.Add("SOAPAction", Action);

                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                string responseContent = await response.Content.ReadAsStringAsync();
                return XDocument.Parse(responseContent);
            }
        }

        private static string ParseSoapResponse(XDocument soapResponse) {
            if (soapResponse == null) {
                throw new ArgumentNullException(nameof(soapResponse), "The remote server returned an unexpected response.");
            }

            if (!soapResponse.Descendants(batchActivationServiceNs + "ResponseXml").Any()) {
                throw new Exception("The remote server returned an unexpected response.");
            }

            try {
                XDocument responseXml = XDocument.Parse(soapResponse.Descendants(batchActivationServiceNs + "ResponseXml").First().Value);

                if (responseXml.Descendants(batchActivationResponseNs + "ErrorCode").Any()) {
                    string errorCodeElement = responseXml.Descendants(batchActivationResponseNs + "ErrorCode").First().Value;

                    switch (errorCodeElement) {
                        case "0x7F":
                            throw new Exception("The Multiple Activation Key has exceeded its limit.");

                        case "0x67":
                            throw new Exception("The product key has been blocked.");

                        case "0x68":
                            throw new Exception("Invalid product key.");

                        case "0x86":
                            throw new Exception("Invalid key type.");

                        case "0x90":
                            throw new Exception("Please check the Installation ID and try again.");

                        default:
                            throw new Exception("The remote server reported an error (" + errorCodeElement + ").");
                    }

                } else if (responseXml.Descendants(batchActivationResponseNs + "ResponseType").Any()) {
                    string responseType = responseXml.Descendants(batchActivationResponseNs + "ResponseType").First().Value;

                    switch (responseType) {
                        case "1":
                            return responseXml.Descendants(batchActivationResponseNs + "CID").First().Value;

                        case "2":
                            return responseXml.Descendants(batchActivationResponseNs + "ActivationRemaining").First().Value;

                        default:
                            throw new Exception("The remote server returned an unrecognized response.");
                    }

                } else {
                    throw new Exception("The remote server returned an unrecognized response.");
                }

            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }
        }
    }
}