#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys the Azure Integration Services infrastructure to Azure.

.DESCRIPTION
    This script deploys all Azure resources for the integration demo including:
    - Use Case 1: RNE Operational Plans Export
    - Use Case 2: Salesforce Negotiated Rates Export
    - Use Case 3: Network Rail CIF File Processing
    
    Resources deployed:
    - Storage Account with Blob, Table, and File shares
    - Container Apps Environment with SFTP servers (primary and backup)
    - Logic Apps Standard for workflow orchestration
    - Azure Functions for transformations and CIF processing
    - Azure Service Bus for event-driven integration
    - Azure Managed Redis for reference data caching
    - Application Insights for monitoring

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group to deploy to.

.PARAMETER Location
    The Azure region for deployment (e.g., westeurope, northeurope).

.PARAMETER Environment
    The environment name (dev, test, prod). Default: dev

.PARAMETER SftpPassword
    The password for the SFTP user. If not provided, will prompt securely.

.PARAMETER AllowedIpRanges
    Array of IP ranges allowed to access SFTP (CIDR notation).

.PARAMETER SkipSshKeyGeneration
    Skip generating new SSH host keys.

.PARAMETER WhatIf
    Show what would be deployed without actually deploying.

.EXAMPLE
    .\deploy.ps1 -ResourceGroupName "rg-transgrid-demo" -Location "westeurope"

.EXAMPLE
    .\deploy.ps1 -ResourceGroupName "rg-transgrid-prod" -Location "westeurope" -Environment "prod" -AllowedIpRanges @("10.0.0.0/8")
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $false)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment = "dev",

    [Parameter(Mandatory = $false)]
    [SecureString]$SftpPassword,

    [Parameter(Mandatory = $false)]
    [string[]]$AllowedIpRanges = @(),

    [Parameter(Mandatory = $false)]
    [string]$OpsApiEndpoint = "http://localhost:5000/graphql",

    [Parameter(Mandatory = $false)]
    [switch]$SkipSshKeyGeneration
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Information "=============================================="
Write-Information "Azure Integration Services - Deployment"
Write-Information "=============================================="
Write-Information ""
Write-Information "Use Cases:"
Write-Information "  1. RNE Operational Plans Export"
Write-Information "  2. Salesforce Negotiated Rates Export"
Write-Information "  3. Network Rail CIF File Processing"
Write-Information ""
Write-Information "Resource Group: $ResourceGroupName"
Write-Information "Location: $Location"
Write-Information "Environment: $Environment"
Write-Information ""

# Check Azure CLI
Write-Information "Checking Azure CLI..."
$azVersion = az version --output json 2>$null | ConvertFrom-Json
if (-not $azVersion) {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecli"
    exit 1
}
Write-Information "Azure CLI version: $($azVersion.'azure-cli')"

# Check Bicep
Write-Information "Checking Bicep CLI..."
$bicepVersion = az bicep version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Information "Installing Bicep CLI..."
    az bicep install
}
Write-Information "Bicep ready"

# Check login status
Write-Information "Checking Azure login status..."
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Information "Please log in to Azure..."
    az login
    $account = az account show --output json | ConvertFrom-Json
}
Write-Information "Logged in as: $($account.user.name)"
Write-Information "Subscription: $($account.name) ($($account.id))"

# Prompt for SFTP password if not provided
if (-not $SftpPassword) {
    Write-Information ""
    $SftpPassword = Read-Host -Prompt "Enter SFTP password" -AsSecureString
}
$sftpPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SftpPassword)
)

# Create resource group if it doesn't exist
Write-Information ""
Write-Information "Ensuring resource group exists..."
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "false") {
    if ($PSCmdlet.ShouldProcess($ResourceGroupName, "Create Resource Group")) {
        az group create --name $ResourceGroupName --location $Location --output none
        Write-Information "Created resource group: $ResourceGroupName"
    }
} else {
    Write-Information "Resource group already exists: $ResourceGroupName"
}

# Generate SSH keys if needed
$sshKeyDir = Join-Path $ScriptDir "ssh-keys"
if (-not $SkipSshKeyGeneration) {
    Write-Information ""
    Write-Information "Generating SSH host keys..."
    
    if (-not (Test-Path $sshKeyDir)) {
        New-Item -ItemType Directory -Path $sshKeyDir -Force | Out-Null
    }
    
    $ed25519Key = Join-Path $sshKeyDir "ssh_host_ed25519_key"
    $rsaKey = Join-Path $sshKeyDir "ssh_host_rsa_key"
    
    if (-not (Test-Path $ed25519Key)) {
        if ($PSCmdlet.ShouldProcess("ssh_host_ed25519_key", "Generate SSH Key")) {
            ssh-keygen -t ed25519 -f $ed25519Key -N '""' -q
            Write-Information "Generated ED25519 host key"
        }
    } else {
        Write-Information "ED25519 key already exists"
    }
    
    if (-not (Test-Path $rsaKey)) {
        if ($PSCmdlet.ShouldProcess("ssh_host_rsa_key", "Generate SSH Key")) {
            ssh-keygen -t rsa -b 4096 -f $rsaKey -N '""' -q
            Write-Information "Generated RSA host key"
        }
    } else {
        Write-Information "RSA key already exists"
    }
}

# Deploy Bicep template
Write-Information ""
Write-Information "Deploying infrastructure..."
Write-Information "This may take 10-15 minutes..."

$deploymentName = "rne-export-$Environment-$(Get-Date -Format 'yyyyMMddHHmmss')"

$deployParams = @(
    "--name", $deploymentName,
    "--resource-group", $ResourceGroupName,
    "--template-file", (Join-Path $ScriptDir "main.bicep"),
    "--parameters", "environment=$Environment",
    "--parameters", "location=$Location",
    "--parameters", "sftpPassword=$sftpPasswordPlain",
    "--parameters", "opsApiEndpoint=$OpsApiEndpoint"
)

if ($AllowedIpRanges.Count -gt 0) {
    $ipRangesJson = $AllowedIpRanges | ConvertTo-Json -Compress
    $deployParams += "--parameters"
    $deployParams += "allowedSftpIpRanges=$ipRangesJson"
}

if ($WhatIf) {
    $deployParams += "--what-if"
}

$deployParams += "--output"
$deployParams += "json"

$deploymentResult = az deployment group create @deployParams | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed!"
    exit 1
}

Write-Information ""
Write-Information "Deployment completed successfully!"

# Extract outputs
$outputs = $deploymentResult.properties.outputs

Write-Information ""
Write-Information "=============================================="
Write-Information "Deployment Outputs"
Write-Information "=============================================="
Write-Information ""
Write-Information "Storage Account: $($outputs.storageAccountName.value)"
Write-Information "Blob Container: $($outputs.blobContainerName.value)"
Write-Information ""
Write-Information "Primary SFTP Endpoint: $($outputs.primarySftpEndpoint.value)"
Write-Information "Backup SFTP Endpoint: $($outputs.backupSftpEndpoint.value)"
Write-Information "SFTP Username: rneuser"
Write-Information ""
Write-Information "Function App: $($outputs.functionAppName.value)"
Write-Information "Function Endpoint: $($outputs.functionEndpoint.value)"
Write-Information ""
Write-Information "Logic App: $($outputs.logicAppName.value)"
Write-Information "Logic App URL: https://$($outputs.logicAppHostname.value)"
Write-Information ""
Write-Information "Application Insights: $($outputs.appInsightsName.value)"
Write-Information ""

# Show Redis Cache info if deployed
if ($outputs.redisCacheName -and $outputs.redisCacheName.value) {
    Write-Information "Redis Cache: $($outputs.redisCacheName.value)"
    Write-Information "Redis Host: $($outputs.redisCacheHostName.value)"
    Write-Information ""
}

# Show CIF Archive container info
if ($outputs.cifArchiveContainer -and $outputs.cifArchiveContainer.value) {
    Write-Information "CIF Archive Container: $($outputs.cifArchiveContainer.value)"
    Write-Information ""
}

# Upload SSH keys to Azure Files
if (-not $SkipSshKeyGeneration -and (Test-Path $sshKeyDir)) {
    Write-Information ""
    Write-Information "Uploading SSH keys to Azure Files..."
    
    $storageAccount = $outputs.storageAccountName.value
    $storageKey = az storage account keys list --account-name $storageAccount --query "[0].value" -o tsv
    
    # Upload to sshkeys share
    $sshKeyFiles = Get-ChildItem -Path $sshKeyDir -File
    foreach ($file in $sshKeyFiles) {
        if ($PSCmdlet.ShouldProcess($file.Name, "Upload to Azure Files")) {
            az storage file upload `
                --account-name $storageAccount `
                --account-key $storageKey `
                --share-name "sshkeys" `
                --source $file.FullName `
                --path $file.Name `
                --output none
            Write-Information "Uploaded: $($file.Name)"
        }
    }
    
    # Upload copykeys.sh to scripts share
    $copykeysScript = Join-Path $ScriptDir "scripts\copykeys.sh"
    if (Test-Path $copykeysScript) {
        if ($PSCmdlet.ShouldProcess("copykeys.sh", "Upload to Azure Files")) {
            az storage file upload `
                --account-name $storageAccount `
                --account-key $storageKey `
                --share-name "scripts" `
                --source $copykeysScript `
                --path "copykeys.sh" `
                --output none
            Write-Information "Uploaded: copykeys.sh"
        }
    }
    
    # Upload to backup shares as well
    foreach ($file in $sshKeyFiles) {
        if ($PSCmdlet.ShouldProcess("$($file.Name) (backup)", "Upload to Azure Files")) {
            az storage file upload `
                --account-name $storageAccount `
                --account-key $storageKey `
                --share-name "sshkeys-backup" `
                --source $file.FullName `
                --path $file.Name `
                --output none
        }
    }
    
    if (Test-Path $copykeysScript) {
        if ($PSCmdlet.ShouldProcess("copykeys.sh (backup)", "Upload to Azure Files")) {
            az storage file upload `
                --account-name $storageAccount `
                --account-key $storageKey `
                --share-name "scripts-backup" `
                --source $copykeysScript `
                --path "copykeys.sh" `
                --output none
        }
    }
    
    Write-Information "SSH keys uploaded to Azure Files shares"
}

Write-Information ""
Write-Information "=============================================="
Write-Information "Next Steps"
Write-Information "=============================================="
Write-Information ""
Write-Information "1. Deploy Azure Function code:"
Write-Information "   func azure functionapp publish $($outputs.functionAppName.value)"
Write-Information ""
Write-Information "2. Import Logic App workflows:"
Write-Information "   - Open Logic App in Azure Portal"
Write-Information "   - Create new workflows from workflow definitions"
Write-Information "   - Configure API connections (Blob, SFTP, Table)"
Write-Information ""
Write-Information "3. Test SFTP connection:"
Write-Information "   sftp -P 22 rneuser@$($outputs.primarySftpFqdn.value)"
Write-Information ""
Write-Information "4. Run the mock server locally:"
Write-Information "   cd ../sources/server/Transgrid.MockServer"
Write-Information "   dotnet run"
Write-Information ""

# Clean up password from memory
$sftpPasswordPlain = $null
[System.GC]::Collect()
