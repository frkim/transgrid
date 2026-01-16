#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys the Azure Functions code to Azure.

.DESCRIPTION
    This script builds and deploys the Transgrid.Functions project to Azure Functions.
    It uses the Azure Functions Core Tools (func) to publish the code.

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group containing the Function App.

.PARAMETER FunctionAppName
    The name of the Azure Function App. If not provided, will be auto-detected.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER SkipBuild
    Skip the dotnet build step (use existing build output).

.EXAMPLE
    .\deploy-functions.ps1 -ResourceGroupName "rg-transgrid-demo"

.EXAMPLE
    .\deploy-functions.ps1 -ResourceGroupName "rg-transgrid-demo" -FunctionAppName "func-transgrid-dev"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$FunctionAppName,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Script and project directories
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InfraDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $InfraDir
$FunctionsProjectDir = Join-Path $RepoRoot "sources\functions\Transgrid.Functions"
$FunctionsPublishDir = Join-Path $FunctionsProjectDir "bin\$Configuration\net8.0\publish"

Write-Information "=============================================="
Write-Information "Azure Functions Deployment"
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

# Check Azure Functions Core Tools
Write-Information "Checking Azure Functions Core Tools..."
$funcVersion = func --version 2>$null
if (-not $funcVersion) {
    Write-Error "Azure Functions Core Tools not found. Install with: npm install -g azure-functions-core-tools@4"
    Write-Error "Or: winget install Microsoft.Azure.FunctionsCoreTools"
    exit 1
}
Write-Information "Azure Functions Core Tools version: $funcVersion"

# Auto-detect Function App name if not provided
if (-not $FunctionAppName) {
    Write-Information "Detecting Function App in resource group..."
    $functionApps = az functionapp list --resource-group $ResourceGroupName --query "[].name" -o tsv
    
    if (-not $functionApps) {
        Write-Error "No Function Apps found in resource group '$ResourceGroupName'"
        exit 1
    }
    
    # Handle single line or multiline output
    if ($functionApps -is [string]) {
        $functionAppArray = @($functionApps.Trim())
    } else {
        $functionAppArray = $functionApps -split "`n" | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }
    }
    
    if ($functionAppArray.Count -eq 1) {
        $FunctionAppName = $functionAppArray[0]
        Write-Information "Found Function App: $FunctionAppName"
    }
    elseif ($functionAppArray.Count -gt 1) {
        Write-Error "Multiple Function Apps found. Please specify -FunctionAppName parameter."
        Write-Information "Available Function Apps:"
        $functionAppArray | ForEach-Object { Write-Information "  - $_" }
        exit 1
    }
}

# Verify Function App exists
Write-Information "Verifying Function App '$FunctionAppName'..."
$functionApp = az functionapp show --resource-group $ResourceGroupName --name $FunctionAppName --output json 2>$null | ConvertFrom-Json
if (-not $functionApp) {
    Write-Error "Function App '$FunctionAppName' not found in resource group '$ResourceGroupName'"
    exit 1
}
Write-Information "Function App state: $($functionApp.state)"

# Build the project
if (-not $SkipBuild) {
    Write-Information ""
    Write-Information "Building Functions project..."
    
    Push-Location $FunctionsProjectDir
    try {
        dotnet publish -c $Configuration -o $FunctionsPublishDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed!"
            exit 1
        }
        Write-Information "Build completed successfully"
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Information "Skipping build (using existing output)"
    if (-not (Test-Path $FunctionsPublishDir)) {
        Write-Error "Publish directory not found: $FunctionsPublishDir"
        Write-Error "Run without -SkipBuild or build manually first"
        exit 1
    }
}

# Deploy using func CLI
Write-Information ""
Write-Information "Deploying to Azure Functions..."
Write-Information "Target: $FunctionAppName"

Push-Location $FunctionsPublishDir
try {
    func azure functionapp publish $FunctionAppName --dotnet-isolated
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deployment failed!"
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Information ""
Write-Information "=============================================="
Write-Information "Deployment Completed Successfully!"
Write-Information "=============================================="
Write-Information ""
Write-Information "Function App URL: https://$($functionApp.defaultHostName)"
Write-Information "Function Endpoint: https://$($functionApp.defaultHostName)/api/TransformTrainPlan"
Write-Information ""
Write-Information "Test the function:"
Write-Information "  curl -X POST https://$($functionApp.defaultHostName)/api/TransformTrainPlan \"
Write-Information "       -H 'Content-Type: application/json' \"
Write-Information "       -d '{\"id\":\"test\",\"serviceCode\":\"9001\",\"travelDate\":\"2026-01-20\",\"origin\":\"GBSTP\",\"destination\":\"FRPNO\"}'"
Write-Information ""
