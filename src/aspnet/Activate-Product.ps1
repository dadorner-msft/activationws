<#
.SYNOPSIS
    To install and activate or to uninstall a product key

.DESCRIPTION
    This script can be used to install and activate or to uninstall a Multiple Activation Key (MAK)

    Exit Codes:  0   Success
                 1   Unknown error
                 10  Product key has failed to install
                 11  Product could not be found
                 13  Product has failed to activate
                 15  Product is not supported for activation
                 20  Number of maximum connection retries reached
                 21  Exception calling 'Invoke-WebService'
                 30  Exception calling 'Write-Log'
                 50  Product key has failed to uninstall

.PARAMETER ProductKey
    Specifies the product key.

.PARAMETER WebServiceUrl
    Specifies the URL of the ActivationWs web service.

.PARAMETER MaximumRetryCount
    Specifies the number of connection retries if the ActivationWs web service cannot be contacted.
    Default is 3 retries.

.PARAMETER RetryIntervalSec
    Specifies the interval in seconds between retries for the connection when a failure is received.
    Default is 30 seconds.

.PARAMETER Uninstall
    Specifies that the product key should be uninstalled.

.PARAMETER LogFile
    Specifies the full path to the log file.

.EXAMPLE
    .\Activate-Product.ps1 -ProductKey XXXXX-XXXXX-XXXXX-XXXXX-XXXXX -WebServiceUrl http://client1:8081/activationws.asmx -MaximumRetryCount 5 -RetryIntervalSec 40

.EXAMPLE
    .\Activate-Product.ps1 -ProductKey XXXXX-XXXXX-XXXXX-XXXXX-XXXXX -Uninstall -LogFile C:\logs\activation.log

.NOTES
    Filename:    Activate-Product.ps1
    Version:     0.18.2
    Author:      Daniel Dorner
    Date:        09/29/2020

    This  script  code  is  provided  "as  is",  with  no guarantee or warranty
    concerning the usability or impact on systems and may be used, distributed,
    and  modified  in  any  way  provided the parties agree and acknowledge the
    Microsoft   or   Microsoft   Partners   have  neither   accountability   or
    responsibility for results produced by use of this script.

.LINK
    https://github.com/dadorner-msft/activationws
#>

[CmdletBinding(DefaultParameterSetName = 'install')]
param
(
	[Parameter(Mandatory = $true,
		ValueFromPipelineByPropertyName = $true,
		HelpMessage = 'Please enter the product key. It is a 25-character code and looks like this: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX')]
	[ValidatePattern('^([A-Z0-9]{5}-){4}[A-Z0-9]{5}$')]
	[string]$ProductKey,

	[Parameter(ParameterSetName = 'install',
		Mandatory = $true,
		ValueFromPipelineByPropertyName = $true,
		HelpMessage = 'Please enter the URL of the ActivationWs web service, eg. "https://server.domain.name/ActivationWs.asmx"')]
	[ValidateScript({
		$uri = [uri]$_
		if($uri.Scheme -match '^https?$' -and $uri.AbsoluteURI.EndsWith(".asmx")) {
			return $true
		} else {
			throw 'Please enter the URL of the ActivationWs web service, eg. "https://server.domain.name/ActivationWs.asmx"'
		}
	})]
	[string]$WebServiceUrl,

	[Parameter(ParameterSetName = 'install',
		ValueFromPipelineByPropertyName = $true)]
	[ValidateRange(0, 2147483647)]
	[int]$MaximumRetryCount = 3,

	[Parameter(ParameterSetName = 'install',
		ValueFromPipelineByPropertyName = $true)]
	[ValidateRange(0, 2147483647)]
	[int]$RetryIntervalSec = 30,

	[Parameter(ParameterSetName = 'uninstall')]
	[switch]$Uninstall,

	[Parameter(ValueFromPipelineByPropertyName = $true)]
	[ValidateNotNullOrEmpty()]
	[string]$LogFile = "$env:TEMP\Activate-Product.log"
)

$script:scriptVersion = "0.18.1"
$script:fullyQualifiedHostName = [System.Net.Dns]::GetHostByName($env:COMPUTERNAME).HostName
$script:logInitialized = $false

function Write-Log {
	[CmdletBinding()]
	param (
		[AllowEmptyString()]
		[string[]]$Message
	)

	$timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

	try {
		if (-not $script:logInitialized) {
			"{0}; <---- Starting {1} on host {2}  ---->" -f $timestamp, $MyInvocation.ScriptName, $script:fullyQualifiedHostName | Out-File -FilePath $LogFile -Append -Force
			"{0}; {1} version: {2}" -f $timestamp, $script:MyInvocation.MyCommand.Name, $script:scriptVersion | Out-File -FilePath $LogFile -Append -Force
			"{0}; Initialized logging at {1}" -f $timestamp, $LogFile | Out-File -FilePath $LogFile -Append -Force

			$script:logInitialized = $true
		}

		foreach ($line in $Message) {
			Write-Host $line
			$line = "{0}; {1}" -f $timestamp, $line
			$line | Out-File -FilePath $LogFile -Append -Force
		}

	} catch [System.IO.DirectoryNotFoundException], [System.UnauthorizedAccessException], [System.IO.IOException], [System.Management.Automation.DriveNotFoundException] {
		$script:LogFile = "$env:TEMP\Activate-Product.log"
		Write-Host "[Warning] $_ The output would be redirected to `'$LogFile`'."

	} catch {
		Write-Host  "[Error] Exception calling 'Write-Log': $_"
		Exit 30
	}
}

function Install-ProductKey {
	[CmdletBinding()]
	param (
		[string]$ProductKey
	)

	$partialProductKey = $ProductKey.Substring($ProductKey.Length - 5)
	$licensingProduct = Get-WmiObject -Query ('SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey = "{0}"' -f $partialProductKey)

	# Check if product key is installed and activated.
	if ($licensingProduct) {
		# Product key is installed.
		if ($licensingProduct.LicenseStatus -eq 1) {
			# Product is activated.
			Write-Log -Message "The product with product key '$ProductKey' is already activated."
			Exit 0
		}

		# Product is not activated.
		Write-Log -Message "The product with product key '$ProductKey' is installed but not activated."

	} else {
		# Product key is not installed.
		$licensingService = Get-WmiObject -Query ('SELECT Version FROM SoftwareLicensingService')
		Write-Log -Message "The Software Licensing Service version is '$($licensingService.Version)'."

		# Install product key.
		Write-Log -Message "Installing product key '$ProductKey'..."

		try {
			$null = $licensingService.InstallProductKey($ProductKey)

		} catch [System.Runtime.InteropServices.COMException] {
			$errorCode = $_.Exception.ErrorCode

			switch ($errorCode) {
				'-1073418203' {
					Write-Log -Message "[Error] The action requires administrator privileges."
					break
				}
				'-1073418160' {
					Write-Log -Message "[Error] The product key is invalid."
					break
				}
				Default {
					Write-Log -Message "[Error] The product key has failed to install ($errorCode)."
				}
			}

			Exit 10

		} catch {
			Write-Log -Message "[Error] The product key has failed to install: $_"
			Exit 10
		}

	}

	# Activate product
	Enable-Product -PartialProductKey $partialProductKey

	Update-LicenseStatus
}

function Uninstall-ProductKey {
	[CmdletBinding()]
	param (
		[string]$ProductKey
	)

	Write-Log -Message "Uninstalling product key '$ProductKey'..."

	# Retrieve product information.
	Write-Log -Message "Retrieving product information..."
	$partialProductKey = $ProductKey.Substring($ProductKey.Length - 5)
	$licensingProduct = $null
	$licensingProduct = Get-WmiObject -Query ('SELECT ID FROM SoftwareLicensingProduct WHERE PartialProductKey = "{0}"' -f $partialProductKey)

	if (-not $licensingProduct) {
		Write-Log -Message "[Error] The product could not be found."
		Exit 11
	}

	try {
		# Uninstall product key.
		$null = $licensingProduct.UninstallProductKey()

	} catch [System.Runtime.InteropServices.COMException] {
		$errorCode = $_.Exception.ErrorCode

		switch ($errorCode) {
			'-1073418203' {
				Write-Log -Message "[Error] The action requires administrator privileges."
				break
			}
			Default {
				Write-Log -Message "[Error] The product key has failed to uninstall ($errorCode)."
			}
		}

		Exit 50

	} catch {
		Write-Log -Message "[Error] The product key has failed to uninstall: $_"
		Exit 50
	}

	Write-Log -Message "The product key has been successfully uninstalled."
	Update-LicenseStatus
	Exit 0
}

function Update-LicenseStatus {

	$licensingService = $null
	$licensingService = Get-WmiObject -Query ('SELECT Version FROM SoftwareLicensingService')

	# Refresh Windows licensing state.
	try {
		$null = $licensingService.RefreshLicenseStatus()

	} catch {
	}
}

function Enable-Product {
	[CmdletBinding()]
	param (
		[string]$PartialProductKey
	)

	# Retrieve product information.
	Write-Log -Message "Retrieving product information..."
	$licensingProduct = $null
	$licensingProduct = Get-WmiObject -Query ('SELECT ID, Name, Description, OfflineInstallationId, ProductKeyID FROM SoftwareLicensingProduct WHERE PartialProductKey = "{0}"' -f $partialProductKey)

	if (-not $licensingProduct) {
		Write-Log -Message "[Error] The product with product key '$ProductKey' could not be found."
		Exit 11
	}

	if ($licensingProduct.Description.Contains("KMS")) {
		Write-Log -Message "[Error] The product '$($licensingProduct.Description)' is not supported for activation."
		Update-LicenseStatus
		Exit 14
	}

	Write-Log -Message "Name             : $($licensingProduct.Name)"
	Write-Log -Message "Description      : $($licensingProduct.Description)"
	Write-Log -Message "Installation ID  : $($licensingProduct.OfflineInstallationId)"
	Write-Log -Message "Activation ID    : $($licensingProduct.ID)"
	Write-Log -Message "Extd. Product ID : $($licensingProduct.ProductKeyID)"

	# Retrieve the Confirmation ID from ActivationWs web service.
	$confirmationId = Invoke-WebService -WebServiceUrl $WebServiceUrl -InstallationId $licensingProduct.OfflineInstallationId -ExtendedProductId $licensingProduct.ProductKeyID
	Write-Log -Message "Confirmation ID  : $confirmationId"

	# Activate the product by depositing the Confirmation ID.
	Write-Log -Message "Activating product..."

	try {
		$null = $licensingProduct.DepositOfflineConfirmationId($licensingProduct.OfflineInstallationId, $confirmationId)

	} catch [System.Runtime.InteropServices.COMException] {
		$errorCode = $_.Exception.ErrorCode

		switch ($errorCode) {
			'-1073418203' {
				Write-Log -Message "[Error] The action requires administrator privileges."
				break
			}
			'-1073418163' {
				Write-Log -Message "[Error] The Installation ID (IID) or the Confirmation ID (CID) is invalid."
				break
			}
			'-1073418191' {
				Write-Log -Message "[Error] The Installation ID (IID) and the Confirmation ID (CID) do not match."
				break
			}
			Default {
				Write-Log -Message "[Error] Failed to deposit the Confirmation ID. The product was not activated ($errorCode)."
			}
		}

		Exit 13

	} catch {
		Write-Log -Message "[Error] Failed to deposit the Confirmation ID. The product was not activated: $_"
		Exit 13
	}

	Update-LicenseStatus

	# Check if the activation was successful.
	$licensingProduct = Get-WmiObject -Query ('SELECT LicenseStatus, LicenseStatusReason FROM SoftwareLicensingProduct WHERE PartialProductKey = "{0}"' -f $partialProductKey)

	if (-not $licensingProduct.LicenseStatus -eq 1) {
		Write-Log -Message "[Error] The product has failed to activate ($($licensingProduct.LicenseStatusReason))."
		Exit 13
	}

	Write-Log -Message "The product has been successfully activated."
}

function Invoke-WebService {
	[CmdletBinding()]
	param(
		[string]$WebServiceUrl,
		[string]$InstallationId,
		[string]$ExtendedProductId
	)

	Write-Log -Message "Sending an activation request to $WebServiceUrl..."

	$soapEnvelopeDocument = [xml]@"
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
	<soap:Body>
	<AcquireConfirmationId xmlns="http://tempuri.org/">
		<installationId>$InstallationId</installationId>
		<extendedProductId>$ExtendedProductId</extendedProductId>
	</AcquireConfirmationId>
	</soap:Body>
</soap:Envelope>
"@

	[bool]$requestSucceeded = $false
	[int]$numberOfRetries = 0
	# Connect to the ActivationWs web service.
	while (-not $requestSucceeded) {
		try {
			if ($numberOfRetries -le $MaximumRetryCount) {
				$webRequest = [System.Net.WebRequest]::Create($WebServiceUrl)
				$webRequest.Accept = "text/xml"
				$webRequest.ContentType = "text/xml;charset=`"utf-8`""
				$webRequest.Headers.Add("SOAPAction", "`"http://tempuri.org/AcquireConfirmationId`"")
				$webRequest.Method = "POST"
				$webRequest.UserAgent = "PowerShell/{0} ({1}) {2}/{3}" -f $PSVersionTable.PSVersion, $script:fullyQualifiedHostName, $script:MyInvocation.MyCommand.Name, $script:scriptVersion

				$requestStream = $webRequest.GetRequestStream()
				$soapEnvelopeDocument.Save($requestStream)
				$requestStream.Close()
				$response = $webRequest.GetResponse()

				Write-Log -Message "Response status: $([int]$response.StatusCode) - $($response.StatusCode)"

				$responseStream = $response.GetResponseStream()
				$soapReader = [System.IO.StreamReader]($responseStream)
				$responseXml = [xml]$soapReader.ReadToEnd()
				$responseStream.Close()

				$requestSucceeded = $true

			} else {
				# The maximum number of connection retries was reached. Stop the script execution.
				Write-Log -Message "[Error] Number of maximum connection retries reached. The execution of this script will be stopped."
				Exit 20
			}

		} catch [System.Net.WebException] {
			# The ActivationWs web service could not be contacted or returned an unexpected response.
			Write-Log -Message "[Warning] $_"

			$numberOfRetries ++
			if ($numberOfRetries -le $MaximumRetryCount) {
				Write-Log -Message "Connection to web service will be retried in $RetryIntervalSec seconds ($numberOfRetries/$MaximumRetryCount)..."
				# Suspend the activity before the connection is retried.
				Start-Sleep $RetryIntervalSec
			}

		} catch {
			Write-Log -Message "[Error] Exception calling 'Invoke-WebService': $_"
			Exit 21
		}
	}

	# Return Confirmation ID.
	return $responseXml.Envelope.Body.AcquireConfirmationIdResponse.InnerText
}

if ($Uninstall) {
	# Uninstall product key.
	Uninstall-ProductKey -ProductKey $ProductKey

} else {
	# Install product key.
	Install-ProductKey -ProductKey $ProductKey
}