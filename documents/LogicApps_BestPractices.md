# Logic Apps Workflow Best Practices

This document outlines the best practices implemented in the RNE Export Logic Apps workflows to ensure reliability, observability, and error resilience.

## 1. Retry Policies

### Problem
Transient failures (network issues, service unavailability) can cause workflow failures.

### Solution
Configure exponential backoff retry policies on all HTTP actions:

```json
"retryPolicy": {
  "type": "exponential",
  "count": 4,
  "interval": "PT10S",
  "minimumInterval": "PT5S",
  "maximumInterval": "PT1M"
}
```

### Recommended Settings by Action Type

| Action Type | Retry Count | Min Interval | Max Interval |
|-------------|-------------|--------------|--------------|
| GraphQL API | 4 | 5s | 1m |
| Azure Function | 3 | 2s | 30s |
| Blob Storage | 3 | 2s | 30s |
| SFTP Upload | 3 | 5s | 1m |
| Table Storage | 3 | 2s | 30s |

## 2. Timeout Configuration

### Problem
Long-running operations can hang indefinitely.

### Solution
Set explicit timeouts on HTTP actions:

```json
"limit": {
  "timeout": "PT2M"
}
```

### Recommended Timeouts

| Action | Timeout |
|--------|---------|
| GraphQL Query | 2 minutes |
| Transform Function | 1 minute |
| File Upload | 5 minutes |

## 3. Correlation and Tracing

### Problem
Difficult to trace execution across multiple runs and systems.

### Solution
Initialize a unique RunId at workflow start and include it in all actions:

```json
"Initialize_RunId": {
  "type": "InitializeVariable",
  "inputs": {
    "variables": [{
      "name": "RunId",
      "type": "string",
      "value": "@{guid()}"
    }]
  },
  "trackedProperties": {
    "workflowType": "DailyExport",
    "runId": "@{variables('RunId')}"
  }
}
```

Pass correlation ID in HTTP headers:
```json
"headers": {
  "X-Correlation-Id": "@{variables('RunId')}"
}
```

## 4. Input Validation

### Problem
Invalid or missing data causes unexpected failures.

### Solution
Validate required fields before processing:

```json
"Validate_Train_Data": {
  "type": "If",
  "expression": {
    "and": [
      {
        "not": {
          "equals": [
            "@coalesce(items('For_Each_Train_Plan')?['serviceCode'], '')",
            ""
          ]
        }
      }
    ]
  }
}
```

## 5. Null Safety with Coalesce

### Problem
Null values cause "Object reference not set" errors.

### Solution
Always use `coalesce()` for potentially null values:

```json
// Instead of:
"@body('Parse_Response')?['data']?['items']"

// Use:
"@coalesce(body('Parse_Response')?['data']?['items'], json('[]'))"
```

## 6. GraphQL Error Handling

### Problem
GraphQL APIs return HTTP 200 even with errors in the response body.

### Solution
Check for errors in the response:

```json
"Check_GraphQL_Errors": {
  "type": "If",
  "expression": {
    "and": [{
      "greater": [
        "@length(coalesce(body('Parse_GraphQL_Response')?['errors'], json('[]')))",
        0
      ]
    }]
  },
  "actions": {
    "Terminate_On_GraphQL_Error": {
      "type": "Terminate",
      "inputs": {
        "runStatus": "Failed",
        "runError": {
          "code": "GraphQLError",
          "message": "@{first(body('Parse_GraphQL_Response')?['errors'])?['message']}"
        }
      }
    }
  }
}
```

## 7. Tracked Properties for Diagnostics

### Problem
Limited visibility into workflow execution details.

### Solution
Add tracked properties to key actions for Application Insights:

```json
"trackedProperties": {
  "eventType": "WorkflowEnd",
  "runId": "@{variables('RunId')}",
  "processedCount": "@{variables('ProcessedCount')}",
  "successCount": "@{variables('SuccessCount')}",
  "failedCount": "@{length(variables('FailedTrains'))}"
}
```

## 8. Failure Classification

### Problem
Difficult to identify root cause of failures.

### Solution
Categorize failures by type:

```json
{
  "trainId": "@items('For_Each_Train_Plan')?['serviceCode']",
  "failureReason": "Transform failed: HTTP @{outputs('Transform_JSON_to_XML')['statusCode']}",
  "failureType": "TRANSFORM",  // VALIDATION, TRANSFORM, UPLOAD, UNKNOWN
  "timestamp": "@utcNow()",
  "runId": "@variables('RunId')"
}
```

## 9. Concurrency Control

### Problem
Too many parallel operations overwhelm downstream systems.

### Solution
Configure concurrency limits:

```json
"runtimeConfiguration": {
  "concurrency": {
    "repetitions": 5
  }
}
```

## 10. Graceful Termination

### Problem
Workflows fail silently without clear error messages.

### Solution
Use explicit Terminate action with error details:

```json
"Terminate_On_API_Error": {
  "type": "Terminate",
  "inputs": {
    "runStatus": "Failed",
    "runError": {
      "code": "APIError",
      "message": "GraphQL API returned status @{outputs('Query_GraphQL_API')['statusCode']}"
    }
  }
}
```

## 11. Workflow Metrics

Track key metrics for monitoring:

| Metric | Purpose |
|--------|---------|
| ProcessedCount | Total items processed |
| SuccessCount | Successful uploads |
| FailedCount | Failed items |
| Duration | Workflow execution time |

## 12. HTTP Response Status Validation

### Problem
Assuming success without checking status codes.

### Solution
Always validate HTTP response status:

```json
"Check_GraphQL_Response_Status": {
  "type": "If",
  "expression": {
    "and": [{
      "equals": [
        "@outputs('Query_GraphQL_API')['statusCode']",
        200
      ]
    }]
  }
}
```

## 13. Scoped Error Handling

### Problem
Single failure causes entire workflow to fail.

### Solution
Use Scope actions with `runAfter` for granular error handling:

```json
"Scope_Upload_Success": {
  "type": "Scope",
  "actions": { /* upload actions */ }
},
"Scope_Handle_Failure": {
  "type": "Scope",
  "actions": { /* error handling */ },
  "runAfter": {
    "Scope_Upload_Success": ["Failed", "TimedOut"]
  }
}
```

## 14. Logging Strategy

Log at key points:
1. **Workflow Start** - Record run ID, input parameters
2. **After Filtering** - Record total/filtered counts
3. **On Error** - Record failure details
4. **Workflow End** - Record summary metrics

## 15. Testing Checklist

Before deploying workflows:

- [ ] Test with empty data set (no train plans)
- [ ] Test with invalid data (missing fields)
- [ ] Test with large data set (100+ items)
- [ ] Test API failure scenarios (503, timeout)
- [ ] Test SFTP connection failure
- [ ] Test concurrent execution
- [ ] Verify retry behavior
- [ ] Check Application Insights logs

## Implementation Status

| Feature | Daily Export | D+2 Export | Retry | HTTP Trigger |
|---------|-------------|------------|-------|--------------|
| Retry Policies | ✅ | ✅ | ✅ | ✅ |
| Timeouts | ✅ | ✅ | ✅ | ✅ |
| Correlation ID | ✅ | ✅ | ✅ | ✅ |
| Input Validation | ✅ | ✅ | ✅ | ✅ |
| GraphQL Error Check | ✅ | ✅ | N/A | ✅ |
| Tracked Properties | ✅ | ✅ | ✅ | ✅ |
| Failure Classification | ✅ | ✅ | ✅ | ✅ |
