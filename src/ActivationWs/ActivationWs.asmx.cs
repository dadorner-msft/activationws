using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
    public class ActivationWs : System.Web.Services.WebService
    {

        [WebMethod]
        public string AcquireConfirmationId(string installationId, string extendedProductId) {
            string confirmationId = Activation.CallWebService(installationId, extendedProductId);
            return confirmationId;
        }
    }
}
