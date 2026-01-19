<#
.SYNOPSIS
    Runs and validates Logic App Standard workflows deployed to Azure.
.DESCRIPTION
    This script triggers Logic App workflows, monitors their execution,
    and analyzes the results for troubleshooting.
.PARAMETER ResourceGroup
    The Azure resource group name. Default: rg-transgrid-dev
.PARAMETER LogicAppName
    The Logic App name. Default: logic-transgrid-rne-export-dev
.PARAMETER WorkflowName
    The workflow to run. Default: rne-http-trigger
.PARAMETER TravelDate
    The travel date for the export. Default: today's date
.PARAMETER ExportType
    The export type. Default: ADHOC
.PARAMETER WaitForCompletion
    Wait for workflow to complete and show results. Default: true
.EXAMPLE
    .\run-workflow.ps1 -WorkflowName "rne-http-trigger" -TravelDate "2026-01-20"
#>

param(
    [string]$ResourceGroup = "rg-transgrid-dev",
    [string]$LogicAppName = "logic-transgrid-rne-export-dev",
    [ValidateSet("rne-http-trigger", "rne-daily-export", "rne-d2-export", "rne-retry-failed")]
    [string]$WorkflowName = "rne-http-trigger",
    [string]$TravelDate = (Get-Date).ToString("yyyy-MM-dd"),
    [ValidateSet("DAILY", "D+2", "ADHOC")]
    [string]$ExportType = "ADHOC",
    [bool]$WaitForCompletion = $true,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================="
Write-Host "Logic App Workflow Runner & Validator"
Write-Host "=============================================="
Write-Host ""

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Cyan
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure CLI. Please run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green

# Get Logic App details
Write-Host ""
Write-Host "Getting Logic App details..." -ForegroundColor Cyan
$logicApp = az webapp show --name $LogicAppName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if (-not $logicApp) {
    Write-Host "Logic App '$LogicAppName' not found in resource group '$ResourceGroup'" -ForegroundColor Red
    exit 1
}

Write-Host "  Logic App: $LogicAppName"
Write-Host "  State: $($logicApp.state)"
Write-Host "  URL: $($logicApp.defaultHostName)"

# Get the callback URL for the workflow
Write-Host ""
Write-Host "Getting workflow callback URL..." -ForegroundColor Cyan

$subscriptionId = $account.id
$baseUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$LogicAppName"

# Get access token
$token = az account get-access-token --query accessToken -o tsv
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# List workflow triggers to get the callback URL
$triggersUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/triggers?api-version=2024-04-01"

try {
    $triggersResponse = Invoke-RestMethod -Uri $triggersUrl -Headers $headers -Method Get
    Write-Host "  Found $($triggersResponse.value.Count) trigger(s)" -ForegroundColor Green
} catch {
    Write-Host "  Error getting triggers: $($_.Exception.Message)" -ForegroundColor Red
    
    # Try alternative approach - list workflows
    Write-Host "  Trying to list workflows..." -ForegroundColor Yellow
    $workflowsUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows?api-version=2024-04-01"
    try {
        $workflowsResponse = Invoke-RestMethod -Uri $workflowsUrl -Headers $headers -Method Get
        Write-Host "  Available workflows:" -ForegroundColor Cyan
        foreach ($wf in $workflowsResponse.value) {
            Write-Host "    - $($wf.name) (State: $($wf.properties.state))"
        }
    } catch {
        Write-Host "  Error listing workflows: $($_.Exception.Message)" -ForegroundColor Red
    }
    exit 1
}

# Get trigger callback URL
$triggerName = $triggersResponse.value[0].name
$triggerProps = $triggersResponse.value[0].properties

# Check if it's a scheduled workflow (has recurrence property)
$isScheduledWorkflow = $null -ne $triggerProps.recurrence
$triggerType = if ($isScheduledWorkflow) { "Recurrence" } else { "Request" }
Write-Host "  Trigger: $triggerName (Type: $triggerType)"

if ($isScheduledWorkflow) {
    Write-Host "  Scheduled workflow detected - will use Run Trigger API" -ForegroundColor Yellow
    $triggerUrl = $null
} else {
    $callbackUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/triggers/$triggerName/listCallbackUrl?api-version=2024-04-01"
    
    try {
        $callbackResponse = Invoke-RestMethod -Uri $callbackUrl -Headers $headers -Method Post
        $triggerUrl = $callbackResponse.value
        Write-Host "  Callback URL obtained" -ForegroundColor Green
    } catch {
        Write-Host "  Error getting callback URL: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Prepare request body based on workflow type
Write-Host ""
Write-Host "=============================================="
Write-Host "Running Workflow: $WorkflowName"
Write-Host "=============================================="
Write-Host "  Travel Date: $TravelDate"
Write-Host "  Export Type: $ExportType"
Write-Host ""

$requestBody = @{
    travelDate = $TravelDate
    exportType = $ExportType
} | ConvertTo-Json

# Trigger the workflow
Write-Host "Triggering workflow..." -ForegroundColor Cyan
$startTime = Get-Date

if ($isScheduledWorkflow) {
    # For scheduled workflows, use the Run Trigger API
    $runTriggerUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/triggers/$triggerName/run?api-version=2024-04-01"
    
    try {
        $response = Invoke-WebRequest -Uri $runTriggerUrl -Headers $headers -Method Post -ContentType "application/json" -UseBasicParsing
        $statusCode = $response.StatusCode
        
        Write-Host "  Scheduled workflow triggered successfully" -ForegroundColor Green
        Write-Host "  Response Status: $statusCode" -ForegroundColor Green
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 202 -or $statusCode -eq 200) {
            Write-Host "  Scheduled workflow triggered (Status: $statusCode)" -ForegroundColor Green
        } else {
            Write-Host "  Error triggering workflow: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} else {
    # For HTTP triggered workflows, use the callback URL
    try {
        $response = Invoke-WebRequest -Uri $triggerUrl -Method Post -Body $requestBody -ContentType "application/json" -UseBasicParsing
        $statusCode = $response.StatusCode
        
        Write-Host "  Response Status: $statusCode" -ForegroundColor Green
        
        if ($response.Content) {
            try {
                $responseBody = $response.Content | ConvertFrom-Json
                Write-Host ""
                Write-Host "Response Body:" -ForegroundColor Cyan
                Write-Host ($responseBody | ConvertTo-Json -Depth 10)
            } catch {
                Write-Host "  Raw Response: $($response.Content)"
            }
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = ""
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd()
            $reader.Close()
        } catch {
            $errorBody = $_.Exception.Message
        }
        
        Write-Host "  Response Status: $statusCode" -ForegroundColor Red
        Write-Host ""
        Write-Host "Error Details:" -ForegroundColor Red
        
        try {
            $errorJson = $errorBody | ConvertFrom-Json
            Write-Host ($errorJson | ConvertTo-Json -Depth 10)
        } catch {
            Write-Host $errorBody
        }
    }
}

$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host ""
Write-Host "=============================================="
Write-Host "Execution Summary"
Write-Host "=============================================="
Write-Host "  Duration: $($duration.TotalSeconds.ToString('F2')) seconds"
Write-Host "  Status Code: $statusCode"

# Get run history for analysis
Write-Host ""
Write-Host "=============================================="
Write-Host "Analyzing Run History"
Write-Host "=============================================="

Start-Sleep -Seconds 2  # Wait for run to be registered

$runsUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs?api-version=2024-04-01&`$top=5"

try {
    $runsResponse = Invoke-RestMethod -Uri $runsUrl -Headers $headers -Method Get
    
    if ($runsResponse.value.Count -gt 0) {
        $latestRun = $runsResponse.value[0]
        $runId = $latestRun.name
        $runStatus = $latestRun.properties.status
        
        Write-Host ""
        Write-Host "Latest Run: $runId" -ForegroundColor Cyan
        Write-Host "  Status: $runStatus" -ForegroundColor $(if ($runStatus -eq "Succeeded") { "Green" } elseif ($runStatus -eq "Running") { "Yellow" } else { "Red" })
        Write-Host "  Start Time: $($latestRun.properties.startTime)"
        
        if ($latestRun.properties.endTime) {
            Write-Host "  End Time: $($latestRun.properties.endTime)"
        }
        
        # If still running and we want to wait
        if ($runStatus -eq "Running" -and $WaitForCompletion) {
            Write-Host ""
            Write-Host "Waiting for workflow to complete..." -ForegroundColor Yellow
            
            $waitStart = Get-Date
            while ($runStatus -eq "Running" -and ((Get-Date) - $waitStart).TotalSeconds -lt $TimeoutSeconds) {
                Start-Sleep -Seconds 3
                Write-Host "." -NoNewline
                
                $runDetailUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs/$runId`?api-version=2024-04-01"
                $runDetail = Invoke-RestMethod -Uri $runDetailUrl -Headers $headers -Method Get
                $runStatus = $runDetail.properties.status
            }
            Write-Host ""
            Write-Host "Final Status: $runStatus" -ForegroundColor $(if ($runStatus -eq "Succeeded") { "Green" } else { "Red" })
        }
        
        # Get action details for the run
        Write-Host ""
        Write-Host "Action Results:" -ForegroundColor Cyan
        
        $actionsUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs/$runId/actions?api-version=2024-04-01"
        
        try {
            $actionsResponse = Invoke-RestMethod -Uri $actionsUrl -Headers $headers -Method Get
            
            foreach ($action in $actionsResponse.value | Sort-Object { $_.properties.startTime }) {
                $actionName = $action.name
                $actionStatus = $action.properties.status
                $statusColor = if ($actionStatus -eq "Succeeded") { "Green" } elseif ($actionStatus -eq "Skipped") { "Gray" } elseif ($actionStatus -eq "Running") { "Yellow" } else { "Red" }
                
                Write-Host "  [$actionStatus] $actionName" -ForegroundColor $statusColor
                
                # If action failed, get details
                if ($actionStatus -eq "Failed") {
                    Write-Host ""
                    Write-Host "  FAILED ACTION DETAILS:" -ForegroundColor Red
                    Write-Host "  -----------------------"
                    
                    # Get action input/output
                    $actionDetailUrl = "$baseUrl/hostruntime/runtime/webhooks/workflow/api/management/workflows/$WorkflowName/runs/$runId/actions/$actionName`?api-version=2024-04-01"
                    
                    try {
                        $actionDetail = Invoke-RestMethod -Uri $actionDetailUrl -Headers $headers -Method Get
                        
                        if ($actionDetail.properties.error) {
                            Write-Host "  Error Code: $($actionDetail.properties.error.code)" -ForegroundColor Red
                            Write-Host "  Error Message: $($actionDetail.properties.error.message)" -ForegroundColor Red
                        }
                        
                        # Try to get outputs
                        if ($actionDetail.properties.outputsLink) {
                            try {
                                $outputs = Invoke-RestMethod -Uri $actionDetail.properties.outputsLink.uri -Method Get
                                Write-Host ""
                                Write-Host "  Action Outputs:" -ForegroundColor Yellow
                                Write-Host ($outputs | ConvertTo-Json -Depth 5)
                            } catch {
                                # Outputs may not be available
                            }
                        }
                        
                        # Try to get inputs for context
                        if ($actionDetail.properties.inputsLink) {
                            try {
                                $inputs = Invoke-RestMethod -Uri $actionDetail.properties.inputsLink.uri -Method Get
                                Write-Host ""
                                Write-Host "  Action Inputs:" -ForegroundColor Yellow
                                Write-Host ($inputs | ConvertTo-Json -Depth 5)
                            } catch {
                                # Inputs may not be available
                            }
                        }
                    } catch {
                        Write-Host "  Could not get action details: $($_.Exception.Message)" -ForegroundColor Yellow
                    }
                    
                    Write-Host ""
                }
            }
        } catch {
            Write-Host "  Error getting actions: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        # Show recent runs summary
        Write-Host ""
        Write-Host "Recent Runs:" -ForegroundColor Cyan
        Write-Host "-----------"
        foreach ($run in $runsResponse.value | Select-Object -First 5) {
            $status = $run.properties.status
            $statusColor = if ($status -eq "Succeeded") { "Green" } elseif ($status -eq "Running") { "Yellow" } else { "Red" }
            Write-Host "  $($run.name) - $status ($($run.properties.startTime))" -ForegroundColor $statusColor
        }
        
    } else {
        Write-Host "No runs found for workflow '$WorkflowName'" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error getting run history: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=============================================="
Write-Host "Validation Complete"
Write-Host "=============================================="
