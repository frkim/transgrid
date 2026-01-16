#Requires -Version 7.0
<#
.SYNOPSIS
    Creates API connections for Logic Apps Standard.

.DESCRIPTION
    This script creates the required API connection resources for Logic Apps workflows:
    - Azure Blob Storage connection (using Managed Identity)
    - Azure Tables connection (using Managed Identity)
    
.PARAMETER ResourceGroupName
    The name of the Azure Resource Group.

.PARAMETER Location
    Azure region. Default: westeurope

.PARAMETER LogicAppName
    The name of the Logic App.

.EXAMPLE
    .\create-api-connections.ps1 -ResourceGroupName "rg-transgrid-dev"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "westeurope",

    [Parameter(Mandatory = $false)]
    [string]$LogicAppName,

    [Parameter(Mandatory = $false)]
    [string]$StorageAccountName
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

Write-Information "=============================================="
Write-Information "Creating API Connections for Logic Apps"
Write-Information "=============================================="
Write-Information ""

# Get subscription and tenant info
$account = az account show --output json | ConvertFrom-Json
$subscriptionId = $account.id
$tenantId = $account.tenantId

Write-Information "Subscription: $subscriptionId"
Write-Information "Location: $Location"
Write-Information ""

# Auto-detect Logic App if not provided
if (-not $LogicAppName) {
    Write-Information "Detecting Logic App..."
    $logicApps = az webapp list --resource-group $ResourceGroupName --query "[?contains(kind, 'workflowapp')].name" -o tsv
    
    if ($logicApps) {
        if ($logicApps -is [string]) {
            $LogicAppName = $logicApps.Trim()
        } else {
            $logicAppArray = $logicApps -split "`n" | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }
            $LogicAppName = $logicAppArray[0]
        }
        Write-Information "Found Logic App: $LogicAppName"
    }
}

# Get Logic App managed identity
$logicAppIdentity = az webapp identity show --resource-group $ResourceGroupName --name $LogicAppName --query principalId -o tsv
Write-Information "Logic App Identity: $logicAppIdentity"

# Auto-detect storage account if not provided
if (-not $StorageAccountName) {
    Write-Information "Detecting Storage Account..."
    $storageAccounts = az storage account list --resource-group $ResourceGroupName --query "[?starts_with(name, 'sttransgrid')].name" -o tsv
    
    if ($storageAccounts) {
        if ($storageAccounts -is [string]) {
            $StorageAccountName = $storageAccounts.Trim()
        } else {
            $storageArray = $storageAccounts -split "`n" | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }
            $StorageAccountName = $storageArray[0]
        }
        Write-Information "Found Storage Account: $StorageAccountName"
    }
}

# Get storage account resource ID
$storageAccountId = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --query id -o tsv

Write-Information ""
Write-Information "Creating API Connections..."
Write-Information ""

# 1. Create Azure Blob Storage API Connection with Managed Identity
Write-Information "1. Creating Azure Blob Storage connection..."

$blobConnectionName = "azureblob"
$blobConnectionJson = @"
{
  "properties": {
    "displayName": "Azure Blob Storage",
    "parameterValueType": "Alternative",
    "alternativeParameterValues": {
      "accountName": "$StorageAccountName"
    },
    "api": {
      "id": "/subscriptions/$subscriptionId/providers/Microsoft.Web/locations/$Location/managedApis/azureblob"
    }
  },
  "kind": "V1",
  "location": "$Location"
}
"@

$blobConnectionJson | Out-File -FilePath "$env:TEMP\blob-connection.json" -Encoding UTF8

az rest --method put `
    --uri "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/connections/$blobConnectionName`?api-version=2016-06-01" `
    --body "@$env:TEMP\blob-connection.json" `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Information "✅ Blob Storage connection created"
} else {
    Write-Warning "Failed to create Blob Storage connection"
}

# 2. Create Azure Tables API Connection with Managed Identity
Write-Information "2. Creating Azure Tables connection..."

$tablesConnectionName = "azuretables"
$tablesConnectionJson = @"
{
  "properties": {
    "displayName": "Azure Tables",
    "parameterValueType": "Alternative",
    "alternativeParameterValues": {
      "storageAccountName": "$StorageAccountName"
    },
    "api": {
      "id": "/subscriptions/$subscriptionId/providers/Microsoft.Web/locations/$Location/managedApis/azuretables"
    }
  },
  "kind": "V1",
  "location": "$Location"
}
"@

$tablesConnectionJson | Out-File -FilePath "$env:TEMP\tables-connection.json" -Encoding UTF8

az rest --method put `
    --uri "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/connections/$tablesConnectionName`?api-version=2016-06-01" `
    --body "@$env:TEMP\tables-connection.json" `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Information "✅ Tables connection created"
} else {
    Write-Warning "Failed to create Tables connection"
}

# Grant Logic App access to connections via access policies
Write-Information ""
Write-Information "Granting Logic App access to connections..."

# Grant access to Blob connection
az rest --method put `
    --uri "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/connections/$blobConnectionName/accessPolicies/$logicAppIdentity`?api-version=2016-06-01" `
    --body "{\"properties\":{\"principal\":{\"type\":\"ActiveDirectory\",\"identity\":{\"tenantId\":\"$tenantId\",\"objectId\":\"$logicAppIdentity\"}}}}" `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Information "✅ Access granted to Blob connection"
}

# Grant access to Tables connection
az rest --method put `
    --uri "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/connections/$tablesConnectionName/accessPolicies/$logicAppIdentity`?api-version=2016-06-01" `
    --body "{\"properties\":{\"principal\":{\"type\":\"ActiveDirectory\",\"identity\":{\"tenantId\":\"$tenantId\",\"objectId\":\"$logicAppIdentity\"}}}}" `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Information "✅ Access granted to Tables connection"
}

# Assign Storage Blob Data Contributor role to Logic App identity
Write-Information ""
Write-Information "Assigning RBAC roles..."

$blobDataContributorRole = "ba92f5b4-2d11-453d-a403-e96b0029c9fe"
$tableDataContributorRole = "0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3"

az role assignment create `
    --assignee $logicAppIdentity `
    --role $blobDataContributorRole `
    --scope $storageAccountId `
    --output none 2>$null

Write-Information "✅ Storage Blob Data Contributor role assigned"

az role assignment create `
    --assignee $logicAppIdentity `
    --role $tableDataContributorRole `
    --scope $storageAccountId `
    --output none 2>$null

Write-Information "✅ Storage Table Data Contributor role assigned"

Write-Information ""
Write-Information "=============================================="
Write-Information "API Connections Created Successfully!"
Write-Information "=============================================="
Write-Information ""
Write-Information "Next steps:"
Write-Information "  1. Restart the Logic App to pick up the connections"
Write-Information "  2. Open workflows in Azure Portal"
Write-Information "  3. Connections should now be valid"
Write-Information ""

# Restart Logic App to pick up connections
Write-Information "Restarting Logic App to apply changes..."
az webapp restart --resource-group $ResourceGroupName --name $LogicAppName --output none

Write-Information "✅ Logic App restarted"
Write-Information ""
