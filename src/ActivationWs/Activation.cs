using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;


namespace ActivationWs
{
    public static class ActivationHelper {
        // Key for HMAC/SHA256 signature.
        private static readonly byte[] MacKey = new byte[64] {
            254,  49, 152, 117, 251,  72, 132, 134,
            156, 243, 241, 206, 153, 168, 144, 100,
            171,  87,  31, 202,  71,   4,  80,  88,
            48,   36, 226,  20,  98, 135, 121, 160,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0
        };

        private const string Action = "http://www.microsoft.com/BatchActivationService/BatchActivate";

        private static readonly Uri Uri = new Uri("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");

        private static readonly XNamespace SoapSchemaNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace XmlSchemaInstanceNs = "http://www.w3.org/2001/XMLSchema-instance";
        private static readonly XNamespace XmlSchemaNs = "http://www.w3.org/2001/XMLSchema";
        private static readonly XNamespace BatchActivationServiceNs = "http://www.microsoft.com/BatchActivationService";
        private static readonly XNamespace BatchActivationRequestNs = "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0";
        private static readonly XNamespace BatchActivationResponseNs = "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0";

        public static string CallWebService(int requestType, string installationId, string extendedProductId) {
            XDocument soapRequest = CreateSoapRequest(requestType, installationId, extendedProductId);
            HttpWebRequest webRequest = CreateWebRequest(soapRequest);
            XDocument soapResponse = new XDocument();

            try {
                IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);
                asyncResult.AsyncWaitHandle.WaitOne();

                // Read data from the response stream.
                using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
                using (StreamReader streamReader = new StreamReader(webResponse.GetResponseStream())) {
                    soapResponse = XDocument.Parse(streamReader.ReadToEnd());
                }

            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }

            return ParseSoapResponse(soapResponse);
        }

        private static XDocument CreateSoapRequest(int requestType, string installationId, string extendedProductId) {
            // Create an activation request.           
            XElement activationRequest = new XElement(BatchActivationRequestNs + "ActivationRequest",
                new XElement(BatchActivationRequestNs + "VersionNumber", "2.0"),
                new XElement(BatchActivationRequestNs + "RequestType", requestType),
                new XElement(BatchActivationRequestNs + "Requests",
                    new XElement(BatchActivationRequestNs + "Request",
                        new XElement(BatchActivationRequestNs + "PID", extendedProductId),
                        requestType == 1 ? new XElement(BatchActivationRequestNs + "IID", installationId) : null)
                )
            );

            // Get the unicode byte array of activationRequest and convert it to Base64.
            byte[] bytes = Encoding.Unicode.GetBytes(activationRequest.ToString());
            string requestXml = Convert.ToBase64String(bytes);

            XDocument soapRequest = new XDocument();

            using (HMACSHA256 hMACSHA = new HMACSHA256(MacKey)) {
                // Convert the HMAC hashed data to Base64.
                string digest = Convert.ToBase64String(hMACSHA.ComputeHash(bytes));

                soapRequest = new XDocument(
                new XDeclaration("1.0", "UTF-8", "no"),
                new XElement(SoapSchemaNs + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soap", SoapSchemaNs),
                    new XAttribute(XNamespace.Xmlns + "xsi", XmlSchemaInstanceNs),
                    new XAttribute(XNamespace.Xmlns + "xsd", XmlSchemaNs),
                    new XElement(SoapSchemaNs + "Body",
                        new XElement(BatchActivationServiceNs + "BatchActivate",
                            new XElement(BatchActivationServiceNs + "request",
                                new XElement(BatchActivationServiceNs + "Digest", digest),
                                new XElement(BatchActivationServiceNs + "RequestXml", requestXml)
                            )
                        )
                    )
                ));

            }

            return soapRequest;
        }

        private static HttpWebRequest CreateWebRequest(XDocument soapRequest) {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(Uri);
            webRequest.Accept = "text/xml";
            webRequest.ContentType = "text/xml; charset=\"utf-8\"";
            webRequest.Headers.Add("SOAPAction", Action);
            webRequest.Method = "POST";

            try {
                // Insert SOAP envelope
                using (Stream stream = webRequest.GetRequestStream()) {
                    soapRequest.Save(stream);
                }

            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }

            return webRequest;
        }

        private static string ParseSoapResponse(XDocument soapResponse) {
            try {
                if (soapResponse == null) {
                    throw new ArgumentNullException("The remote server returned an unexpected response.");
                }

                XDocument responseXml = XDocument.Parse(soapResponse.Descendants(BatchActivationServiceNs + "ResponseXml").First().Value);

                if (responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").Any()) {
                    string errorCode = responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").First().Value;

                    switch (errorCode) {
                        case "0x7F":
                            throw new Exception("The Multiple Activation Key has exceeded its limit");

                        case "0x67":
                            throw new Exception("The MAK has been blocked");

                        case "0x86":
                            throw new Exception("Invalid license type.");

                        case "0x90":
                            throw new Exception("Please check the Installation ID and try again");

                        default:
                            throw new Exception(string.Format("The remote server reported an error ({0})", errorCode));
                    }

                } else if (responseXml.Descendants(BatchActivationResponseNs + "ResponseType").Any()) {
                    int responseType = Convert.ToInt32(responseXml.Descendants(BatchActivationResponseNs + "ResponseType").First().Value);

                    switch (responseType) {
                        case 1:
                            string confirmationId = responseXml.Descendants(BatchActivationResponseNs + "CID").First().Value;
                            return confirmationId;

                        case 2:
                            string activationsRemaining = responseXml.Descendants(BatchActivationResponseNs + "ActivationRemaining").First().Value;
                            return activationsRemaining;

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