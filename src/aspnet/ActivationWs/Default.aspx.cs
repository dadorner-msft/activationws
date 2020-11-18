using System;

namespace ActivationWs
{
    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e) {
            if (RadioButtonList1.SelectedIndex == 0) {
                InstallationId.Enabled = true;

            } else {
                InstallationId.Enabled = false;
            }
        }

        protected void Submit_Click(Object sender, EventArgs E) {
            try {
                if (RadioButtonList1.SelectedIndex == 0 && !((string.IsNullOrWhiteSpace(InstallationId.Text)) || (string.IsNullOrWhiteSpace(ExtendedProductId.Text)))) {
                    Result.Text = string.Format("The Confirmation ID is: <b>{0}</b>", ActivationHelper.CallWebService(1, InstallationId.Text, ExtendedProductId.Text));

                } else if (RadioButtonList1.SelectedIndex == 1 && !(string.IsNullOrWhiteSpace(ExtendedProductId.Text))) {
                    Result.Text = string.Format("You have <b>{0}</b> activations left.", ActivationHelper.CallWebService(2, null, ExtendedProductId.Text));

                } else {
                    Result.Text = "Please fill in all required fields and try again.";
                }

            } catch (Exception ex) {
                Result.Text = string.Format("The data could not be retrieved. {0}.", ex.Message);
            }
        }
    }
}