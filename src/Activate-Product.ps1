#Requires -RunAsAdministrator

<#
========================================================================
	File:		Activate-Product.ps1
	Version:	0.15.2
	Author:		Daniel Dorner
	Date:		12/15/2019
	
	Purpose:	Installs and activates a product key
	
	Usage:		./Activate-Product.ps1 <Required Parameter> [Optional Parameter]
				
                <-ProductKey>          <Specifies the product key>
                <-WebServiceUrl>       <Specifies the URL to the ActivationWs web service>
                [-LogFile]             [Specifies the full path to the log file]
                [-MaximumRetryCount]   [Specifies the number of connection retries if the ActivationWs web service cannot be contacted]
                [-RetryIntervalSec]    [Specifies the interval in seconds between retries for the connection when a failure is received]

	This script code is provided as is with no guarantee or waranty concerning
	the usability or impact on systems and may be used, distributed, and
	modified in any way provided the parties agree and acknowledge the 
	Microsoft or Microsoft Partners have neither accountabilty or 
	responsibility for results produced by use of this script.
	
	Microsoft will not provide any support through any means.

========================================================================
#>

param (
	[Parameter(
		Mandatory = $true,
		ValueFromPipeline = $true,
		HelpMessage = 'Specifies the product key. It is a 25-character code and looks like this: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX',
		Position = 0)]
	[ValidatePattern('^([A-Z0-9]{5}-){4}[A-Z0-9]{5}$')]
	[string]$ProductKey,

	[Parameter(
		Mandatory = $true,
		ValueFromPipeline = $true,
		HelpMessage = 'Specifies the URL to the ActivationWs web service, e.g. "https://server.domain.name/ActivationWs.asmx"',
	    Position = 1)]
	[ValidateNotNullorEmpty()]
	[string]$WebServiceUrl,

	[Parameter(
		Mandatory = $false,
		ValueFromPipeline = $true,
		HelpMessage = 'Specifies the full path to the log file, e.g. "C:\Log\Logfile.log"',
		Position = 2)]
	[ValidateNotNullorEmpty()]
	[string]$LogFile = "$env:TEMP\Activate-Product.log",
	
	[Parameter(
		Mandatory = $false,
		ValueFromPipeline = $true,
		HelpMessage = 'Specifies the number of connection retries if the ActivationWs web service cannot be contacted, e.g. "5"',
		Position = 3)]
	[ValidateNotNullorEmpty()]
	[int]$MaximumRetryCount = 3,
	
	[Parameter(
		Mandatory = $false,
		ValueFromPipeline = $true,
		HelpMessage = 'Specifies the interval in seconds between retries for the connection when a failure is received, e.g. "30"',
		Position = 4)]
	[ValidateNotNullorEmpty()]
	[int]$RetryIntervalSec = 30
)

function LogAndConsole($Message)
{
	try {
		if (!$logInitialized) {
			"{0}; <---- Starting {1} on host {2}  ---->" -f (Get-Date), $MyInvocation.ScriptName, $env:COMPUTERNAME | Out-File -FilePath $LogFile -Append -Force
			"{0}; {1} version: {2}" -f (Get-Date), $script:MyInvocation.MyCommand.Name, $scriptVersion | Out-File -FilePath $LogFile -Append -Force
			"{0}; Initialized logging at {1}" -f (Get-Date), $LogFile | Out-File -FilePath $LogFile -Append -Force
			
			$script:logInitialized = $true
		}
		
		foreach ($line in $Message) {
			$line = "{0}; {1}" -f (Get-Date), $line
			$line | Out-File -FilePath $LogFile -Append -Force
		}
		
		Write-Host $Message
		
	} catch [System.IO.DirectoryNotFoundException] {
		$script:LogFile = "$env:TEMP\Activate-Product.log"
		Write-Host "[Warning] Could not find a part of the path $LogFile. The output would be redirected to $LogFile." 
		
	} catch [System.UnauthorizedAccessException] {
		$script:LogFile = "$env:TEMP\Activate-Product.log"
		Write-Host "[Warning] Access to the path $LogFile is denied. The output would be redirected to $LogFile."
		
	} catch {
		Write-Host  "[Error] Exception calling 'LogAndConsole':" $_.Exception.Message
		Exit $MyInvocation.ScriptLineNumber
	}
}

function CallWebService {
	param(
		[string]$WebServiceUrl, 
		[string]$InstallationId, 
		[string]$ExtendedProductId
	)

	LogAndConsole "Sending an activation request to $WebServiceUrl..."
	
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
				$webRequest.Headers.Add("SOAPAction","`"http://tempuri.org/AcquireConfirmationId`"")
				
				$webRequest.ContentType = "text/xml;charset=`"utf-8`"" 
				$webRequest.Accept      = "text/xml" 
				$webRequest.Method      = "POST" 
				
				$requestStream = $webRequest.GetRequestStream() 
				$soapEnvelopeDocument.Save($requestStream) 
				$requestStream.Close()
				$response = $webRequest.GetResponse()
				$requestSucceeded = $true
				
				LogAndConsole "Response status: $([int]$response.StatusCode) - $($response.StatusCode)"
				
				$responseStream = $response.GetResponseStream() 
				$soapReader = [System.IO.StreamReader]($responseStream) 
				$responseXml = [xml]$soapReader.ReadToEnd()
				$responseStream.Close()
				
				$requestSucceeded = $true
				
			} else {
				# The maximum number of connection retries was reached. Stop the script execution.
				LogAndConsole "[Error] Number of maximum connection retries reached. The execution of this script will be stopped."
				Exit $MyInvocation.ScriptLineNumber
			}
		} catch [System.Net.WebException] {
			# The ActivationWs web service could not be contacted. Reasons include:
			# The remote host could not be resolved; Unable to connect to the remote host; HTTP errors.
			$exMessage = $_.Exception.Message
			LogAndConsole "[Warning] $exMessage"
			
			$numberOfRetries ++
			if ($numberOfRetries -le $MaximumRetryCount) {
				LogAndConsole "Connection to web service will be retried in $RetryIntervalSec seconds ($numberOfRetries/$MaximumRetryCount)..."
				# Suspend the activity before the connection is retried.
				Start-Sleep $RetryIntervalSec
				
			}
		} catch {
			$exMessage = $_.Exception.Message
			LogAndConsole "[Error] Exception calling 'CallWebService': $exMessage"
			Exit $MyInvocation.ScriptLineNumber
		}
	}

	LogAndConsole "Confirmation ID retrieved."
	
	# Return Confirmation ID.
	return $responseXml.Envelope.Body.AcquireConfirmationIdResponse.InnerText
}

function InstallAndActivateProductKey([string]$ProductKey) {

	try {
		# Check if product key is already installed and activated.
		$partialProductKey = $ProductKey.Substring($ProductKey.Length - 5)
		$licensingProduct = Get-WmiObject -Query ('SELECT LicenseStatus FROM SoftwareLicensingProduct where PartialProductKey = "{0}"' -f $partialProductKey)
		
		if ($licensingProduct.LicenseStatus -eq 1) {
			LogAndConsole "The product is already activated."
			Exit $MyInvocation.ScriptLineNumber
		}
	
		# Install the product key.
		LogAndConsole "Installing product key $ProductKey ..."
		$licensingService = Get-WmiObject -Query 'SELECT * FROM SoftwareLicensingService'
		$licensingService.InstallProductKey($ProductKey) | Out-Null
		$licensingService.RefreshLicenseStatus() | Out-Null

	} catch {
		LogAndConsole "[Error] Failed to install the product key."
		Exit $MyInvocation.ScriptLineNumber
	}

	try {
		# Get the licensing information.
		LogAndConsole "Retrieving license information..."
		$licensingProduct = Get-WmiObject -Query ('SELECT ID, Name, OfflineInstallationId, ProductKeyID FROM SoftwareLicensingProduct where PartialProductKey = "{0}"' -f $partialProductKey)

		if(!$licensingProduct) {
			LogAndConsole "No license information for product key $ProductKey was found."
			Exit $MyInvocation.ScriptLineNumber
		}
		
		$licenseName = $licensingProduct.Name                       # Name  
		$InstallationId = $licensingProduct.OfflineInstallationId   # Installation ID
		$activationId = $licensingProduct.ID                        # Activation ID
		$ExtendedProductId = $licensingProduct.ProductKeyID         # Extended Product ID
	   
		LogAndConsole "Name             : $licenseName"
		LogAndConsole "Installation ID  : $InstallationId"
		LogAndConsole "Activation ID    : $activationId"
		LogAndConsole "Extd. Product ID : $ExtendedProductId"
		
	} catch {
		LogAndConsole "[Error] Failed to retrieve the license information."
		Exit $MyInvocation.ScriptLineNumber
	}

	# Retrieve the Confirmation ID.
	$confirmationId = CallWebService $WebServiceUrl $InstallationId $ExtendedProductId

	try {
		# Activate the product by depositing the Confirmation ID.
		LogAndConsole "Confirmation ID  : $confirmationId"
		LogAndConsole "Activating product..."
		$licensingProduct.DepositOfflineConfirmationId($InstallationId, $confirmationId) | Out-Null
		$licensingService.RefreshLicenseStatus() | Out-Null
		
		# Check if the activation was successful.
		$licensingProduct = Get-WmiObject -Query ('SELECT LicenseStatus, LicenseStatusReason FROM SoftwareLicensingProduct where PartialProductKey = "{0}"' -f $partialProductKey)
		
		if (!$licensingProduct.LicenseStatus -eq 1) {
			LogAndConsole "[Error] Product activation failed ($licensingProduct.LicenseStatusReason)."
			Exit $MyInvocation.ScriptLineNumber
		}
		
		LogAndConsole "Product activated successfully."
		
	} catch {
		LogAndConsole "[Error] Failed to deposit the Confirmation ID. The product was not activated."
		Exit $MyInvocation.ScriptLineNumber
	}
}

function Main {
	$scriptVersion = "0.15.2"
	LogAndConsole ""
	InstallAndActivateProductKey($ProductKey)
}

Main