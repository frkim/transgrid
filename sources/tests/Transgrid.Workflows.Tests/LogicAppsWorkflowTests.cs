using System.Text.Json;
using System.Text.Json.Nodes;

namespace Transgrid.Workflows.Tests;

/// <summary>
/// Tests to validate Logic Apps Standard workflow definitions for the RNE Export use case.
/// Follows the Logic Apps Standard folder structure with workflow.json in each workflow folder.
/// </summary>
public class LogicAppsWorkflowTests
{
    // Logic Apps Standard structure: sources/logicapps/{workflow-name}/workflow.json
    private static readonly string LogicAppsPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "sources", "logicapps");

    // Legacy ARM template location (kept for backwards compatibility during migration)
    private static readonly string LegacyWorkflowsPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "infra", "workflows");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Gets the workflow path based on folder structure.
    /// Standard format: {LogicAppsPath}/{workflowName}/workflow.json
    /// Legacy format: {LegacyWorkflowsPath}/{workflowName}.json
    /// </summary>
    private static string GetWorkflowPath(string workflowName)
    {
        // First try Standard format
        var standardPath = Path.Combine(LogicAppsPath, workflowName, "workflow.json");
        if (File.Exists(standardPath))
            return standardPath;
        
        // Fall back to legacy format
        var legacyPath = Path.Combine(LegacyWorkflowsPath, $"{workflowName}.json");
        if (File.Exists(legacyPath))
            return legacyPath;
        
        // Default to standard path (will fail with proper message)
        return standardPath;
    }

    #region Daily Export Workflow Tests

    [Fact]
    public void DailyExportWorkflow_ShouldBeValidJson()
    {
        // Arrange
        var workflowPath = GetWorkflowPath("rne-daily-export");

        // Act
        var json = File.ReadAllText(workflowPath);
        var parseAction = () => JsonDocument.Parse(json);

        // Assert
        parseAction.Should().NotThrow("workflow should be valid JSON");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveDefinitionSection()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");

        // Assert
        workflow.Should().ContainKey("definition");
        var definition = workflow["definition"]!.AsObject();
        definition.Should().ContainKey("$schema");
        definition.Should().ContainKey("triggers");
        definition.Should().ContainKey("actions");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveRecurrenceTrigger()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var triggers = workflow["definition"]!["triggers"]!.AsObject();

        // Assert
        triggers.Should().ContainKey("Recurrence_Daily_Export");
        var trigger = triggers["Recurrence_Daily_Export"]!.AsObject();
        trigger["type"]!.GetValue<string>().Should().Be("Recurrence");
        
        var recurrence = trigger["recurrence"]!.AsObject();
        recurrence["frequency"]!.GetValue<string>().Should().Be("Day");
        recurrence["interval"]!.GetValue<int>().Should().Be(1);
        recurrence["timeZone"]!.GetValue<string>().Should().Be("Romance Standard Time");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveScheduleAt6AM()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var trigger = workflow["definition"]!["triggers"]!["Recurrence_Daily_Export"]!.AsObject();
        var schedule = trigger["recurrence"]!["schedule"]!.AsObject();

        // Assert
        var hours = schedule["hours"]!.AsArray();
        hours.Should().ContainSingle();
        hours[0]!.GetValue<string>().Should().Be("6");

        var minutes = schedule["minutes"]!.AsArray();
        minutes.Should().ContainSingle();
        minutes[0]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveRequiredActions()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        var requiredActions = new[]
        {
            "Initialize_RunId",
            "Initialize_TravelDate",
            "Initialize_FailedTrains",
            "Initialize_SuccessCount",
            "Query_GraphQL_API",
            "Check_API_Response_Status",
            "Condition_Has_Failures"
        };

        foreach (var action in requiredActions)
        {
            actions.Should().ContainKey(action, $"workflow should contain action '{action}'");
        }
    }

    [Fact]
    public void DailyExportWorkflow_ShouldQueryGraphQLEndpoint()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var queryAction = workflow["definition"]!["actions"]!["Query_GraphQL_API"]!.AsObject();

        // Assert
        queryAction["type"]!.GetValue<string>().Should().Be("Http");
        
        var inputs = queryAction["inputs"]!.AsObject();
        inputs["method"]!.GetValue<string>().Should().Be("POST");
        inputs["uri"]!.GetValue<string>().Should().Contain("OPS_API_ENDPOINT");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveForEachWithNestedActions()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();

        // Assert
        forEach["type"]!.GetValue<string>().Should().Be("Foreach");
        
        var nestedActions = forEach["actions"]!.AsObject();
        nestedActions.Should().ContainKey("Validate_Required_Fields");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveUploadActionsInScope()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var validateFields = forEach["actions"]!["Validate_Required_Fields"]!.AsObject();
        var uploadScope = validateFields["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Archive_to_Blob");
        uploadActions.Should().ContainKey("Upload_to_Primary_SFTP");
        uploadActions.Should().ContainKey("Upload_to_Backup_SFTP");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveErrorHandlingScope()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var validateFields = forEach["actions"]!["Validate_Required_Fields"]!.AsObject();
        var nestedActions = validateFields["actions"]!.AsObject();

        // Assert
        nestedActions.Should().ContainKey("Scope_Handle_Failure");
        var errorHandler = nestedActions["Scope_Handle_Failure"]!.AsObject();
        errorHandler["type"]!.GetValue<string>().Should().Be("Scope");
        
        var runAfter = errorHandler["runAfter"]!.AsObject();
        runAfter.Should().ContainKey("Scope_Upload_Success");
    }

    #endregion

    #region D+2 Export Workflow Tests

    [Fact]
    public void D2ExportWorkflow_ShouldBeValidJson()
    {
        // Arrange
        var workflowPath = GetWorkflowPath("rne-d2-export");

        // Act
        var json = File.ReadAllText(workflowPath);
        var parseAction = () => JsonDocument.Parse(json);

        // Assert
        parseAction.Should().NotThrow("workflow should be valid JSON");
    }

    [Fact]
    public void D2ExportWorkflow_ShouldHaveRecurrenceTriggerAt630AM()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-d2-export.json");
        var triggers = workflow["definition"]!["triggers"]!.AsObject();

        // Assert
        triggers.Should().ContainKey("Recurrence_D2_Export");
        var trigger = triggers["Recurrence_D2_Export"]!.AsObject();
        
        var schedule = trigger["recurrence"]!["schedule"]!.AsObject();
        var hours = schedule["hours"]!.AsArray();
        hours[0]!.GetValue<string>().Should().Be("6");
        
        var minutes = schedule["minutes"]!.AsArray();
        minutes[0]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public void D2ExportWorkflow_ShouldCalculateFutureDate()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-d2-export.json");
        var initAction = workflow["definition"]!["actions"]!["Initialize_TravelDate_D2"]!.AsObject();
        var inputs = initAction["inputs"]!.AsObject();
        var variables = inputs["variables"]!.AsArray();
        var travelDateVar = variables[0]!.AsObject();

        // Assert
        var value = travelDateVar["value"]!.GetValue<string>();
        value.Should().Contain("addDays", "should calculate D+2 date using addDays");
        value.Should().Contain("2", "should add 2 days");
    }

    [Fact]
    public void D2ExportWorkflow_ShouldHaveRequiredStructure()
    {
        // Arrange - D+2 workflow uses the original simpler structure
        var d2Workflow = LoadWorkflow("rne-d2-export.json");
        var d2Actions = d2Workflow["definition"]!["actions"]!.AsObject();

        // Assert - D+2 should have core actions at top level (note: uses _D2 suffix for some actions)
        var requiredActions = new[] { "Initialize_TravelDate_D2", "Initialize_FailedTrains", "Query_GraphQL_API_D2", 
            "Filter_Active_FR_GB_Plans", "For_Each_Train_Plan", "Condition_Has_Failures" };
        
        foreach (var action in requiredActions)
        {
            d2Actions.Should().ContainKey(action, $"D+2 workflow should have {action} action");
        }
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveEnhancedStructure()
    {
        // Arrange - Daily export has enhanced structure with additional validation layers
        var dailyWorkflow = LoadWorkflow("rne-daily-export.json");
        var dailyActions = dailyWorkflow["definition"]!["actions"]!.AsObject();

        // Assert - Daily should have enhanced actions at top level
        var topLevelActions = new[] { "Initialize_TravelDate", "Initialize_RunId", "Initialize_SuccessCount", 
            "Initialize_FailedTrains", "Check_API_Response_Status", "Condition_Has_Failures" };
        
        foreach (var action in topLevelActions)
        {
            dailyActions.Should().ContainKey(action, $"Daily workflow should have {action} action");
        }

        // Verify nested API call and validation structure
        var checkStatus = dailyActions["Check_API_Response_Status"]!.AsObject();
        checkStatus["actions"]!.AsObject().Should().ContainKey("Check_GraphQL_Errors");
    }

    #endregion

    #region Retry Failed Workflow Tests

    [Fact]
    public void RetryWorkflow_ShouldBeValidJson()
    {
        // Arrange
        var workflowPath = GetWorkflowPath("rne-retry-failed");

        // Act
        var json = File.ReadAllText(workflowPath);
        var parseAction = () => JsonDocument.Parse(json);

        // Assert
        parseAction.Should().NotThrow("workflow should be valid JSON");
    }

    [Fact]
    public void RetryWorkflow_ShouldHaveRecurrenceTrigger()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed.json");
        var triggers = workflow["definition"]!["triggers"]!.AsObject();

        // Assert
        triggers.Should().ContainKey("Recurrence_Retry_Failed");
        var trigger = triggers["Recurrence_Retry_Failed"]!.AsObject();
        trigger["type"]!.GetValue<string>().Should().Be("Recurrence");
    }

    [Fact]
    public void RetryWorkflow_ShouldQueryTableStorage()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert - Standard Logic Apps use ServiceProvider type for Table Storage
        actions.Should().ContainKey("Query_Failed_Exports");
        var queryAction = actions["Query_Failed_Exports"]!.AsObject();
        var actionType = queryAction["type"]!.GetValue<string>();
        actionType.Should().BeOneOf("ApiConnection", "ServiceProvider", 
            "Query action should use ApiConnection or ServiceProvider");
    }

    [Fact]
    public void RetryWorkflow_ShouldHaveMaxRetriesVariable()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        actions.Should().ContainKey("Initialize_MaxRetries");
        var initAction = actions["Initialize_MaxRetries"]!.AsObject();
        initAction["type"]!.GetValue<string>().Should().Be("InitializeVariable");
        
        var variables = initAction["inputs"]!["variables"]!.AsArray();
        var maxRetriesVar = variables[0]!.AsObject();
        maxRetriesVar["name"]!.GetValue<string>().Should().Be("MaxRetries");
        maxRetriesVar["value"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public void RetryWorkflow_ShouldDeleteSuccessfulRetries()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Failed_Export"]!.AsObject();
        var retryScope = forEach["actions"]!["Scope_Retry_Upload"]!.AsObject();
        var retryActions = retryScope["actions"]!.AsObject();

        // Assert
        retryActions.Should().ContainKey("Delete_Retry_Record");
    }

    [Fact]
    public void RetryWorkflow_ShouldIncrementRetryCountOnFailure()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Failed_Export"]!.AsObject();
        var incrementScope = forEach["actions"]!["Scope_Increment_Retry"]!.AsObject();
        var incrementActions = incrementScope["actions"]!.AsObject();

        // Assert
        incrementActions.Should().ContainKey("Update_Retry_Count");
        
        // Verify it runs after failure
        var runAfter = incrementScope["runAfter"]!.AsObject();
        runAfter.Should().ContainKey("Scope_Retry_Upload");
        var conditions = runAfter["Scope_Retry_Upload"]!.AsArray();
        conditions.Select(c => c!.GetValue<string>()).Should().Contain("Failed");
    }

    #endregion

    #region HTTP Trigger Workflow Tests

    [Fact]
    public void HttpTriggerWorkflow_ShouldBeValidJson()
    {
        // Arrange
        var workflowPath = GetWorkflowPath("rne-http-trigger");

        // Act
        var json = File.ReadAllText(workflowPath);
        var parseAction = () => JsonDocument.Parse(json);

        // Assert
        parseAction.Should().NotThrow("workflow should be valid JSON");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveDefinitionSection()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");

        // Assert
        workflow.Should().ContainKey("definition");
        var definition = workflow["definition"]!.AsObject();
        definition.Should().ContainKey("$schema");
        definition.Should().ContainKey("triggers");
        definition.Should().ContainKey("actions");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveHttpTrigger()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var triggers = workflow["definition"]!["triggers"]!.AsObject();

        // Assert
        triggers.Should().ContainKey("Manual_HTTP_Trigger");
        var trigger = triggers["Manual_HTTP_Trigger"]!.AsObject();
        trigger["type"]!.GetValue<string>().Should().Be("Request");
        trigger["kind"]!.GetValue<string>().Should().Be("Http");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldAcceptPostMethod()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var trigger = workflow["definition"]!["triggers"]!["Manual_HTTP_Trigger"]!.AsObject();
        var inputs = trigger["inputs"]!.AsObject();

        // Assert
        inputs["method"]!.GetValue<string>().Should().Be("POST");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveRequestSchema()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var trigger = workflow["definition"]!["triggers"]!["Manual_HTTP_Trigger"]!.AsObject();
        var schema = trigger["inputs"]!["schema"]!.AsObject();
        var properties = schema["properties"]!.AsObject();

        // Assert
        properties.Should().ContainKey("travelDate");
        properties.Should().ContainKey("exportType");
        properties.Should().ContainKey("trainIds");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveRequiredActions()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        var requiredActions = new[]
        {
            "Initialize_RunId",
            "Initialize_TravelDate",
            "Initialize_ExportType",
            "Initialize_RequestedTrainIds",
            "Initialize_SuccessCount",
            "Initialize_FailedTrains",
            "Query_GraphQL_API",
            "Parse_GraphQL_Response",
            "Filter_Active_FR_GB_Plans",
            "For_Each_Train_Plan",
            "Response_Success"
        };

        foreach (var action in requiredActions)
        {
            actions.Should().ContainKey(action, $"workflow should contain action '{action}'");
        }
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldReturnResponse()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        actions.Should().ContainKey("Response_Success");
        var responseAction = actions["Response_Success"]!.AsObject();
        responseAction["type"]!.GetValue<string>().Should().Be("Response");
        
        var inputs = responseAction["inputs"]!.AsObject();
        inputs["statusCode"]!.GetValue<int>().Should().Be(200);
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveResponseWithSummary()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var responseAction = workflow["definition"]!["actions"]!["Response_Success"]!.AsObject();
        var inputs = responseAction["inputs"]!.AsObject();
        var body = inputs["body"]!.AsObject();

        // Assert
        body.Should().ContainKey("status");
        body.Should().ContainKey("exportType");
        body.Should().ContainKey("travelDate");
        body.Should().ContainKey("summary");
        body.Should().ContainKey("failedTrains");
        body.Should().ContainKey("message");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveSameUploadLogicAsScheduled()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var uploadScope = forEach["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Archive_to_Blob", 
            "HTTP trigger should archive to blob storage");
        uploadActions.Should().ContainKey("Upload_to_Primary_SFTP", 
            "HTTP trigger should upload to primary SFTP");
        uploadActions.Should().ContainKey("Upload_to_Backup_SFTP", 
            "HTTP trigger should upload to backup SFTP");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldHaveConnectionParameters()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");

        // Assert - Standard Logic Apps use empty parameters in workflow.json
        // Connections are defined separately in connections.json
        workflow.Should().ContainKey("definition", "workflow should have definition section");
        
        // For Standard Logic Apps, the workflow.json has empty parameters
        // and connections are managed via connections.json
        var definition = workflow["definition"]!.AsObject();
        definition.Should().ContainKey("parameters", "definition should have parameters section");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldSupportSpecificTrainFiltering()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        actions.Should().ContainKey("Initialize_RequestedTrainIds");
        actions.Should().ContainKey("Condition_Filter_Specific_Trains");
        
        var conditionAction = actions["Condition_Filter_Specific_Trains"]!.AsObject();
        conditionAction["type"]!.GetValue<string>().Should().Be("If");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldTrackSuccessCount()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var initAction = workflow["definition"]!["actions"]!["Initialize_SuccessCount"]!.AsObject();

        // Assert
        initAction["type"]!.GetValue<string>().Should().Be("InitializeVariable");
        
        var variables = initAction["inputs"]!["variables"]!.AsArray();
        var successCountVar = variables[0]!.AsObject();
        successCountVar["name"]!.GetValue<string>().Should().Be("SuccessCount");
        successCountVar["type"]!.GetValue<string>().Should().Be("integer");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldIncrementSuccessOnUpload()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var uploadScope = forEach["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Increment_Success_Count");
        var incrementAction = uploadActions["Increment_Success_Count"]!.AsObject();
        incrementAction["type"]!.GetValue<string>().Should().Be("IncrementVariable");
    }

    [Fact]
    public void HttpTriggerWorkflow_ShouldTrackFailedTrains()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-http-trigger.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var failureScope = forEach["actions"]!["Scope_Handle_Failure"]!.AsObject();
        var failureActions = failureScope["actions"]!.AsObject();

        // Assert
        failureActions.Should().ContainKey("Append_Failed_Train");
        var appendAction = failureActions["Append_Failed_Train"]!.AsObject();
        appendAction["type"]!.GetValue<string>().Should().Be("AppendToArrayVariable");
    }

    #endregion

    #region Cross-Workflow Validation Tests

    [Fact]
    public void AllWorkflows_ShouldHaveDefinition()
    {
        // Arrange
        var workflows = new[] { "rne-daily-export", "rne-d2-export", "rne-retry-failed", "rne-http-trigger" };

        foreach (var workflowName in workflows)
        {
            // Act
            var workflow = LoadWorkflow(workflowName);
            
            // Assert - Standard Logic Apps should have definition section
            workflow.Should().ContainKey("definition", $"{workflowName} should have definition section");
            
            var definition = workflow["definition"]!.AsObject();
            definition.Should().ContainKey("actions", $"{workflowName} should have actions");
            definition.Should().ContainKey("triggers", $"{workflowName} should have triggers");
        }
    }

    [Fact]
    public void AllWorkflows_ShouldHaveSchemaVersion()
    {
        // Arrange
        var workflows = new[] { "rne-daily-export", "rne-d2-export", "rne-retry-failed", "rne-http-trigger" };

        foreach (var workflowName in workflows)
        {
            // Act
            var workflow = LoadWorkflow(workflowName);
            var schema = workflow["definition"]!["$schema"]!.GetValue<string>();

            // Assert
            schema.Should().Contain("Microsoft.Logic", $"{workflowName} should use Logic Apps schema");
        }
    }

    [Fact]
    public void AllWorkflows_ShouldHaveContentVersion()
    {
        // Arrange
        var workflows = new[] { "rne-daily-export", "rne-d2-export", "rne-retry-failed", "rne-http-trigger" };

        foreach (var workflowName in workflows)
        {
            // Act
            var workflow = LoadWorkflow(workflowName);
            var contentVersion = workflow["definition"]!["contentVersion"]!.GetValue<string>();

            // Assert
            contentVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+\.\d+$", 
                $"{workflowName} should have valid content version");
        }
    }

    [Theory]
    [InlineData("rne-daily-export")]
    [InlineData("rne-d2-export")]
    [InlineData("rne-retry-failed")]
    [InlineData("rne-http-trigger")]
    public void Workflow_ShouldHaveValidActionDependencies(string workflowName)
    {
        // Arrange
        var workflow = LoadWorkflow(workflowName);
        var actions = workflow["definition"]!["actions"]!.AsObject();
        var actionNames = actions.Select(a => a.Key).ToHashSet();

        // Act & Assert - verify runAfter references exist
        foreach (var action in actions)
        {
            var actionObj = action.Value!.AsObject();
            
            if (actionObj.ContainsKey("runAfter"))
            {
                var runAfter = actionObj["runAfter"]!.AsObject();
                
                foreach (var dependency in runAfter)
                {
                    // Dependency should be a valid action name
                    actionNames.Should().Contain(dependency.Key, 
                        $"Action '{action.Key}' references non-existent action '{dependency.Key}'");
                }
            }
        }
    }

    #endregion

    #region Schema Validation Tests

    [Theory]
    [InlineData("rne-daily-export")]
    [InlineData("rne-d2-export")]
    [InlineData("rne-retry-failed")]
    [InlineData("rne-http-trigger")]
    public void Workflow_ShouldHaveValidTriggerType(string workflowName)
    {
        // Arrange
        var workflow = LoadWorkflow(workflowName);
        var triggers = workflow["definition"]!["triggers"]!.AsObject();

        // Assert
        triggers.Should().NotBeEmpty($"{workflowName} should have at least one trigger");

        foreach (var trigger in triggers)
        {
            var triggerObj = trigger.Value!.AsObject();
            triggerObj.Should().ContainKey("type", $"trigger '{trigger.Key}' should have a type");
            
            var validTypes = new[] { "Recurrence", "Request", "ApiConnection", "Http" };
            var triggerType = triggerObj["type"]!.GetValue<string>();
            validTypes.Should().Contain(triggerType, 
                $"trigger type '{triggerType}' should be valid");
        }
    }

    [Theory]
    [InlineData("rne-daily-export")]
    [InlineData("rne-d2-export")]
    [InlineData("rne-retry-failed")]
    [InlineData("rne-http-trigger")]
    public void Workflow_AllActionsShouldHaveValidTypes(string workflowName)
    {
        // Arrange
        var workflow = LoadWorkflow(workflowName);
        var validActionTypes = new HashSet<string>
        {
            "Http", "ApiConnection", "Compose", "Query", "Foreach", "If", "Scope",
            "InitializeVariable", "SetVariable", "IncrementVariable", "Response",
            "ParseJson", "Select", "Join", "Function", "Terminate", "AppendToArrayVariable",
            "ServiceProvider"  // Standard Logic Apps use ServiceProvider type
        };

        // Act
        ValidateActionTypes(workflow["definition"]!["actions"]!.AsObject(), validActionTypes, workflowName);
    }

    private void ValidateActionTypes(JsonObject actions, HashSet<string> validTypes, string context)
    {
        foreach (var action in actions)
        {
            var actionObj = action.Value!.AsObject();
            actionObj.Should().ContainKey("type", $"action '{action.Key}' in {context} should have a type");

            var actionType = actionObj["type"]!.GetValue<string>();
            validTypes.Should().Contain(actionType, 
                $"action type '{actionType}' in {context} should be valid");

            // Recursively check nested actions
            if (actionObj.ContainsKey("actions"))
            {
                ValidateActionTypes(actionObj["actions"]!.AsObject(), validTypes, $"{context}/{action.Key}");
            }
        }
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void DailyExportWorkflow_FilterShouldExcludeEvolutionPlans()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var filterAction = checkErrors["else"]!["actions"]!["Filter_Active_FR_GB_Plans"]!.AsObject();
        var inputs = filterAction["inputs"]!.AsObject();
        var where = inputs["where"]!.GetValue<string>();

        // Assert
        where.Should().Contain("EVOLUTION", "filter should reference EVOLUTION plan type");
        where.Should().Contain("not", "filter should exclude EVOLUTION plans");
    }

    [Fact]
    public void DailyExportWorkflow_FilterShouldOnlyIncludeActivePlans()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var filterAction = checkErrors["else"]!["actions"]!["Filter_Active_FR_GB_Plans"]!.AsObject();
        var inputs = filterAction["inputs"]!.AsObject();
        var where = inputs["where"]!.GetValue<string>();

        // Assert
        where.Should().Contain("ACTIVE", "filter should check for ACTIVE status");
    }

    [Fact]
    public void DailyExportWorkflow_FilterShouldOnlyIncludeFranceAndUK()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var filterAction = checkErrors["else"]!["actions"]!["Filter_Active_FR_GB_Plans"]!.AsObject();
        var inputs = filterAction["inputs"]!.AsObject();
        var where = inputs["where"]!.GetValue<string>();

        // Assert
        where.Should().Contain("FR", "filter should include France");
        where.Should().Contain("GB", "filter should include Great Britain");
    }

    [Fact]
    public void D2ExportWorkflow_ShouldUploadToBlob()
    {
        // Arrange - D+2 uses the original structure
        var workflow = LoadWorkflow("rne-d2-export.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var uploadScope = forEach["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Archive_to_Blob", 
            "D+2 workflow should upload to blob storage");
    }

    [Fact]
    public void D2ExportWorkflow_ShouldUploadToBothSFTPServers()
    {
        // Arrange - D+2 uses the original structure
        var workflow = LoadWorkflow("rne-d2-export.json");
        var forEach = workflow["definition"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var uploadScope = forEach["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Upload_to_Primary_SFTP", 
            "D+2 workflow should upload to primary SFTP");
        uploadActions.Should().ContainKey("Upload_to_Backup_SFTP", 
            "D+2 workflow should upload to backup SFTP");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldUploadToBlob()
    {
        // Arrange - Daily export uses the new nested structure with API validation and input validation
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var validateFields = forEach["actions"]!["Validate_Required_Fields"]!.AsObject();
        var uploadScope = validateFields["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Archive_to_Blob", 
            "Daily export workflow should upload to blob storage");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldUploadToBothSFTPServers()
    {
        // Arrange - Daily export uses the new nested structure with API validation and input validation
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var validateFields = forEach["actions"]!["Validate_Required_Fields"]!.AsObject();
        var uploadScope = validateFields["actions"]!["Scope_Upload_Success"]!.AsObject();
        var uploadActions = uploadScope["actions"]!.AsObject();

        // Assert
        uploadActions.Should().ContainKey("Upload_to_Primary_SFTP", 
            "Daily export workflow should upload to primary SFTP");
        uploadActions.Should().ContainKey("Upload_to_Backup_SFTP", 
            "Daily export workflow should upload to backup SFTP");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveConcurrencyLimit()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();

        // Assert
        forEach.Should().ContainKey("runtimeConfiguration");
        var runtimeConfig = forEach["runtimeConfiguration"]!.AsObject();
        runtimeConfig.Should().ContainKey("concurrency");
        var concurrency = runtimeConfig["concurrency"]!.AsObject();
        concurrency["repetitions"]!.GetValue<int>().Should().BeGreaterThan(0);
    }

    [Fact]
    public void RetryWorkflow_ShouldFilterByRetryCount()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-retry-failed");
        var queryAction = workflow["definition"]!["actions"]!["Query_Failed_Exports"]!.AsObject();
        var inputs = queryAction["inputs"]!.AsObject();

        // Assert - Standard Logic Apps use ServiceProvider with parameters
        var actionType = queryAction["type"]!.GetValue<string>();
        if (actionType == "ServiceProvider")
        {
            // Standard Logic Apps structure
            var parameters = inputs["parameters"]!.AsObject();
            parameters.Should().ContainKey("filter", "should have filter parameter");
            var filter = parameters["filter"]!.GetValue<string>();
            filter.Should().Contain("RetryCount", "query should filter by RetryCount");
        }
        else
        {
            // Legacy ApiConnection structure
            var filter = inputs["queries"]!["$filter"]!.GetValue<string>();
            filter.Should().Contain("RetryCount", "query should filter by RetryCount");
            filter.Should().Contain("MaxRetries", "query should reference MaxRetries variable");
        }
    }

    #endregion

    #region Connection Validation Tests

    [Fact]
    public void ConnectionsJson_ShouldExist()
    {
        // Arrange - Standard Logic Apps use a shared connections.json file
        var connectionsPath = Path.Combine(LogicAppsPath, "connections.json");

        // Assert
        File.Exists(connectionsPath).Should().BeTrue("connections.json should exist in Logic Apps folder");
    }

    [Fact]
    public void ConnectionsJson_ShouldHaveRequiredConnections()
    {
        // Arrange
        var connectionsPath = Path.Combine(LogicAppsPath, "connections.json");
        var json = File.ReadAllText(connectionsPath);
        var connections = JsonNode.Parse(json)!.AsObject();

        // Assert - Standard Logic Apps connections structure
        connections.Should().ContainKey("managedApiConnections", "should have managed API connections");
        
        var managedConnections = connections["managedApiConnections"]!.AsObject();
        managedConnections.Should().ContainKey("azureblob", "should have blob storage connection");
        managedConnections.Should().ContainKey("sftpprimary", "should have primary SFTP connection");
        managedConnections.Should().ContainKey("sftpbackup", "should have backup SFTP connection");
        managedConnections.Should().ContainKey("azuretables", "should have table storage connection");
    }

    [Fact]
    public void HostJson_ShouldExist()
    {
        // Arrange
        var hostPath = Path.Combine(LogicAppsPath, "host.json");

        // Assert
        File.Exists(hostPath).Should().BeTrue("host.json should exist in Logic Apps folder");
    }

    [Fact]
    public void HostJson_ShouldHaveWorkflowsExtensionBundle()
    {
        // Arrange
        var hostPath = Path.Combine(LogicAppsPath, "host.json");
        var json = File.ReadAllText(hostPath);
        var host = JsonNode.Parse(json)!.AsObject();

        // Assert
        host.Should().ContainKey("extensionBundle", "host.json should have extensionBundle");
        var bundle = host["extensionBundle"]!.AsObject();
        bundle["id"]!.GetValue<string>().Should().Contain("Workflows", 
            "should use Workflows extension bundle for Logic Apps Standard");
    }

    #endregion

    #region Best Practices Validation Tests

    [Theory]
    [InlineData("rne-daily-export")]
    [InlineData("rne-http-trigger")]
    public void Workflow_ShouldHaveCorrelationId(string workflowName)
    {
        // Arrange
        var workflow = LoadWorkflow(workflowName);
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert - Should have RunId for correlation
        actions.Should().ContainKey("Initialize_RunId", $"{workflowName} should initialize a correlation ID");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveRetryPolicyOnGraphQL()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export");
        var queryAction = workflow["definition"]!["actions"]!["Query_GraphQL_API"]!.AsObject();
        var inputs = queryAction["inputs"]!.AsObject();

        // Assert
        inputs.Should().ContainKey("retryPolicy", "GraphQL query should have retry policy");
        var retryPolicy = inputs["retryPolicy"]!.AsObject();
        retryPolicy["type"]!.GetValue<string>().Should().Be("exponential");
        retryPolicy["count"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveTimeoutOnGraphQL()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var queryAction = workflow["definition"]!["actions"]!["Query_GraphQL_API"]!.AsObject();

        // Assert
        queryAction.Should().ContainKey("limit", "GraphQL query should have timeout limit");
        var limit = queryAction["limit"]!.AsObject();
        limit.Should().ContainKey("timeout");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldCheckAPIResponseStatus()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        actions.Should().ContainKey("Check_API_Response_Status", "workflow should validate API response status");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldCheckGraphQLErrors()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var innerActions = checkStatus["actions"]!.AsObject();

        // Assert
        innerActions.Should().ContainKey("Check_GraphQL_Errors", "workflow should check for GraphQL errors in response");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveSuccessCounter()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var actions = workflow["definition"]!["actions"]!.AsObject();

        // Assert
        actions.Should().ContainKey("Initialize_SuccessCount", "workflow should track successful exports");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldHaveTrackedProperties()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var queryAction = workflow["definition"]!["actions"]!["Query_GraphQL_API"]!.AsObject();

        // Assert
        queryAction.Should().ContainKey("trackedProperties", "GraphQL query should have tracked properties for diagnostics");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldUseCoalesceForNullSafety()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var filterAction = checkErrors["else"]!["actions"]!["Filter_Active_FR_GB_Plans"]!.AsObject();
        var inputs = filterAction["inputs"]!.AsObject();
        var from = inputs["from"]!.GetValue<string>();

        // Assert
        from.Should().Contain("coalesce", "filter should use coalesce for null safety");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldValidateRequiredFields()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var nestedActions = forEach["actions"]!.AsObject();

        // Assert
        nestedActions.Should().ContainKey("Validate_Required_Fields", "workflow should validate required fields before processing");
    }

    [Fact]
    public void DailyExportWorkflow_ShouldClassifyFailureTypes()
    {
        // Arrange
        var workflow = LoadWorkflow("rne-daily-export.json");
        var checkStatus = workflow["definition"]!["actions"]!["Check_API_Response_Status"]!.AsObject();
        var checkErrors = checkStatus["actions"]!["Check_GraphQL_Errors"]!.AsObject();
        var forEach = checkErrors["else"]!["actions"]!["For_Each_Train_Plan"]!.AsObject();
        var validateFields = forEach["actions"]!["Validate_Required_Fields"]!.AsObject();
        var failureScope = validateFields["actions"]!["Scope_Handle_Failure"]!.AsObject();
        var appendAction = failureScope["actions"]!["Append_Failed_Train"]!.AsObject();
        var value = appendAction["inputs"]!["value"]!.AsObject();

        // Assert
        value.Should().ContainKey("failureType", "failed trains should have failure type classification");
        value.Should().ContainKey("runId", "failed trains should include run ID for correlation");
    }

    #endregion

    #region Helper Methods

    private JsonObject LoadWorkflow(string workflowNameOrFile)
    {
        // Support both "rne-daily-export.json" and "rne-daily-export" formats
        var workflowName = workflowNameOrFile.Replace(".json", "");
        var filePath = GetWorkflowPath(workflowName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Workflow file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return JsonNode.Parse(json)!.AsObject();
    }

    #endregion
}
