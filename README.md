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

Welcome to the ActivationWs GitHub repository!

ActivationWs is a customizable solution that allows you to automate the Multiple Activation Key (MAK) activation process for Windows, Office, and other Microsoft products (eg. Extended Security Update (ESU)).

### How does ActivationWs work and how does it benefit you?

ActivationWs includes an ASP.NET web service and a PowerShell script to install and activate the MAK. The following graphic shows a simplified version of the ESU license deployment and activation process (the deployment and activation process also applies to all other products):

![activation-process](https://github.com/dadorner-msft/ActivationWs/blob/master/doc/images/activation-process.gif) 
 
1. The PowerShell script `Activate-Product.ps1` is deployed to your devices (eg. using ConfigMgr or another solution of your choice)
2. The script installs the MAK, queries the Installation- and Product ID. It then sends a SOAP request to the ActivationWs web service (the ActivationWs web service is installed onto a host in your internal network. Communication takes place over a port of your choice, eg. 80/443)
3. Installation- and Product IDs are sent to the Microsoft BatchActivation Service
4. Confirmation ID is returned to the ActivationWs web service, which will then return the Confirmation ID to the device
5. The script deposits the Confirmation ID and concludes the activation

#### Benefits:
- Helps organizations of any size with the deployment of MAKs
- Provides a pull-based activation solution and reduces obstacles faced during the product key activation
- Easy to implement, time-saving, allows you to ensure business goals are realized, manages risks and delivers business value
- Customizable and addresses privacy concerns, given the fact that the source code is available to the public

[Back to ToC](#table-of-contents)

## Requirements
- ActivationWs web service runs on IIS and requires the .NET Framework 4.6 and ASP.NET modules
- The web service requires access to the Microsoft BatchActivation Service (`https://activation.sls.microsoft.com`). A proxy server can be specified in the web.config file, where necessary
- `Activate-Product.ps1` requires Windows PowerShell v2.0 or later and needs to be executed with administrative rights

[Back to ToC](#table-of-contents)

## Installation and Usage

The latest version of this solution can be downloaded from the [ActivationWs GitHub releases page](https://github.com/dadorner-msft/ActivationWs/releases). Click on `Assets` to show the files available in the release.

1. Deploy the ActivationWs web service to IIS
2. For the deployment of ESU licenses only: please ensure that all of the [prerequisites](https://techcommunity.microsoft.com/t5/windows-it-pro-blog/obtaining-extended-security-updates-for-eligible-windows-devices/ba-p/1167091#) are installed on your ESU eligible devices
3. Deploy the PowerShell script `Activate-Product.ps1` to all relevant devices to install and activate the license

![activate-product](https://github.com/dadorner-msft/activationws/blob/master/doc/images/Activate-License-v0.15.2.gif)

### Manual Confirmation ID retrieval

ActivationWs also supports you in the activation process of air-gapped devices.

1. Open the ActivationWs site
2. Enter the Installation- and Product ID to retrieve the corresponding Confirmation ID
3. Activate the product by `slmgr.vbs /atp <Confirmation ID> <Activation ID>`

![manual-cid-retrieval](https://github.com/dadorner-msft/activationws/blob/master/doc/images/manual-cid-retrieval.png)

[Back to ToC](#table-of-contents)

## FAQ

The following section contains answers to frequently asked questions. Please feel free to [contact me](https://github.com/login?return_to=https%3A%2F%2Fgithub.com%2Fdadorner-msft) should you have any question or need support.

**To which external addresses does ActivationWs web service specifically need access to?**

ActivationWs web service requires access to the URL listed in the [requirement](#requirements) section.

**After successfully deploying the licenses using ActivationWs, how can I verify the deployment of the extended security updates?**

Please take a look at [this blog article](https://techcommunity.microsoft.com/t5/windows-it-pro-blog/obtaining-extended-security-updates-for-eligible-windows-devices/ba-p/1167091#), which outlines the available updates to verify the deployment.

**Activate-Product.ps1 fails with "[Error] The product key is invalid"**

- Check your product key
- For the deployment of ESU licenses only: ensure that all of the [prerequisites](https://techcommunity.microsoft.com/t5/windows-it-pro-blog/obtaining-extended-security-updates-for-eligible-windows-devices/ba-p/1167091#) are installed on your ESU eligible device

If it fails even though you followed these steps, please take a look at the following support article: [How to rebuild the Tokens.dat file when you troubleshoot Windows activation issues](https://support.microsoft.com/en-us/help/2736303).

**Activate-Product.ps1 fails with "[Error] The Installation ID (IID) and the Confirmation ID (CID) do not match" or "[Error] Product activation failed (3221549105)"**

For the deployment of ESU licenses only: ensure that all of the [prerequisites](https://techcommunity.microsoft.com/t5/windows-it-pro-blog/obtaining-extended-security-updates-for-eligible-windows-devices/ba-p/1167091#) are installed on your ESU eligible device

**Activate-Product.ps1 fails with "[Warning] The remote server returned an error: (500) Internal Server Error"**

This is a "server-side" error, meaning that the ActivationWs web service couldn't acquire the Confirmation Id. Reasons include:
- The ActivationWs web service couldn't connect to the [required URL](#requirements)
- No MAK activations are left on your product key
- The specified WebServiceUrl is incorrect

**We're using SCCM to deploy your script. Is there way to obfuscate or hide the ESU key in the logs?**

You could create a task sequence (TS) variable that contains the MAK. Then modify the PowerShell script `Activate-Product.ps1` to not output the product key and create an instance of a COM object that represents the TS environment to read the variable, eg.

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

[Back to ToC](#table-of-contents)
