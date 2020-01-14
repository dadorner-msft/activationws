# ActivationWs

# Table of Contents
<!-- toc -->
- [Introduction](#introduction)
- [Requirements](#requirements)
- [Installation and Usage](#installation-and-usage)
- [FAQ](#faq)
- [Contributions are welcome](#contributions-are-welcome)
- [Code of Conduct](#code-of-conduct)
- [Disclaimer](#disclaimer)

## Introduction

Welcome to the ActivationWs GitHub repository! ActivationWs is a customizable Multiple Activation Key (MAK) distribution and activation solution. It includes an ASP.NET web service and a PowerShell script to install and activate the MAK. 

ActivationWs was designed for organizations who are facing challenges in the deployment and activation of their Extended Security Update (ESU) MAK key. It provides you with a “pull-based” activation solution, eliminates the pre-requisites that VAMT needs and reduces obstacles you could face in the product key activation process.

### How does ActivationWs work and how does it benefit you?

![activation-process](https://github.com/dadorner-msft/ActivationWs/blob/master/doc/images/activation-process.gif) 
 
1. Deploy the PowerShell script `Activate-Product.ps1` to your ESU eligible devices (e.g. using ConfigMgr or another solution of your choice)
2. The script installs the MAK key, queries the Installation ID and Product ID. It then sends a SOAP request to the ActivationWs web service (the ActivationWs web service is installed on a host on your internal network. Communication takes place over a port of your choice, e.g. 80/443)
3. Installation- and Product IDs are sent to the Microsoft BatchActivation Service
4. Confirmation ID is returned to the ActivationWs web service, which will then return the Confirmation ID to the client
5. The script deposits the Confirmation ID and finishes up the activation

[Back to ToC](#table-of-contents)

## Requirements
- ActivationWs web service runs on IIS and requires the .NET Framework 4.6. It also requires access to the Microsoft BatchActivation Service (`https://activation.sls.microsoft.com`). A proxy server can be specified in the web.config file, when necessary.
- `Activate-Product.ps1` requires Windows PowerShell v2.0 or later and needs to be executed with administrative rights.

[Back to ToC](#table-of-contents)

## Installation and Usage

The latest version of this solution can be downloaded from the [ActivationWs GitHub releases page](https://github.com/dadorner-msft/ActivationWs/releases). Click on `Assets` to show the files available in the release.

1. Deploy the ActivationWs web service to IIS
2. Verify that your devices meet the [ESU installation requirements](https://techcommunity.microsoft.com/t5/Windows-IT-Pro-Blog/How-to-get-Extended-Security-Updates-for-eligible-Windows/ba-p/917807)
3. Run the PowerShell script `Activate-Product.ps1` on your clients to install and activate the license

![Activate-License](https://github.com/dadorner-msft/activationws/blob/master/doc/images/Activate-License-v0.15.2.gif)

[Back to ToC](#table-of-contents)

## FAQ

**To which external addresses does ActivationWs web service specifically need access to?**

ActivationWs web service requires access to `https://activation.sls.microsoft.com`

**I'd like to evaluate ActivationWs, however I do not have access to the Extended Security MAK key yet. How can I evaluate ActivationWs beforehand?**

You can evaluate ActivationWs by using your Windows MAK key.

**After successfully deploying the licenses using ActivationWs, how can I verify the deployment of the extended security updates?**

Please take a look at [this blog article](https://techcommunity.microsoft.com/t5/Windows-IT-Pro-Blog/How-to-get-Extended-Security-Updates-for-eligible-Windows/ba-p/917807), which outlines the available updates to verify the deployment.

**Activate-Product.ps1 fails with "[Error] Failed to install the product key."**

- Verify that you meet the ESU requiements, listed here: [How-to-get-Extended-Security-Updates](https://techcommunity.microsoft.com/t5/Windows-IT-Pro-Blog/How-to-get-Extended-Security-Updates-for-eligible-Windows/ba-p/917807)
- Run the PowerShell script `Activate-License.ps1` as administrator
- Check your product key

If it fails even though you followed these steps, please run `slmgr.vbs /ipk <product key>` and check the result.

**Activate-Product.ps1 fails with "[Error] Product activation failed (3221549105)."**

The field test results showed that this error occurs on devices that haven't been connected to the Internet for a while. Please deploy the [latest monthly rollup](https://www.catalog.update.microsoft.com/Search.aspx?q=2020-01%20Security%20Monthly%20Quality%20Rollup) to update the system components to finish up the activation.

**Activate-Product.ps1 fails with "[Warning] The remote server returned an error: (500) Internal Server Error.**

This is a "server-side" error, meaning that the ActivationWs web service couldn't acquire the Confirmation Id. Reasons include:
- The ActivationWs web service can connect to the Microsoft BatchActivation Service
- No MAK activations are left on your product key
- The specified -webServiceUrl parameter is incorrect

**I would love to use ActivationWs, but is it officialy supported by Microsoft?**

Microsoft does not provide technical support for this solution. Please take a look at the [official supported methods](https://techcommunity.microsoft.com/t5/Windows-IT-Pro-Blog/How-to-get-Extended-Security-Updates-for-eligible-Windows/ba-p/917807) for activation.

**We're using SCCM to deploy your script. Is there way to obfuscate or hide the ESU key in the logs?**

You could create a task sequence (TS) variable that contains the MAK. Then modify the PowerShell script `Activate-Product.ps1` to not output the product key and create an instance of a COM object that represents the TS environment to read the variable, e.g.

```powershell
$tsEnv = New-Object -COMObject Microsoft.SMS.TSEnvironment
$productKey = $tsEnv.Value("PKEY")
```
This would prevent the product key from showing up in the ConfigMgr log files.

[Back to ToC](#table-of-contents)

## Contributions are welcome

There are many ways to contribute:

1. Open a new bug report or feature request by opening a new issue [here](https://github.com/dadorner-msft/ActivationWs/issues/new/choose).
2. Participate in the discussions of [issues](https://github.com/dadorner-msft/ActivationWs/issues), [pull requests](https://github.com/dadorner-msft/ActivationWs/pulls) and verify/test fixes or new features.
3. Submit your own fixes or features as a pull request but please discuss it beforehand in an issue if the change is substantial.
4. Submit test cases.

[Back to ToC](#table-of-contents)

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code]. For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/ 
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com

[Back to ToC](#table-of-contents)

## Disclaimer

This script code is provided "as is", with no guarantee or warranty concerning the usability or impact on systems and may be used, distributed, and modified in any way provided the parties agree and acknowledge the Microsoft or Microsoft Partners have neither accountability or responsibility for results produced by use of this script.

Microsoft will not provide any support through any means.

[Back to ToC](#table-of-contents)
