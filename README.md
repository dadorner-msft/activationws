<img src="./doc/images/Logo_color.png" width="400" alt="Trusted by the world’s leading enterprises"/>
Welcome to the ActivationWs GitHub repository!

## Overview
**ActivationWs** is a customizable solution that allows you to automate the Multiple Activation Key (MAK)[^1] activation process for various Microsoft products such as Windows, Office, and Extended Security Update (ESU) add-ons.
[^1]: MAKs are interchangeably referred to as product keys, or license keys

### Key Benefits
- **Automates MAK deployment** for organizations of any size
- **Pull-based activation**: Devices request activation, reducing manual steps and obstacles
- **Customizable and privacy-friendly**: Full source code is available for review and modification
- **Easy to implement**: Integrates with deployment tools like ConfigMgr or custom solutions

---

## How It Works
ActivationWs is made up of two components: 

1. **ASP.NET Core web app**
2. **PowerShell script** (`Activate-Product.ps1`)

The following illustration shows a simplified version of the MAK deployment and product activation process:

![Illustration showing a simplified version of the MAK deployment and product activation process](./doc/images/activation-process.png)

### Activation Flow:
1. The PowerShell script is deployed to your devices using ConfigMgr or a deployment tool of your choice
2. The script installs the MAK and queries the respective Installation- and Product IDs. It then sends the data to the ActivationWs web app. Communication between the device and the ActivationWs web app takes place over a port of your choice, e.g. 80/443
3. Installation- and Product IDs are transmitted to the Microsoft BatchActivation Service
4. A Confirmation ID is subsequently returned to the ActivationWs web app
5. ActivationWs web app returns the Confirmation ID to the device. The script deposits the Confirmation ID and concludes the product activation

---

## Requirements
- The ActivationWs web app needs access to the Microsoft BatchActivation Service (`https://activation.sls.microsoft.com`)
- To host the ActivationWs web app on IIS, install the [ASP.NET Core Module](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0)
- `Activate-Product.ps1` requires Windows PowerShell 3.0 or later and needs to be executed with administrative rights

---

## Installation and Usage
This section highlights some of the most frequent scenarios and guides you through the initial setup.

> [!NOTE]
> For the deployment of ESU MAKs, please ensure that these prerequisites are met on your devices:
> * [Prerequisites for Windows 10](https://learn.microsoft.com/en-us/windows/whats-new/extended-security-updates)
> * [Prerequisites for Windows Server 2012/2012 R2](https://support.microsoft.com/en-us/topic/kb5031043-procedure-to-continue-receiving-security-updates-after-extended-support-has-ended-on-october-10-2023-c1a20132-e34c-402d-96ca-1e785ed51d45)
> * [Prerequisites for Windows 7 and Windows Server 2008/2008 R2](https://techcommunity.microsoft.com/t5/windows-it-pro-blog/obtaining-extended-security-updates-for-eligible-windows-devices/ba-p/1167091#)

### 1. Build and deploy the web app
1. Build the solution (Visual Studio 2022 or later)
2. Deploy the ActivationWs web app to a web server, e.g. IIS

### 2. Activate the product

#### Scenario #1: Automated activation
Deploy `Activate-Product.ps1` to your devices. The following animation demonstrates the MAK installation and activation of an ESU product:

![Animation demonstrating the installation and activation of an ESU product](./doc/images/activate-product-v0.25.1.gif)

#### Scenario #2: Manual activation (air-gapped devices)
1. Open the ActivationWs home page
2. Enter the Hostname, Installation- and Extended Product ID to retrieve the Confirmation ID
3. Activate the product by `slmgr.vbs /atp <Confirmation ID> <Activation ID>`

![Graphic showing the ActivationWS UI](./doc/images/manual-cid-retrieval.png)

---

## FAQ

>[!TIP]
>The following section contains answers to frequently asked questions. Please feel free to file an [issue](https://github.com/dadorner-msft/ActivationWs/issues) or [contact me](https://github.com/login?return_to=https%3A%2F%2Fgithub.com%2Fdadorner-msft) should you have any question or need support.

### To which external addresses does ActivationWs web service specifically need access to?

ActivationWs web service requires access to the URL listed in the [Requirements](#requirements) section.

### Activate-Product.ps1 fails with an error

| Error | How to fix it |
|:---|:---|
| The product key is invalid | - Check your MAK<br>- For the deployment of ESU MAKs only: ensure that all of the [prerequisites](#scenario-1-automated-activation) are installed on your ESU eligible device<br> <br>If it fails even though you followed these steps, please take a look at the following support article: [How to rebuild the Tokens.dat file when you troubleshoot Windows activation issues](https://support.microsoft.com/en-us/help/2736303) |
| The Installation ID (IID) and the Confirmation ID (CID) do not match | For the deployment of ESU MAKs only: ensure that all of the [prerequisites](#scenario-1-automated-activation) are installed on your ESU eligible device |
| (500) Internal Server Error | This is a "server-side" error, meaning that the ActivationWs web service couldn't acquire the Confirmation ID. Reasons include:<br>- The ActivationWs web service couldn't connect to the [required URL](#requirements) |

### No activations are left on my MAK
Please request an increase to MAK activation limits [here](https://learn.microsoft.com/en-us/microsoft-365/commerce/licenses/product-keys-for-vl?view=o365-worldwide#request-an-increase-to-mak-activation-limits).

---

## Contributions are welcome

You don’t have to be a developer to contribute to the project!

There are other ways to join the project including:

1. Writing documentation
2. Creating tutorials
3. Bug reporting and triaging ([here](https://github.com/dadorner-msft/activationws/issues/new/choose))
4. Feature testing

<br />

[Back to Overview](#overview)