#Requires -Version 7.0
<#
.SYNOPSIS
    Master deployment script for Transgrid Azure Integration Services.

.DESCRIPTION
    This script orchestrates the complete deployment of the Transgrid solution:
    1. Infrastructure (Bicep templates)
    2. Mock Server (Container Apps)
    3. Azure Functions code
    4. Logic Apps Standard workflows

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group. Default: rg-transgrid-{environment}

.PARAMETER Location
    Azure region for deployment. Default: westeurope

.PARAMETER Environment
    Deployment environment (dev, test, prod). Default: dev

.PARAMETER SftpPassword
    Password for SFTP user account.

.PARAMETER OpsApiEndpoint
    The Operations API (GraphQL) endpoint URL. If not provided, Mock Server endpoint will be used.

.PARAMETER SkipInfrastructure
    Skip infrastructure deployment (only deploy code).

.PARAMETER SkipMockServer
    Skip Mock Server deployment.

.PARAMETER SkipFunctions
    Skip Azure Functions deployment.

.PARAMETER SkipLogicApps
    Skip Logic Apps deployment.

.EXAMPLE
    # Deploy everything
    .\deploy-all.ps1 -SftpPassword "SecurePassword123!"

.EXAMPLE
    # Deploy only code (infrastructure already exists)
    .\deploy-all.ps1 -ResourceGroupName "rg-transgrid-dev" -SkipInfrastructure

.EXAMPLE
    # Deploy with specific OPS API endpoint
    .\deploy-all.ps1 -SftpPassword "SecurePassword123!" -OpsApiEndpoint "https://api.eurostar.com/graphql"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "westeurope",

    [Parameter(Mandatory = $false)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment = "dev",

    [Parameter(Mandatory = $false)]
    [SecureString]$SftpPassword,

    [Parameter(Mandatory = $false)]
    [string]$OpsApiEndpoint,

    [Parameter(Mandatory = $false)]
    [string[]]$AllowedIpRanges = @(),

    [Parameter(Mandatory = $false)]
    [switch]$SkipInfrastructure,

    [Parameter(Mandatory = $false)]
    [switch]$SkipMockServer,

    [Parameter(Mandatory = $false)]
    [switch]$SkipFunctions,

    [Parameter(Mandatory = $false)]
    [switch]$SkipLogicApps
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Script directories
$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$InfraDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $InfraDir

# Default resource group name
if (-not $ResourceGroupName) {
    $ResourceGroupName = "rg-transgrid-$Environment"
}

Write-Information ""
Write-Information "╔══════════════════════════════════════════════════════════════╗"
Write-Information "║      Transgrid Azure Integration Services Deployment         ║"
Write-Information "╠══════════════════════════════════════════════════════════════╣"
Write-Information "║  Resource Group: $($ResourceGroupName.PadRight(38))║"
Write-Information "║  Location:       $($Location.PadRight(38))║"
Write-Information "║  Environment:    $($Environment.PadRight(38))║"
Write-Information "╚══════════════════════════════════════════════════════════════╝"
Write-Information ""

# Check Azure CLI
Write-Information "🔐 Checking Azure authentication..."
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Information "Please log in to Azure..."
    az login
    $account = az account show --output json | ConvertFrom-Json
}
Write-Information "✅ Logged in as: $($account.user.name)"
Write-Information "   Subscription: $($account.name)"
Write-Information ""

$deploymentSuccess = $true
$startTime = Get-Date

# ============================================================
# Step 1: Infrastructure Deployment
# ============================================================
if (-not $SkipInfrastructure) {
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information "📦 STEP 1: Infrastructure Deployment"
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information ""
    
    if (-not $SftpPassword) {
        Write-Error "SftpPassword is required for infrastructure deployment. Use -SkipInfrastructure to skip."
        exit 1
    }
    
    $deployScript = Join-Path $InfraDir "deploy.ps1"
    
    $deployParams = @{
        ResourceGroupName = $ResourceGroupName
        Location = $Location
        Environment = $Environment
        SftpPassword = $SftpPassword
    }
    
    if ($OpsApiEndpoint) {
        $deployParams.OpsApiEndpoint = $OpsApiEndpoint
    }
    
    if ($AllowedIpRanges -and $AllowedIpRanges.Count -gt 0) {
        $deployParams.AllowedIpRanges = $AllowedIpRanges
    }
    
    & $deployScript @deployParams
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Infrastructure deployment failed!"
        $deploymentSuccess = $false
    }
    else {
        Write-Information "✅ Infrastructure deployment completed"
    }
    Write-Information ""
}
else {
    Write-Information "⏭️  Skipping infrastructure deployment"
    Write-Information ""
}

# ============================================================
# Step 2: Mock Server Deployment
# ============================================================
$mockServerEndpoint = $OpsApiEndpoint
if (-not $SkipMockServer -and $deploymentSuccess) {
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information "🌐 STEP 2: Mock Server Container Apps Deployment"
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information ""
    
    $mockServerScript = Join-Path $ScriptDir "deploy-mockserver.ps1"
    
    if (Test-Path $mockServerScript) {
        $mockServerResult = & $mockServerScript -ResourceGroupName $ResourceGroupName -Environment $Environment
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Mock Server deployment failed! Continuing with remaining deployments..."
            $deploymentSuccess = $false
        }
        else {
            Write-Information "✅ Mock Server deployment completed"
            # Use Mock Server endpoint if no OpsApiEndpoint was specified
            if (-not $OpsApiEndpoint -and $mockServerResult) {
                $mockServerEndpoint = $mockServerResult
                Write-Information "   Using Mock Server endpoint: $mockServerEndpoint"
            }
        }
    }
    else {
        Write-Warning "Mock Server deployment script not found at: $mockServerScript"
    }
    Write-Information ""
}
else {
    Write-Information "⏭️  Skipping Mock Server deployment"
    Write-Information ""
}

# ============================================================
# Step 3: Azure Functions Deployment
# ============================================================
if (-not $SkipFunctions -and $deploymentSuccess) {
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information "⚡ STEP 3: Azure Functions Deployment"
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information ""
    
    $functionsScript = Join-Path $ScriptDir "deploy-functions.ps1"
    
    if (Test-Path $functionsScript) {
        & $functionsScript -ResourceGroupName $ResourceGroupName
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Azure Functions deployment failed! Continuing with remaining deployments..."
            $deploymentSuccess = $false
        }
        else {
            Write-Information "✅ Azure Functions deployment completed"
        }
    }
    else {
        Write-Warning "Functions deployment script not found at: $functionsScript"
    }
    Write-Information ""
}
else {
    Write-Information "⏭️  Skipping Azure Functions deployment"
    Write-Information ""
}

# ============================================================
# Step 4: Logic Apps Deployment
# ============================================================
if (-not $SkipLogicApps -and $deploymentSuccess) {
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information "🔄 STEP 4: Logic Apps Standard Deployment"
    Write-Information "═══════════════════════════════════════════════════════════════"
    Write-Information ""
    
    $logicAppsScript = Join-Path $ScriptDir "deploy-logicapps.ps1"
    
    if (Test-Path $logicAppsScript) {
        $logicAppsParams = @{
            ResourceGroupName = $ResourceGroupName
        }
        
        if ($mockServerEndpoint) {
            $logicAppsParams.OpsApiEndpoint = $mockServerEndpoint
        }
        
        & $logicAppsScript @logicAppsParams
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Logic Apps deployment failed!"
            $deploymentSuccess = $false
        }
        else {
            Write-Information "✅ Logic Apps deployment completed"
        }
    }
    else {
        Write-Warning "Logic Apps deployment script not found at: $logicAppsScript"
    }
    Write-Information ""
}
else {
    Write-Information "⏭️  Skipping Logic Apps deployment"
    Write-Information ""
}

# ============================================================
# Deployment Summary
# ============================================================
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Information ""
Write-Information "╔══════════════════════════════════════════════════════════════╗"
Write-Information "║                    DEPLOYMENT SUMMARY                        ║"
Write-Information "╠══════════════════════════════════════════════════════════════╣"

if ($deploymentSuccess) {
    Write-Information "║  Status: ✅ SUCCESS                                          ║"
}
else {
    Write-Information "║  Status: ⚠️  COMPLETED WITH WARNINGS                         ║"
}

Write-Information "║  Duration: $($duration.ToString("mm\:ss").PadRight(47))║"
Write-Information "╚══════════════════════════════════════════════════════════════╝"
Write-Information ""

# Get deployed resources info
Write-Information "📋 Deployed Resources:"
Write-Information ""

$functionApp = az functionapp list --resource-group $ResourceGroupName --query "[0]" -o json 2>$null | ConvertFrom-Json
if ($functionApp) {
    Write-Information "   Azure Function: https://$($functionApp.defaultHostName)"
}

$logicApp = az logicapp list --resource-group $ResourceGroupName --query "[0]" -o json 2>$null | ConvertFrom-Json
if ($logicApp) {
    Write-Information "   Logic App:      https://$($logicApp.defaultHostName)"
}

$storageAccounts = az storage account list --resource-group $ResourceGroupName --query "[].name" -o json 2>$null | ConvertFrom-Json
if ($storageAccounts) {
    Write-Information "   Storage:        $($storageAccounts -join ', ')"
}

Write-Information ""
Write-Information "🔗 Azure Portal:"
Write-Information "   https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroupName"
Write-Information ""

if (-not $deploymentSuccess) {
    exit 1
}
