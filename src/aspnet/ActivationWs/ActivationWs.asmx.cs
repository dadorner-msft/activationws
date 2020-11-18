using System;
using System.Web.Services;

namespace ActivationWs
{
    /// <summary>
    /// Summary description for ActivationWs
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class ActivationService : System.Web.Services.WebService
    {

        [WebMethod]
        public string AcquireConfirmationId(string installationId, string extendedProductId) {
            string confirmationId = ActivationHelper.CallWebService(1, installationId, extendedProductId);
            return confirmationId;
        }

        [WebMethod]
        public string RetrieveActivationCount(string extendedProductId) {
            string remainingActivationCount = ActivationHelper.CallWebService(2, null, extendedProductId);
            return remainingActivationCount;
        }

    }
}