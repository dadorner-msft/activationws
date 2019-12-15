using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;

namespace ActivationWs
{
    public class Activation
    {
        // Key for HMAC/SHA256 signature
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

        public static string CallWebService(string installationId, string extendedProductId) {
            string url = "https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx";
            string action = "http://www.microsoft.com/BatchActivationService/BatchActivate";

            XmlDocument soapEnvelopeXml = CreateSoapEnvelope(installationId, extendedProductId);
            HttpWebRequest webRequest = CreateWebRequest(url, action);
            InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml, webRequest);

            // Issue the async request
            IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);

            // Wait until after the callback is called
            asyncResult.AsyncWaitHandle.WaitOne();

            // Read data from the response stream
            string soapResult;

            try {
                using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
                using (StreamReader streamReader = new StreamReader(webResponse.GetResponseStream())) {
                    soapResult = streamReader.ReadToEnd();
                }

            } catch (Exception ex) {
                throw new Exception("Exception calling 'CallWebservice': " + ex.Message);
            }

            return ParseSoapResult(soapResult);
        }

        public static string ParseSoapResult(string soapResult) {

            // Parse the response stream
            try {
                string cid;
                XmlReader xmlReader;

                using (xmlReader = XmlReader.Create(new StringReader(soapResult))) {
                    xmlReader.ReadToFollowing("ResponseXml");
                    string responseXml = xmlReader.ReadElementContentAsString();

                    responseXml = responseXml.Replace("utf-16", "utf-8");
                    responseXml = responseXml.Replace("&lt;", "<");
                    responseXml = responseXml.Replace("&gt;", ">");

                    using (xmlReader = XmlReader.Create(new StringReader(responseXml))) {
                        xmlReader.ReadToFollowing("CID");
                        cid = xmlReader.ReadElementContentAsString();
                    }
                }

                return cid;
            } catch (Exception ex) {
                throw new Exception("Exception calling 'ParseSoapResult': " + ex.Message);
            }
        }

        private static HttpWebRequest CreateWebRequest(string url, string action) {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Method = "POST";
            webRequest.Accept = "text/xml";
            webRequest.ContentType = "text/xml; charset=\"utf-8\"";
            webRequest.Headers.Add("SOAPAction", action);

            return webRequest;
        }

        private static XmlDocument CreateSoapEnvelope(string installationId, string extendedProductId) {
            XmlDocument soapEnvelopeDocument = new XmlDocument();

            // Create new XML Document
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;

            xmlDoc.LoadXml("<ActivationRequest xmlns=\"http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0\">" +
                "<VersionNumber>2.0</VersionNumber>" +
                "<RequestType>1</RequestType>" +
                "<Requests>" +
                "<Request>" +
                "<PID>" + extendedProductId + "</PID>" +
                "<IID>" + installationId + "</IID>" +
                "</Request>" +
                "</Requests>" +
                "</ActivationRequest>"
            );

            // Get the unicode byte array of xmlDoc and convert it to Base64
            byte[] bytes = Encoding.Unicode.GetBytes(xmlDoc.InnerXml);
            string requestXml = Convert.ToBase64String(bytes);

            HMACSHA256 hMACSHA;
            using (hMACSHA = new HMACSHA256()) {
                hMACSHA.Key = macKey;
                // Compute digest of the Base64 XML bytes
                // Create Base64 hash using HMAC/SHA256
                string digest = Convert.ToBase64String(hMACSHA.ComputeHash(bytes));
                soapEnvelopeDocument.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                    "<soap:Body>" +
                    "<BatchActivate xmlns=\"http://www.microsoft.com/BatchActivationService\">" +
                    "<request>" +
                    "<Digest>" + digest + "</Digest>" +
                    "<RequestXml>" + requestXml + "</RequestXml>" +
                    "</request>" +
                    "</BatchActivate>" +
                    "</soap:Body>" +
                    "</soap:Envelope>"
                );
            }

            return soapEnvelopeDocument;
        }

        private static void InsertSoapEnvelopeIntoWebRequest(XmlDocument soapEnvelopeXml, HttpWebRequest webRequest) {
            try {
                using (Stream stream = webRequest.GetRequestStream()) {
                    soapEnvelopeXml.Save(stream);
                }
            } catch (Exception ex) {
                throw new Exception("Exception calling 'InsertSoapEnvelopeIntoWebRequest': " + ex.Message);
            }
        }
    }
}