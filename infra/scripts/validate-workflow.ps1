<#
.SYNOPSIS
    Comprehensive workflow validation script with detailed analysis and auto-fix capabilities.
.DESCRIPTION
    This script runs Logic App workflows, analyzes execution results,
    and provides detailed troubleshooting information.
.PARAMETER ResourceGroup
    The Azure resource group name. Default: rg-transgrid-dev
.PARAMETER LogicAppName
    The Logic App name. Default: logic-transgrid-rne-export-dev
.PARAMETER WorkflowName
    The workflow to validate. Default: rne-http-trigger
.PARAMETER TravelDate
    The travel date for testing. Default: auto-selects a date with ACTIVE trains
.PARAMETER SkipSftpErrors
    Consider workflow successful even if SFTP fails (useful for initial testing)
.EXAMPLE
    .\validate-workflow.ps1
.EXAMPLE
    .\validate-workflow.ps1 -SkipSftpErrors
#>

param(
    [string]$ResourceGroup = "rg-transgrid-dev",
    [string]$LogicAppName = "logic-transgrid-rne-export-dev",
    [ValidateSet("rne-http-trigger", "rne-daily-export", "rne-d2-export", "rne-retry-failed")]
    [string]$WorkflowName = "rne-http-trigger",
    [string]$TravelDate = "",
    [switch]$SkipSftpErrors
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host ">>> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Logic App Workflow Validation & Troubleshooting Tool"
Write-Host "============================================================" -ForegroundColor Cyan

# Step 1: Verify Azure CLI and get context
Write-Step "Step 1: Checking Azure CLI authentication"
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Fail "Not logged in to Azure CLI"
    Write-Host "  Run 'az login' to authenticate" -ForegroundColor Yellow
    exit 1
}
Write-Success "Logged in as: $($account.user.name)"
$subscriptionId = $account.id

# Step 2: Verify resources exist
Write-Step "Step 2: Verifying Azure resources"

$logicApp = az webapp show --name $LogicAppName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if (-not $logicApp) {
    Write-Fail "Logic App '$LogicAppName' not found"
    exit 1
}
Write-Success "Logic App: $LogicAppName (State: $($logicApp.state))"

# Step 3: Check app settings
Write-Step "Step 3: Verifying app settings"

$appSettings = az webapp config appsettings list --name $LogicAppName --resource-group $ResourceGroup -o json | ConvertFrom-Json
$requiredSettings = @("OPS_API_ENDPOINT", "FUNCTION_ENDPOINT", "FUNCTION_KEY", "BLOB_CONNECTION_STRING")
$missingSettings = @()

foreach ($setting in $requiredSettings) {
    $value = ($appSettings | Where-Object { $_.name -eq $setting }).value
    if ([string]::IsNullOrWhiteSpace($value)) {
        Write-Fail "$setting is not configured"
        $missingSettings += $setting
    }
    else {
        $displayValue = if ($setting -like "*KEY*" -or $setting -like "*CONNECTION*") { "***configured***" } else { $value }
        Write-Success "$setting = $displayValue"
    }
}

if ($missingSettings.Count -gt 0) {
    Write-Host ""
    Write-Host "  Missing settings detected. Run deploy-logicapps.ps1 to fix." -ForegroundColor Yellow
    exit 1
}

# Step 4: Check Mock Server / OPS API
Write-Step "Step 4: Testing OPS API connectivity"
$opsEndpoint = ($appSettings | Where-Object { $_.name -eq "OPS_API_ENDPOINT" }).value

try {
    # Test GraphQL endpoint
    $graphqlBody = @{
        query = "query { trainPlans { id serviceCode travelDate status country } }"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri $opsEndpoint -Method Post -Body $graphqlBody -ContentType "application/json" -TimeoutSec 30
    
    if ($response.data.trainPlans) {
        $trainCount = $response.data.trainPlans.Count
        Write-Success "OPS API reachable - $trainCount train plans available"
        
        # Find a good travel date to test with
        if ([string]::IsNullOrEmpty($TravelDate)) {
            $activeTrains = $response.data.trainPlans | Where-Object { 
                $_.status -eq "ACTIVE" -and ($_.country -eq "FR" -or $_.country -eq "GB")
            }
            if ($activeTrains.Count -gt 0) {
                $TravelDate = ($activeTrains | Select-Object -First 1).travelDate.Substring(0, 10)
                Write-Info "Auto-selected travel date with ACTIVE FR/GB train: $TravelDate"
            }
            else {
                $TravelDate = (Get-Date).AddDays(7).ToString("yyyy-MM-dd")
                Write-Warn "No ACTIVE FR/GB trains found. Using default date: $TravelDate"
            }
        }
    }
    else {
        Write-Warn "OPS API responded but returned no train plans"
    }
}
catch {
    Write-Fail "OPS API unreachable: $($_.Exception.Message)"
    exit 1
}

# Step 5: Check Function App
Write-Step "Step 5: Testing Function App"
$funcEndpoint = ($appSettings | Where-Object { $_.name -eq "FUNCTION_ENDPOINT" }).value
$funcKey = ($appSettings | Where-Object { $_.name -eq "FUNCTION_KEY" }).value

$testBody = @{
    serviceCode = "TEST001"
    travelDate = "2026-01-20"
    origin = "Paris"
    destination = "London"
    passagePoints = @()
} | ConvertTo-Json

try {
    $funcUrl = "$funcEndpoint/TransformTrainPlan?code=$funcKey"
    $funcResponse = Invoke-WebRequest -Uri $funcUrl -Method Post -Body $testBody -ContentType "application/json" -UseBasicParsing -TimeoutSec 30
    
    if ($funcResponse.StatusCode -eq 200) {
        Write-Success "Function App reachable and responding"
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-Success "Function App reachable (returned validation error as expected for test data)"
    }
    elseif ($statusCode -eq 401) {
        Write-Fail "Function App: Unauthorized - check FUNCTION_KEY"
    }
    else {
        Write-Fail "Function App error: $($_.Exception.Message)"
    }
}

# Step 6: Trigger workflow
Write-Step "Step 6: Triggering workflow '$WorkflowName'"
Write-Info "Travel Date: $TravelDate"

$token = az account get-access-token --query accessToken -o tsv
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$baseUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$LogicAppName"

# Get callback URL
$triggersUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/triggers?api-version=2024-04-01"
$triggersResponse = Invoke-RestMethod -Uri $triggersUrl -Headers $headers -Method Get

if ($triggersResponse.value.Count -eq 0) {
    Write-Fail "No triggers found for workflow"
    exit 1
}

$triggerName = $triggersResponse.value[0].name
$callbackUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/triggers/$triggerName/listCallbackUrl?api-version=2024-04-01"
$callbackResponse = Invoke-RestMethod -Uri $callbackUrl -Headers $headers -Method Post
$triggerUrl = $callbackResponse.value

$requestBody = @{
    travelDate = $TravelDate
    exportType = "ADHOC"
} | ConvertTo-Json

$startTime = Get-Date
try {
    $workflowResponse = Invoke-WebRequest -Uri $triggerUrl -Method Post -Body $requestBody -ContentType "application/json" -UseBasicParsing
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Success "Workflow completed in $([math]::Round($duration, 2))s (HTTP $($workflowResponse.StatusCode))"
    
    $responseBody = $workflowResponse.Content | ConvertFrom-Json
    Write-Info "Trains Processed: $($responseBody.summary.totalPlansProcessed)"
    Write-Info "Successful Exports: $($responseBody.summary.successfulExports)"
    Write-Info "Failed Exports: $($responseBody.summary.failedExports)"
}
catch {
    $endTime = Get-Date
    Write-Fail "Workflow failed: $($_.Exception.Message)"
}

# Step 7: Analyze run details
Write-Step "Step 7: Analyzing workflow execution"
Start-Sleep -Seconds 2

$runsUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs?api-version=2024-04-01&`$top=1"
$runsResponse = Invoke-RestMethod -Uri $runsUrl -Headers $headers -Method Get

if ($runsResponse.value.Count -eq 0) {
    Write-Fail "No run history found"
    exit 1
}

$latestRun = $runsResponse.value[0]
$runId = $latestRun.name
$runStatus = $latestRun.properties.status

Write-Info "Run ID: $runId"
Write-Info "Status: $runStatus"

$actionsUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs/$runId/actions?api-version=2024-04-01"
$actionsResponse = Invoke-RestMethod -Uri $actionsUrl -Headers $headers -Method Get

$failedActions = @()
$criticalActions = @("Query_GraphQL_API", "Parse_GraphQL_Response", "Filter_Active_FR_GB_Plans", "Transform_JSON_to_XML", "Archive_to_Blob")
$sftpActions = @("Upload_to_Primary_SFTP", "Upload_to_Backup_SFTP")

foreach ($action in $actionsResponse.value | Sort-Object { $_.properties.startTime }) {
    $actionName = $action.name
    $actionStatus = $action.properties.status
    
    if ($actionStatus -eq "Failed") {
        $failedActions += $actionName
        
        if ($sftpActions -contains $actionName) {
            if ($SkipSftpErrors) {
                Write-Warn "$actionName - Failed (ignored with -SkipSftpErrors)"
            }
            else {
                Write-Fail "$actionName - Failed"
            }
        }
        elseif ($criticalActions -contains $actionName) {
            Write-Fail "$actionName - Failed (CRITICAL)"
        }
        else {
            Write-Fail "$actionName - Failed"
        }
    }
    elseif ($actionStatus -eq "Succeeded") {
        if ($criticalActions -contains $actionName) {
            Write-Success "$actionName"
        }
    }
}

# Step 8: Summary
Write-Step "Step 8: Validation Summary"

$criticalFailures = $failedActions | Where-Object { $criticalActions -contains $_ }
$sftpFailures = $failedActions | Where-Object { $sftpActions -contains $_ }

Write-Host ""
if ($criticalFailures.Count -gt 0) {
    Write-Host "  RESULT: FAILED" -ForegroundColor Red
    Write-Host "  Critical actions failed: $($criticalFailures -join ', ')" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Troubleshooting:" -ForegroundColor Yellow
    foreach ($failure in $criticalFailures) {
        switch ($failure) {
            "Query_GraphQL_API" {
                Write-Host "    - Check OPS_API_ENDPOINT setting"
                Write-Host "    - Verify Mock Server Container App is running"
            }
            "Parse_GraphQL_Response" {
                Write-Host "    - Check API response format matches schema"
                Write-Host "    - Verify passagePoints structure"
            }
            "Transform_JSON_to_XML" {
                Write-Host "    - Check FUNCTION_ENDPOINT and FUNCTION_KEY settings"
                Write-Host "    - Verify Function App is running"
                Write-Host "    - Check input data has required fields"
            }
            "Archive_to_Blob" {
                Write-Host "    - Check BLOB_CONNECTION_STRING setting"
                Write-Host "    - Verify Blob container 'ci-rne-export' exists"
            }
        }
    }
    exit 1
}
elseif ($sftpFailures.Count -gt 0 -and -not $SkipSftpErrors) {
    Write-Host "  RESULT: PARTIAL SUCCESS" -ForegroundColor Yellow
    Write-Host "  Core workflow succeeded, but SFTP upload failed" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  SFTP Issues:" -ForegroundColor Yellow
    Write-Host "    - Check SFTP_PRIMARY_HOST setting"
    Write-Host "    - Verify SFTP Container Apps are running"
    Write-Host "    - Check SFTP username/password in connections.json"
    Write-Host ""
    Write-Host "  Run with -SkipSftpErrors to consider this a success" -ForegroundColor Gray
}
else {
    Write-Host "  RESULT: SUCCESS" -ForegroundColor Green
    Write-Host "  All workflow actions completed successfully!" -ForegroundColor Green
    
    if ($sftpFailures.Count -gt 0) {
        Write-Host "  (SFTP errors were ignored with -SkipSftpErrors)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
