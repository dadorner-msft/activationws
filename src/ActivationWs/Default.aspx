<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ActivationWs.WebForm" %>
<%@ Import Namespace="ActivationWs" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>ActivationWs</title>
    <style>
        body {
            font-family: "Segoe UI", Verdana, sans-serif; 
            font-size: 14px; 
            margin: 10px;
        }
		a:link { 
            color: #ffffff;
            text-decoration: none;
		}
		a:visited { 
            color: #ffffff;
		}
		a:active { 
            color: #ffffff;
		}
        a:hover {
            color: #ffffff;
        }
        .header {
            color: #ffffff;
            font-size: 26px;
            background-color: #5f9ea0;
            margin-top: 10px;
            margin-bottom: 10px;
            margin-left: -10px;
            margin-right: -10px;
            padding-top: 6px;
            padding-bottom: 2px;
            padding-left: 10px;
        }
        .footer {
            color: #ffffff;
            font-size: 12px;
            background-color: #5f9ea0;
            margin-left: -10px;
            margin-right: -10px;
            padding-left: 10px;
        }
    </style>
</head>
<body>
    <p class="header">ActivationWs</p>
    <p>Use this page if you need to acquire a Confirmation ID from the Microsoft licensing servers.</p>
    <form id="form1" runat="server">
        <p>Acquire a Confirmation ID</p>
        <table>
            <tr>
                <td>Installation ID: </td>
                <td>
                    <asp:TextBox ID="InstallationId" runat="server" Width="480px" TabIndex="1" />
                </td>
                <td>
                    <asp:RegularExpressionValidator ID="RegularExpressionValidatorIId"
                        ControlToValidate="InstallationId" runat="server"
                        ErrorMessage="!"
                        ValidationExpression="^[0-9]{54,}$"
                        ForeColor="Red" >
                    </asp:RegularExpressionValidator>
                </td>
            </tr>
            <tr>
                <td >Extended PID: </td>
                <td>
                    <asp:TextBox ID="ExtendedProductId" runat="server" Width="480px" TabIndex="2" />
                </td>
                <td>
                    <asp:RegularExpressionValidator ID="RegularExpressionValidatorPId"
                        ControlToValidate="ExtendedProductId" runat="server"
                        ErrorMessage="!"
                        ValidationExpression="^[0-9]{5}-[0-9]{5}-[0-9]{3}-[0-9]{6}-[0-9]{2}-[0-9]{4}-[0-9]{4}.[0-9]{4}-[0-9]{7}$"
                        ForeColor="Red" >
                    </asp:RegularExpressionValidator>
                </td>
            </tr>
        </table>
        <br />
        <asp:Label ID="Result" runat="server" />
        <br />
        <br />
        <asp:Button ID="Submit" runat="server" Text="Submit" OnClick="Submit_Click" TabIndex="3" CssClass="button" />
    </form>
    <br />
    <p class="footer">Find ActivationWs on <a href="https://github.com/dadorner-msft/activationws" target="_blank">GitHub</a></p>
    <script runat="server">
        public void Submit_Click(Object sender, EventArgs E) {
            try {
                if(!((InstallationId.Text == "") || (ExtendedProductId.Text == ""))) {
                    ActivationService activationService = new ActivationService();
                    Result.Text = "The Confirmation ID is: <b>" +  activationService.AcquireConfirmationId(InstallationId.Text, ExtendedProductId.Text) + "</b>.";
                } else {
                    Result.Text = "Please enter a Confirmation ID and Extended Product ID.";
                }
            } catch (Exception ex) {
                Result.Text = "The Confirmation ID could not be retrieved (" + ex.Message + ")";
            }
        }
    </script>
</body>
</html>