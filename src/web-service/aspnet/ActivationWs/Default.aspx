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
    <p>Use this page to perform a manual MAK activation or to check your remaining MAK activation count.</p>
    <form id="form1" runat="server">
        <asp:RadioButtonList ID="RadioButtonList1"
            AutoPostBack="True"
            RepeatDirection="Horizontal"
            runat="server">
            <asp:ListItem Selected="True">Acquire Confirmation ID</asp:ListItem>
            <asp:ListItem>Retrieve remaining activation count</asp:ListItem>
        </asp:RadioButtonList>
        <br />
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
                        ForeColor="Red"
                        SetFocusOnError="True"
                        ValidationExpression="^[0-9]{54,}$">
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
                        ForeColor="Red"
                        SetFocusOnError="True"
                        ValidationExpression="^[0-9]{5}-[0-9]{5}-[0-9]{3}-[0-9]{6}-[0-9]{2}-[0-9]{4}-[0-9]{4,5}.[0-9]{4}-[0-9]{7}$">
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
    <p class="footer"><a href="https://github.com/dadorner-msft/ActivationWs/releases" target="_blank">Find ActivationWs on GitHub</a>&nbsp;&nbsp;&#124;&nbsp;&nbsp; Version <%= typeof(ActivationHelper).Assembly.GetName().Version.ToString() %></p>
</body>
</html>