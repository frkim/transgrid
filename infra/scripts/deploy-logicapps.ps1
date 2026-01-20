#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys the Logic Apps Standard workflows to Azure.

.DESCRIPTION
    This script packages and deploys the Logic Apps Standard project to Azure.
    It creates a ZIP package of the workflows and deploys using Azure CLI.

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group containing the Logic App.

.PARAMETER LogicAppName
    The name of the Azure Logic App. If not provided, will be auto-detected.

.PARAMETER OpsApiEndpoint
    The Operations API (GraphQL) endpoint URL.

.PARAMETER FunctionEndpoint
    The Azure Function endpoint URL for XML transformation.

.EXAMPLE
    .\deploy-logicapps.ps1 -ResourceGroupName "rg-transgrid-demo"

.EXAMPLE
    .\deploy-logicapps.ps1 -ResourceGroupName "rg-transgrid-demo" -OpsApiEndpoint "https://api.eurostar.com/graphql"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$LogicAppName,

    [Parameter(Mandatory = $false)]
    [string]$OpsApiEndpoint,

    [Parameter(Mandatory = $false)]
    [string]$FunctionEndpoint
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Script and project directories
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InfraDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $InfraDir
$LogicAppsDir = Join-Path $RepoRoot "sources\logicapps"
$TempDir = Join-Path $env:TEMP "transgrid-logicapps-deploy"
$ZipPath = Join-Path $TempDir "logicapps.zip"

Write-Information "=============================================="
Write-Information "Logic Apps Standard Deployment"
Write-Information "=============================================="
Write-Information ""

# Check Azure CLI
Write-Information "Checking Azure CLI..."
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Information "Please log in to Azure..."
    az login
    $account = az account show --output json | ConvertFrom-Json
}
Write-Information "Logged in as: $($account.user.name)"

# Auto-detect Logic App name if not provided
if (-not $LogicAppName) {
    Write-Information "Detecting Logic App in resource group..."
    $logicApps = az logicapp list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
    
    if (-not $logicApps) {
        # Try webapp with kind containing 'workflowapp'
        $logicApps = az webapp list --resource-group $ResourceGroupName --query "[?contains(kind, 'workflowapp')].name" -o tsv
    }
    
    if (-not $logicApps) {
        Write-Error "No Logic Apps found in resource group '$ResourceGroupName'"
        exit 1
    }
    
    # Handle single line or multiline output
    if ($logicApps -is [string]) {
        $logicAppArray = @($logicApps.Trim())
    } else {
        $logicAppArray = $logicApps -split "`n" | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }
    }
    
    if ($logicAppArray.Count -eq 1) {
        $LogicAppName = $logicAppArray[0]
        Write-Information "Found Logic App: $LogicAppName"
    }
    elseif ($logicAppArray.Count -gt 1) {
        Write-Error "Multiple Logic Apps found. Please specify -LogicAppName parameter."
        Write-Information "Available Logic Apps:"
        $logicAppArray | ForEach-Object { Write-Information "  - $_" }
        exit 1
    }
}

# Verify Logic App exists and get details
Write-Information "Verifying Logic App '$LogicAppName'..."
$logicApp = az logicapp show --resource-group $ResourceGroupName --name $LogicAppName --output json 2>$null | ConvertFrom-Json
if (-not $logicApp) {
    # Try webapp command as fallback
    $logicApp = az webapp show --resource-group $ResourceGroupName --name $LogicAppName --output json 2>$null | ConvertFrom-Json
}
if (-not $logicApp) {
    Write-Error "Logic App '$LogicAppName' not found in resource group '$ResourceGroupName'"
    exit 1
}
Write-Information "Logic App state: $($logicApp.state)"

# Auto-detect Function endpoint if not provided
$FunctionAppName = $null
if (-not $FunctionEndpoint) {
    Write-Information "Detecting Function App endpoint..."
    $functionApps = az functionapp list --resource-group $ResourceGroupName --query "[].{name:name,hostname:defaultHostName}" -o json | ConvertFrom-Json
    if ($functionApps -and $functionApps.Count -gt 0) {
        $FunctionAppName = $functionApps[0].name
        $FunctionEndpoint = "https://$($functionApps[0].hostname)/api"
        Write-Information "Found Function endpoint: $FunctionEndpoint"
    }
    else {
        Write-Warning "No Function App found. You may need to configure FUNCTION_ENDPOINT manually."
        $FunctionEndpoint = "https://your-function-app.azurewebsites.net/api"
    }
}

# Auto-detect Function key if Function App was found
$FunctionKey = $null
if ($FunctionAppName) {
    Write-Information "Detecting Function key..."
    $FunctionKey = az functionapp keys list --resource-group $ResourceGroupName --name $FunctionAppName --query "functionKeys.default" -o tsv 2>$null
    if ($FunctionKey) {
        Write-Information "Found Function key"
    }
    else {
        Write-Warning "No Function key found. You may need to configure FUNCTION_KEY manually."
    }
}

# Auto-detect Mock Server / OPS API endpoint if not provided
if (-not $OpsApiEndpoint) {
    Write-Information "Detecting OPS API endpoint..."
    $containerApps = az containerapp list --resource-group $ResourceGroupName --query "[?contains(name, 'mock')].{name:name,fqdn:properties.configuration.ingress.fqdn}" -o json 2>$null | ConvertFrom-Json
    if ($containerApps -and $containerApps.Count -gt 0) {
        $OpsApiEndpoint = "https://$($containerApps[0].fqdn)/graphql"
        Write-Information "Found OPS API endpoint: $OpsApiEndpoint"
    }
    else {
        # Check existing app setting
        $existingEndpoint = az webapp config appsettings list --resource-group $ResourceGroupName --name $LogicAppName --query "[?name=='OPS_API_ENDPOINT'].value" -o tsv 2>$null
        if ($existingEndpoint) {
            $OpsApiEndpoint = $existingEndpoint
            Write-Information "Using existing OPS API endpoint: $OpsApiEndpoint"
        }
        else {
            Write-Warning "No Container App found. You may need to configure OPS_API_ENDPOINT manually."
        }
    }
}

# Prepare deployment package
Write-Information ""
Write-Information "Preparing deployment package..."

# Clean and create temp directory
if (Test-Path $TempDir) {
    Remove-Item -Path $TempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

# Copy Logic Apps files (excluding local.settings.json which has secrets)
$filesToCopy = @(
    "host.json",
    "connections.json",
    "parameters.json"
)

foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $LogicAppsDir $file
    if (Test-Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination $TempDir -Force
        Write-Information "  Copied: $file"
    }
}

# Copy workflow folders
$workflowFolders = Get-ChildItem -Path $LogicAppsDir -Directory | Where-Object { $_.Name -like "rne-*" -or $_.Name -like "sf-*" }
foreach ($folder in $workflowFolders) {
    $destFolder = Join-Path $TempDir $folder.Name
    Copy-Item -Path $folder.FullName -Destination $destFolder -Recurse -Force
    Write-Information "  Copied workflow: $($folder.Name)"
}

# Create ZIP package
Write-Information ""
Write-Information "Creating ZIP package..."
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipPath -Force
$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1KB, 2)
Write-Information "Package created: $zipSize KB"

# Deploy to Azure
Write-Information ""
Write-Information "Deploying to Logic App..."
Write-Information "Target: $LogicAppName"

az logicapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $LogicAppName `
    --src $ZipPath `
    --output none

if ($LASTEXITCODE -ne 0) {
    # Try webapp deployment as fallback
    Write-Information "Trying webapp deployment..."
    az webapp deployment source config-zip `
        --resource-group $ResourceGroupName `
        --name $LogicAppName `
        --src $ZipPath `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deployment failed!"
        exit 1
    }
}

# Configure app settings
Write-Information ""
Write-Information "Configuring app settings..."

$appSettings = @()

if ($FunctionEndpoint) {
    $appSettings += "FUNCTION_ENDPOINT=$FunctionEndpoint"
}

if ($FunctionKey) {
    $appSettings += "FUNCTION_KEY=$FunctionKey"
}

if ($OpsApiEndpoint) {
    $appSettings += "OPS_API_ENDPOINT=$OpsApiEndpoint"
    # Also set Salesforce API endpoint to the Mock Server
    $salesforceEndpoint = $OpsApiEndpoint -replace "/graphql$", ""
    $appSettings += "SALESFORCE_API_ENDPOINT=$salesforceEndpoint"
}

if ($appSettings.Count -gt 0) {
    az logicapp config appsettings set `
        --resource-group $ResourceGroupName `
        --name $LogicAppName `
        --settings $appSettings `
        --output none 2>$null

    if ($LASTEXITCODE -ne 0) {
        # Try webapp command as fallback
        az webapp config appsettings set `
            --resource-group $ResourceGroupName `
            --name $LogicAppName `
            --settings $appSettings `
            --output none
    }
} else {
    Write-Information "  No app settings to configure."
}

# Clean up
Write-Information ""
Write-Information "Cleaning up temporary files..."
Remove-Item -Path $TempDir -Recurse -Force

Write-Information ""
Write-Information "=============================================="
Write-Information "Deployment Completed Successfully!"
Write-Information "=============================================="
Write-Information ""
Write-Information "Logic App URL: https://$($logicApp.defaultHostName)"
Write-Information ""
Write-Information "Deployed Workflows:"
$workflowFolders | ForEach-Object { Write-Information "  - $($_.Name)" }
Write-Information ""
Write-Information "Next steps:"
Write-Information "  1. Open Azure Portal and navigate to the Logic App"
Write-Information "  2. Configure API connections (Blob, SFTP, Table Storage)"
Write-Information "  3. Enable the workflows"
Write-Information ""
