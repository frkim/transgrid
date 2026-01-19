#Requires -Version 7.0
<#
.SYNOPSIS
    Deploy Transgrid Mock Server to Azure Container Apps.

.DESCRIPTION
    This script builds and deploys the Transgrid Mock Server to Azure Container Apps.
    It uses Azure Container Registry to store the container image.

.PARAMETER ResourceGroupName
    The name of the Azure Resource Group. Default: rg-transgrid-{environment}

.PARAMETER Environment
    Deployment environment (dev, test, prod). Default: dev

.PARAMETER ContainerAppsEnvironmentName
    The name of the Container Apps Environment. Default: cae-transgrid-{environment}

.PARAMETER RegistryName
    The Azure Container Registry name. If not provided, will create one.

.PARAMETER ImageTag
    Tag for the container image. Default: latest

.EXAMPLE
    .\deploy-mockserver.ps1 -ResourceGroupName "rg-transgrid-dev"

.EXAMPLE
    .\deploy-mockserver.ps1 -Environment "dev" -ImageTag "v1.0.0"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment = "dev",

    [Parameter(Mandatory = $false)]
    [string]$ContainerAppsEnvironmentName,

    [Parameter(Mandatory = $false)]
    [string]$RegistryName,

    [Parameter(Mandatory = $false)]
    [string]$ImageTag = "latest"
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
$MockServerPath = Join-Path $RepoRoot "sources" "server" "Transgrid.MockServer"

# Default values
if (-not $ResourceGroupName) {
    $ResourceGroupName = "rg-transgrid-$Environment"
}
if (-not $ContainerAppsEnvironmentName) {
    $ContainerAppsEnvironmentName = "cae-transgrid-$Environment"
}

$ContainerAppName = "ca-transgrid-mock-$Environment"
$ImageName = "transgrid-mockserver"

Write-Information ""
Write-Information "╔══════════════════════════════════════════════════════════════╗"
Write-Information "║       Transgrid Mock Server Container Apps Deployment        ║"
Write-Information "╠══════════════════════════════════════════════════════════════╣"
Write-Information "║  Resource Group:  $($ResourceGroupName.PadRight(40))║"
Write-Information "║  Environment:     $($Environment.PadRight(40))║"
Write-Information "║  Container App:   $($ContainerAppName.PadRight(40))║"
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

# Check Docker is running
Write-Information "🐳 Checking Docker..."
$dockerVersion = docker --version 2>$null
if (-not $dockerVersion) {
    Write-Error "Docker is not installed or not running. Please install and start Docker Desktop."
    exit 1
}
Write-Information "✅ Docker: $dockerVersion"
Write-Information ""

# Get or create Azure Container Registry
Write-Information "📦 Checking Azure Container Registry..."
if (-not $RegistryName) {
    # Try to find existing ACR in the resource group
    $existingAcr = az acr list --resource-group $ResourceGroupName --query "[0].name" -o tsv 2>$null
    if ($existingAcr) {
        $RegistryName = $existingAcr
        Write-Information "   Found existing ACR: $RegistryName"
    }
    else {
        # Generate a unique ACR name
        $uniqueSuffix = -join ((97..122) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
        $RegistryName = "acrtransgrid$uniqueSuffix"
        Write-Information "   Creating new ACR: $RegistryName..."
        az acr create `
            --resource-group $ResourceGroupName `
            --name $RegistryName `
            --sku Basic `
            --admin-enabled true `
            --output none
        Write-Information "   ✅ ACR created: $RegistryName"
    }
}

$RegistryServer = "$RegistryName.azurecr.io"
Write-Information "   Registry: $RegistryServer"
Write-Information ""

# Build Docker image using ACR Tasks (no local Docker required)
Write-Information "🏗️  Building Docker image using ACR Tasks..."
$fullImageName = "${ImageName}:${ImageTag}"
Write-Information "   Image: $RegistryServer/$fullImageName"

Push-Location $MockServerPath
try {
    az acr build `
        --registry $RegistryName `
        --image $fullImageName `
        --file Dockerfile `
        . `
        --no-logs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ACR build failed!"
        exit 1
    }
    Write-Information "✅ Docker image built and pushed to ACR"
}
finally {
    Pop-Location
}
Write-Information ""

$fullImageNameWithRegistry = "${RegistryServer}/${fullImageName}"

# Get ACR credentials
Write-Information "🔑 Getting ACR credentials..."
$acrCredentials = az acr credential show --name $RegistryName --output json | ConvertFrom-Json
$acrUsername = $acrCredentials.username
$acrPassword = $acrCredentials.passwords[0].value

# Check if Container App exists
Write-Information "🚀 Deploying to Azure Container Apps..."
$existingApp = az containerapp show `
    --name $ContainerAppName `
    --resource-group $ResourceGroupName `
    --output json 2>$null | ConvertFrom-Json

if ($existingApp) {
    Write-Information "   Updating existing Container App: $ContainerAppName"
    az containerapp update `
        --name $ContainerAppName `
        --resource-group $ResourceGroupName `
        --image $fullImageNameWithRegistry `
        --output none
}
else {
    Write-Information "   Creating new Container App: $ContainerAppName"
    az containerapp create `
        --name $ContainerAppName `
        --resource-group $ResourceGroupName `
        --environment $ContainerAppsEnvironmentName `
        --image $fullImageNameWithRegistry `
        --registry-server $RegistryServer `
        --registry-username $acrUsername `
        --registry-password $acrPassword `
        --target-port 8080 `
        --ingress external `
        --cpu 0.5 `
        --memory 1.0Gi `
        --min-replicas 1 `
        --max-replicas 3 `
        --env-vars "ASPNETCORE_ENVIRONMENT=Development" "ASPNETCORE_URLS=http://+:8080" `
        --output none
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Container App deployment failed!"
    exit 1
}
Write-Information "✅ Container App deployed successfully"
Write-Information ""

# Get the Container App URL
$containerApp = az containerapp show `
    --name $ContainerAppName `
    --resource-group $ResourceGroupName `
    --output json | ConvertFrom-Json

$mockServerUrl = "https://$($containerApp.properties.configuration.ingress.fqdn)"

Write-Information ""
Write-Information "╔══════════════════════════════════════════════════════════════╗"
Write-Information "║                    DEPLOYMENT COMPLETE                       ║"
Write-Information "╠══════════════════════════════════════════════════════════════╣"
Write-Information "║  Container App: $($ContainerAppName.PadRight(42))║"
Write-Information "║  Status:        ✅ SUCCESS                                   ║"
Write-Information "╚══════════════════════════════════════════════════════════════╝"
Write-Information ""
Write-Information "🔗 Mock Server Endpoints:"
Write-Information "   Home:     $mockServerUrl"
Write-Information "   Swagger:  $mockServerUrl/swagger"
Write-Information "   OpsAPI:   $mockServerUrl/api/OpsApi"
Write-Information ""
Write-Information "📝 To update Logic Apps with the new endpoint, run:"
Write-Information "   .\deploy-logicapps.ps1 -ResourceGroupName $ResourceGroupName -OpsApiEndpoint `"$mockServerUrl`""
Write-Information ""

# Return the URL for use in other scripts
return $mockServerUrl
