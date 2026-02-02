# Implementation Review

> **Repository:** [https://github.com/frkim/transgrid](https://github.com/frkim/transgrid)

This document reviews the implementation of all use cases and key capabilities described in [Azure Integration Services Use Cases.md](https://github.com/frkim/transgrid/blob/main/documents/Azure%20Integration%20Services%20Use%20Cases.md).

## Summary

| Use Case | Status | Workflows | Functions | Notes |
|----------|--------|-----------|-----------|-------|
| RNE Operational Plans Export | ✅ Implemented | [4 workflows](#workflows) | [1 function](#azure-functions-rne) | Full implementation with retry pattern |
| Salesforce Negotiated Rates Export | ✅ Implemented | [1 workflow](#workflows-1) | [1 function](#azure-functions-salesforce) | Service Bus integration |
| Network Rail CIF Processing | ✅ Implemented | [1 workflow](#workflows-2) | [3 functions](#azure-functions-cif) | Timer-triggered Azure Functions |

---

## Use Case 1: RNE Operational Plans Export

### Triggers

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [Daily export scheduler](#snippet-recurrence-daily-export) | Logic Apps Standard | Workflow Trigger | `Recurrence_Daily_Export` | Runs daily at 06:00 (Romance Standard Time) | ✅ |
| [Future D+2 export scheduler](#snippet-recurrence-d2-export) | Logic Apps Standard | Workflow Trigger | `Recurrence_D2_Export` | Runs daily at 06:30 (Romance Standard Time) | ✅ |
| [Retry failed exports scheduler](#snippet-recurrence-retry-failed) | Logic Apps Standard | Workflow Trigger | `Recurrence_Retry_Failed` | Runs daily at 07:00 (Romance Standard Time) | ✅ |
| [Timezone support](#snippet-recurrence-daily-export) | Logic Apps Standard | Trigger Configuration | `timeZone: "Romance Standard Time"` | Configured in all recurrence triggers | ✅ |

### Processing Capabilities

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [GraphQL API client](#snippet-query-graphql-api) | Logic Apps Standard | HTTP Action | `Query_GraphQL_API` | POST request to Operations GraphQL API | ✅ |
| [Train plan filtering (Country: FR/GB)](#snippet-filter-active-plans) | Logic Apps Standard | Query Action | `Filter_Active_FR_GB_Plans` | Filters plans where country is FR or GB | ✅ |
| [Train plan filtering (Status: ACTIVE)](#snippet-filter-active-plans) | Logic Apps Standard | Query Action | `Filter_Active_FR_GB_Plans` | Filters plans where status is ACTIVE | ✅ |
| [Train plan filtering (Not EVOLUTION)](#snippet-filter-active-plans) | Logic Apps Standard | Query Action | `Filter_Active_FR_GB_Plans` | Filters out EVOLUTION plan types | ✅ |
| [Validate required fields](#snippet-validate-required-fields) | Logic Apps Standard | Condition Action | `Validate_Required_Fields` | Validates serviceCode and id exist | ✅ |
| [JSON to XML transformation](#snippet-transform-train-plan) | Azure Functions | HTTP Function | `TransformTrainPlan` | Transforms JSON to TAF-JSG XML | ✅ |
| [XML schema validation](#snippet-xml-validation-service) | Azure Functions | Service | `XmlValidationService` | Validates against PassengerTrainCompositionProcessMessage v2.1.6 XSD | ✅ |
| [Reference data loading](#snippet-reference-data-service) | Azure Functions | Service | `ReferenceDataService` | Location codes, UIC codes, vehicle info | ✅ |

### Workflows

| Workflow | File Path | Trigger | Description |
|----------|-----------|---------|-------------|
| rne-daily-export | [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json) | Daily at 06:00 | Exports today's train plans |
| rne-d2-export | [sources/logicapps/rne-d2-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-d2-export/workflow.json) | Daily at 06:30 | Exports D+2 (future) train plans |
| rne-retry-failed | [sources/logicapps/rne-retry-failed/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-retry-failed/workflow.json) | Daily at 07:00 | Retries failed exports |
| rne-http-trigger | [sources/logicapps/rne-http-trigger/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-http-trigger/workflow.json) | HTTP POST | Manual/on-demand export |

### Destinations

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [Azure Blob Storage (archive)](#snippet-archive-to-blob) | Azure Blob Storage | Container | `ci-rne-export` | Path: YYYY-MM/YYYY-MM-DD/filename.xml | ✅ |
| [RNE SFTP Server (primary)](#snippet-upload-to-sftp) | Container Apps + SFTP | Service Provider Connection | `SftpPrimary` | Upload to /upload/YYYY-MM-DD/ | ✅ |
| [RNE SFTP Server (backup)](#snippet-sftp-connection) | Container Apps + SFTP | Service Provider Connection | `SftpBackup` | Backup SFTP on port 2222 | ✅ |
| [Object Store (retry state)](#snippet-failed-exports-table) | Azure Table Storage | Table | `FailedExports` | Stores failed train IDs for retry | ✅ |

### Azure Functions (RNE)

| Function | File Path | Description |
|----------|-----------|-------------|
| TransformTrainPlan | [TransformTrainPlan.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformTrainPlan.cs) | JSON to XML transformation |

### Services (RNE)

| Service | File Path | Description |
|---------|-----------|-------------|
| XmlTransformService | [XmlTransformService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/XmlTransformService.cs) | TAF-JSG XML generation |
| XmlValidationService | [XmlValidationService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/XmlValidationService.cs) | XML schema validation |
| ReferenceDataService | [ReferenceDataService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/ReferenceDataService.cs) | Location/vehicle reference data |

---

## Use Case 2: Salesforce Negotiated Rates Export

### Triggers

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [Salesforce Platform Event listener](#snippet-servicebus-trigger) | Azure Service Bus | Queue Trigger | `When_messages_are_available_in_queue` | Receives events from Service Bus queue | ✅ |
| [Platform Event channel](#snippet-servicebus-connection) | Azure Service Bus | Queue | `salesforce-events` | Queue for NegotiatedRateExtract events | ✅ |

> **Note:** Instead of direct Salesforce Streaming API integration (which requires managed connector), the implementation uses Azure Service Bus as an intermediary. A mock server simulates Salesforce Platform Events and publishes to Service Bus.

### Processing Capabilities

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [APEX Web Service call](#snippet-get-negotiated-rates) | Logic Apps Standard | HTTP Action | `Get_Negotiated_Rates_From_API` | Calls mock Salesforce API to get rates | ✅ |
| [Parallel processing (scatter-gather)](#snippet-process-route) | Azure Functions | Service Method | `ProcessRoute()` | Processes 3 routes: IDL_S3, GDS_AIR, BENE | ✅ |
| [Route 1 - IDL/S3 filtering](#snippet-filter-idl-s3) | Azure Functions | Service | `CsvGeneratorService.FilterForIdlS3()` | Filters GND BE, GND NL, FCE, IDL records | ✅ |
| [Route 2 - GDS Air filtering](#snippet-filter-gds-air) | Azure Functions | Service | `CsvGeneratorService.FilterForGdsAir()` | Filters Amadeus, Apollo, Galileo, Sabre | ✅ |
| [Route 3 - BeNe filtering](#snippet-filter-bene) | Azure Functions | Service | `CsvGeneratorService.FilterForBeNe()` | Filters GND BE, GND NL for external partners | ✅ |
| [CSV file generation (IDL/S3)](#snippet-generate-idl-s3-csv) | Azure Functions | Service | `CsvGeneratorService.GenerateIdlS3Csv()` | Account Manager, Account Name, Unique Code, Type, Road, Tariff Codes, Discounts, Action Type | ✅ |
| [CSV file generation (GDS Air)](#snippet-generate-gds-air-csv) | Azure Functions | Service | `CsvGeneratorService.GenerateGdsAirCsv()` | Account Manager, Account Name, Unique Code, GDS Used, PCC, Road, Tariff Codes, Dates, Action Type | ✅ |
| [CSV file generation (BeNe)](#snippet-generate-bene-csv) | Azure Functions | Service | `CsvGeneratorService.GenerateBeneCsv()` | Account Manager, Account Name, Unique Code, Distributor, Road, Tariff Codes, Action Type | ✅ |
| [Salesforce record updates](#snippet-update-salesforce-status) | Logic Apps Standard | HTTP Action | `Update_Salesforce_Status` | Updates Extract_Requested, B2b_Status, dates | ✅ |

### Workflows

| Workflow | File Path | Trigger | Description |
|----------|-----------|---------|-------------|
| sf-negotiated-rates | [sources/logicapps/sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json) | Service Bus message | Processes negotiated rate extract events |

### Destinations

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [Azure Blob Storage (internal - S3)](#snippet-upload-csv-blob) | Azure Blob Storage | Container | `salesforce-internal` | Path: S3/Priority Files/yyyyMM/... or S3/Normal Files/... | ✅ |
| [Azure Blob Storage (internal - GDS Air)](#snippet-upload-csv-blob) | Azure Blob Storage | Container | `salesforce-internal` | Path: GDS_AIR/yyyyMM/... | ✅ |
| [Azure Blob Storage (external - BeNe)](#snippet-upload-csv-blob) | Azure Blob Storage | Container | `salesforce-external` | Path: yyyyMM/yyyyMMdd_HHmmss_... | ✅ |
| [Salesforce record updates](#snippet-update-salesforce-status) | Mock Server API | HTTP Endpoint | `/api/Salesforce/updateExtractStatus` | Updates B2b_Status, Extract dates | ✅ |

### Azure Functions (Salesforce)

| Function | File Path | Description |
|----------|-----------|-------------|
| TransformNegotiatedRates | [TransformNegotiatedRates.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformNegotiatedRates.cs) | JSON to CSV transformation for all 3 routes |

### Services (Salesforce)

| Service | File Path | Description |
|---------|-----------|-------------|
| CsvGeneratorService | [CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs) | CSV generation + filtering |

---

## Use Case 3: Network Rail CIF File Processing

### Triggers

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [Daily poller (every 60 min)](#snippet-process-cif-updates) | Azure Functions | Timer Trigger | `ProcessCifUpdates` | Runs hourly at :00 | ✅ |
| [Weekly full download](#snippet-process-cif-full-refresh) | Azure Functions | Timer Trigger | `ProcessCifFullRefresh` | Runs every Sunday at 02:00 UTC | ✅ |

### Processing Capabilities

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| [HTTP file download](#snippet-gzip-decompression) | Azure Functions | Service | `CifProcessingService` | Downloads from Network Rail NTROD API | ✅ (simulated) |
| [GZIP decompression](#snippet-gzip-decompression) | Azure Functions | Service | `ProcessCifStreamAsync()` | Uses GZipStream for decompression | ✅ |
| [Text splitting (NDJSON)](#snippet-process-cif-content) | Azure Functions | Service | `ProcessCifContentAsync()` | Splits by newline character | ✅ |
| [JSON parsing (line by line)](#snippet-process-line) | Azure Functions | Service | `ProcessLineAsync()` | Parses each line as CIF record | ✅ |
| [Filter: CIF_stp_indicator = N](#snippet-process-line) | Azure Functions | Service | `ProcessLineAsync()` | Only planning schedules (N = New) | ✅ |
| [Filter: schedule location exists](#snippet-process-line) | Azure Functions | Service | `ProcessLineAsync()` | Must have location data | ✅ |
| [Reference data lookup](#snippet-station-mappings) | Azure Functions | Service | `StationMappings` | TIPLOC to CRS/Station mapping | ✅ |
| [Transform to event](#snippet-transform-to-event) | Azure Functions | Service | `TransformToEvent()` | Creates InfrastructurePathwayConfirmed event | ✅ |
| [Deduplication check](#snippet-deduplication) | Azure Functions | Service | `_processedKeys` HashSet | Prevents duplicate event publishing | ✅ |
| Protobuf serialization | - | - | - | Not implemented - using JSON event format | ⚠️ Simplified |
| gRPC client | - | - | - | Not implemented - simulated publish | ⚠️ Simplified |

### Workflows

| Workflow | File Path | Trigger | Description |
|----------|-----------|---------|-------------|
| nr-cif-processing | [sources/logicapps/nr-cif-processing/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/nr-cif-processing/workflow.json) | HTTP POST | On-demand CIF processing orchestration |

### Destinations

| Requirement | Azure Service | Component Type | Element Name | Description | Status |
|-------------|---------------|----------------|--------------|-------------|--------|
| Internal Message Store (gRPC) | - | - | `SimulatePublishAsync()` | Simulated gRPC publish to message store | ⚠️ Simulated |
| [CIF Archive](#snippet-blob-connection) | Azure Blob Storage | Container | `cif-archive` | Archive of processed CIF files | ✅ |
| [Processing Logs](#snippet-table-connection) | Azure Table Storage | Table | `CifProcessingLogs` | Processing statistics and status | ✅ |
| [Deduplication State](#snippet-deduplication) | Azure Table Storage | Table | `CifDeduplication` | Tracks processed schedules | ✅ |

### Azure Functions (CIF)

| Function | File Path | Trigger | Description |
|----------|-----------|---------|-------------|
| ProcessCifUpdates | [ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs) | Timer (hourly) | Processes CIF updates |
| ProcessCifFullRefresh | [ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs) | Timer (weekly) | Full timetable refresh |
| ProcessCifOnDemand | [ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs) | HTTP POST | On-demand processing |
| TransformCifSchedule | [ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs) | HTTP POST | Single schedule transformation |

### Services (CIF)

| Service | File Path | Description |
|---------|-----------|-------------|
| CifProcessingService | [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs) | CIF parsing, filtering, transformation |
| ReferenceDataService | [ReferenceDataService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/ReferenceDataService.cs) | Station/location mappings |

---

## Infrastructure Components

| Component | Azure Service | Resource Name | Description | File Path |
|-----------|---------------|---------------|-------------|-----------|
| Storage Account | Azure Storage | `st{baseName}{uniqueSuffix}` | Blob, Table, File storage | [infra/main.bicep](https://github.com/frkim/transgrid/blob/main/infra/main.bicep) |
| Logic Apps | Logic Apps Standard | `{baseName}-rne-export` | Workflow orchestration | [infra/modules/logic-app.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/logic-app.bicep) |
| Function App | Azure Functions | `{baseName}-transform` | Data transformation | [infra/modules/function-app.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/function-app.bicep) |
| SFTP Primary | Container Apps | `sftp-rne-primary` | Primary SFTP server | [infra/modules/sftp-server.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/sftp-server.bicep) |
| SFTP Backup | Container Apps | `sftp-rne-backup` | Backup SFTP server | [infra/modules/sftp-server.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/sftp-server.bicep) |
| Mock Server | Container Apps | `mockserver-transgrid-mock` | API simulation | [infra/modules/mock-server.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/mock-server.bicep) |
| Service Bus | Azure Service Bus | `sb-{baseName}-{environment}` | Message broker for Salesforce | [infra/modules/service-bus.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/service-bus.bicep) |
| Redis Cache | Azure Cache for Redis | `redis-{baseName}-{environment}` | CIF deduplication cache | [infra/modules/redis-cache.bicep](https://github.com/frkim/transgrid/blob/main/infra/modules/redis-cache.bicep) |
| Log Analytics | Log Analytics | `log-{baseName}-{environment}` | Monitoring and logging | [infra/main.bicep](https://github.com/frkim/transgrid/blob/main/infra/main.bicep) |
| Application Insights | Application Insights | `appi-{baseName}-{environment}` | APM and telemetry | [infra/main.bicep](https://github.com/frkim/transgrid/blob/main/infra/main.bicep) |
| Virtual Network | Azure VNet | `vnet-{baseName}-{environment}` | Network isolation | [infra/main.bicep](https://github.com/frkim/transgrid/blob/main/infra/main.bicep) |
| Container Apps Environment | Container Apps | `cae-{baseName}-{environment}` | Container hosting | [infra/main.bicep](https://github.com/frkim/transgrid/blob/main/infra/main.bicep) |

---

## Connections Configuration

| Connection Name | Provider | Purpose | File Path |
|-----------------|----------|---------|-----------|
| serviceBus | Service Bus | Salesforce event messages | [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json) |
| AzureBlob | Azure Blob Storage | File storage/archive | [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json) |
| AzureTables | Azure Table Storage | State management/retry | [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json) |
| SftpPrimary | SFTP | Primary RNE SFTP | [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json) |
| SftpBackup | SFTP | Backup RNE SFTP | [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json) |

---

## Key Capabilities Coverage Summary

### Use Case 1: RNE Operational Plans Export

| Capability | Status | Implementation Notes |
|------------|--------|---------------------|
| Cron scheduling with timezone support | ✅ | Logic Apps Recurrence triggers with Romance Standard Time |
| GraphQL API client | ✅ | HTTP action with POST to /graphql endpoint |
| JSON to XML transformation | ✅ | Azure Function TransformTrainPlan |
| XML schema validation | ✅ | XmlValidationService with TAF-JSG v2.1.6 |
| Azure Blob Storage connector | ✅ | AzureBlob service provider connection |
| SFTP connector | ✅ | SftpPrimary and SftpBackup connections |
| Object Store / persistent cache for retry | ✅ | Azure Table Storage (FailedExports table) |

### Use Case 2: Salesforce Negotiated Rates Export

| Capability | Status | Implementation Notes |
|------------|--------|---------------------|
| Salesforce Platform Event subscription | ⚠️ | Via Service Bus (mock server simulates Salesforce) |
| Salesforce APEX Web Service invocation | ✅ | HTTP action to mock Salesforce API |
| Salesforce SOQL queries | ⚠️ | Implemented in mock server |
| Salesforce record updates | ✅ | HTTP action to updateExtractStatus endpoint |
| Parallel processing (scatter-gather) | ✅ | Foreach with concurrency=3 + route processing |
| Complex data transformation | ✅ | CsvGeneratorService with filtering |
| CSV file generation | ✅ | GenerateIdlS3Csv, GenerateGdsAirCsv, GenerateBeneCsv |
| Azure Blob Storage connector | ✅ | Uploads to salesforce-internal and salesforce-external |

### Use Case 3: Network Rail CIF File Processing

| Capability | Status | Implementation Notes |
|------------|--------|---------------------|
| Cron scheduling | ✅ | Timer triggers: hourly updates + weekly refresh |
| HTTP file download (large files) | ⚠️ | Simulated with generated sample data |
| GZIP decompression | ✅ | GZipStream in CifProcessingService |
| Text splitting and JSON parsing | ✅ | NDJSON line-by-line processing |
| Complex filtering logic | ✅ | CIF_stp_indicator=N, location validation |
| Protobuf serialization | ⚠️ | Simplified to JSON event format |
| gRPC client | ⚠️ | Simulated with logging |
| Reference data lookup | ✅ | StationMappings dictionary |

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Fully implemented |
| ⚠️ | Partially implemented or simplified |
| ❌ | Not implemented |

---

## Notes and Recommendations

### Implemented Simplifications

1. **Salesforce Integration**: Instead of direct Salesforce Streaming API, uses Azure Service Bus with a mock server that simulates Platform Events.

2. **Network Rail API**: Uses generated sample CIF data instead of actual Network Rail NTROD API calls.

3. **gRPC/Protobuf**: Uses JSON event format instead of Protobuf serialization for message store integration.

4. **Reference Data**: Hardcoded reference data in services instead of external data source (Azure Table Storage or CosmosDB).

### Production Recommendations

1. **Salesforce Connector**: Consider using Azure Logic Apps Salesforce connector for direct Platform Event subscription if licensed.

2. **Network Rail API**: Implement actual HTTP download from Network Rail NTROD API with proper authentication.

3. **gRPC Integration**: Implement actual gRPC client with Protobuf serialization for message store.

4. **External Reference Data**: Move reference data to Azure Table Storage or CosmosDB for easier management.

5. **Secrets Management**: Use Azure Key Vault for all sensitive configuration (currently using app settings).

---

## Code Snippets

### Use Case 1: RNE Operational Plans Export

<a id="snippet-recurrence-daily-export"></a>
#### Recurrence_Daily_Export Trigger

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Recurrence_Daily_Export": {
  "type": "Recurrence",
  "recurrence": {
    "frequency": "Day",
    "interval": 1,
    "schedule": {
      "hours": ["6"],
      "minutes": [0]
    },
    "timeZone": "Romance Standard Time"
  }
}
```

<a id="snippet-recurrence-d2-export"></a>
#### Recurrence_D2_Export Trigger

> **Source:** [sources/logicapps/rne-d2-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-d2-export/workflow.json)

```json
"Recurrence_D2_Export": {
  "type": "Recurrence",
  "recurrence": {
    "frequency": "Day",
    "interval": 1,
    "schedule": {
      "hours": ["6"],
      "minutes": [30]
    },
    "timeZone": "Romance Standard Time"
  }
}
```

<a id="snippet-recurrence-retry-failed"></a>
#### Recurrence_Retry_Failed Trigger

> **Source:** [sources/logicapps/rne-retry-failed/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-retry-failed/workflow.json)

```json
"Recurrence_Retry_Failed": {
  "type": "Recurrence",
  "recurrence": {
    "frequency": "Day",
    "interval": 1,
    "schedule": {
      "hours": ["7"],
      "minutes": [0]
    },
    "timeZone": "Romance Standard Time"
  }
}
```

<a id="snippet-query-graphql-api"></a>
#### Query_GraphQL_API Action

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Query_GraphQL_API": {
  "type": "Http",
  "inputs": {
    "method": "POST",
    "uri": "@{appsetting('OPS_API_ENDPOINT')}/graphql",
    "headers": {
      "Content-Type": "application/json",
      "X-Correlation-Id": "@{variables('RunId')}"
    },
    "body": {
      "query": "query GetTrainPlans($travelDate: Date!) { trainPlans(filter: { travelDate: $travelDate }) { id serviceCode pathway travelDate passagePoints { locationCode arrivalTime departureTime } origin destination status planType country } }",
      "variables": {
        "travelDate": "@{variables('TravelDate')}"
      }
    },
    "retryPolicy": {
      "type": "exponential",
      "count": 4,
      "interval": "PT10S"
    }
  }
}
```

<a id="snippet-filter-active-plans"></a>
#### Filter_Active_FR_GB_Plans Action

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Filter_Active_FR_GB_Plans": {
  "type": "Query",
  "inputs": {
    "from": "@coalesce(body('Parse_GraphQL_Response')?['data']?['trainPlans'], json('[]'))",
    "where": "@and(or(equals(item()?['country'], 'FR'), equals(item()?['country'], 'GB')), equals(item()?['status'], 'ACTIVE'), not(equals(item()?['planType'], 'EVOLUTION')))"
  }
}
```

<a id="snippet-validate-required-fields"></a>
#### Validate_Required_Fields Condition

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Validate_Required_Fields": {
  "type": "If",
  "expression": {
    "and": [
      {
        "not": {
          "equals": ["@coalesce(items('For_Each_Train_Plan')?['serviceCode'], '')", ""]
        }
      },
      {
        "not": {
          "equals": ["@coalesce(items('For_Each_Train_Plan')?['id'], '')", ""]
        }
      }
    ]
  }
}
```

<a id="snippet-transform-train-plan"></a>
#### TransformTrainPlan Function

> **Source:** [sources/functions/Transgrid.Functions/Functions/TransformTrainPlan.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformTrainPlan.cs)

```csharp
[Function("TransformTrainPlan")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    // 1. Parse input JSON
    var trainPlan = JsonSerializer.Deserialize<TrainPlanInput>(requestBody);
    
    // 2. Validate required fields
    var validationErrors = ValidateInput(trainPlan);
    
    // 3. Load reference data
    var referenceData = await _referenceDataService.GetReferenceDataAsync();
    
    // 4. Transform to XML
    string xml = _xmlTransformService.Transform(trainPlan, referenceData);
    
    // 5. Validate XML
    var xmlValidation = _xmlValidationService.Validate(xml);
    
    // 6. Return XML response
    response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
    await response.WriteStringAsync(xml);
    return response;
}
```

<a id="snippet-xml-validation-service"></a>
#### XmlValidationService

> **Source:** [sources/functions/Transgrid.Functions/Services/XmlValidationService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/XmlValidationService.cs)

```csharp
public ValidationResult ValidateWithDetails(string xml)
{
    var errors = new List<ValidationError>();
    
    // Validate required elements exist
    var requiredElements = new[]
    {
        "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:MessageHeader/ptcpm:MessageIdentifier",
        "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:MessageHeader/ptcpm:MessageType",
        "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TrainNumber",
        "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TravelDate"
    };

    foreach (var xpath in requiredElements)
    {
        var node = doc.SelectSingleNode(xpath, ns);
        if (node == null || string.IsNullOrWhiteSpace(node.InnerText))
        {
            errors.Add(new ValidationError { Message = $"Required element missing" });
        }
    }

    return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
}
```

<a id="snippet-reference-data-service"></a>
#### ReferenceDataService

> **Source:** [sources/functions/Transgrid.Functions/Services/ReferenceDataService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/ReferenceDataService.cs)

```csharp
public class ReferenceDataService : IReferenceDataService
{
    private readonly ReferenceData _referenceData = new ReferenceData
    {
        Locations = new Dictionary<string, LocationReference>
        {
            ["FRPNO"] = new() { Code = "FRPNO", Name = "Paris Nord", Country = "FR", UicCode = "8727100", Type = "STATION" },
            ["GBSTP"] = new() { Code = "GBSTP", Name = "London St Pancras", Country = "GB", UicCode = "7015400", Type = "STATION" },
            ["GBEBF"] = new() { Code = "GBEBF", Name = "Ebbsfleet International", Country = "GB", UicCode = "7015440", Type = "STATION" },
            ["BEBMI"] = new() { Code = "BEBMI", Name = "Brussels Midi", Country = "BE", UicCode = "8814001", Type = "STATION" },
            ["NLAMA"] = new() { Code = "NLAMA", Name = "Amsterdam Centraal", Country = "NL", UicCode = "8400058", Type = "STATION" }
        },
        Vehicles = new Dictionary<string, VehicleReference>
        {
            ["E320"] = new() { VehicleNumber = "E320", VehicleType = "EMU", TareWeight = 965, MaxPassengers = 900 }
        }
    };
}
```

<a id="snippet-archive-to-blob"></a>
#### Archive_to_Blob Action

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Archive_to_Blob": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "AzureBlob",
      "operationId": "uploadBlob",
      "serviceProviderId": "/serviceProviders/AzureBlob"
    },
    "parameters": {
      "containerName": "ci-rne-export",
      "blobName": "@{formatDateTime(utcNow(), 'yyyy-MM')}/@{formatDateTime(utcNow(), 'yyyy-MM-dd')}/@{items('For_Each_Train_Plan')?['serviceCode']}_@{variables('TravelDate')}.xml",
      "content": "@body('Transform_JSON_to_XML')"
    }
  }
}
```

<a id="snippet-upload-to-sftp"></a>
#### Upload_to_Primary_SFTP Action

> **Source:** [sources/logicapps/rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

```json
"Upload_to_Primary_SFTP": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "SftpPrimary",
      "operationId": "uploadFileContent",
      "serviceProviderId": "/serviceProviders/Sftp"
    },
    "parameters": {
      "filePath": "/upload/@{formatDateTime(utcNow(), 'yyyy-MM-dd')}/@{items('For_Each_Train_Plan')?['serviceCode']}_@{variables('TravelDate')}.xml",
      "content": "@body('Transform_JSON_to_XML')",
      "overWriteFileIfExists": true
    }
  },
  "retryPolicy": {
    "type": "exponential",
    "count": 3,
    "interval": "PT10S"
  }
}
```

<a id="snippet-failed-exports-table"></a>
#### Insert_Failed_Export_Record Action

> **Source:** [sources/logicapps/rne-retry-failed/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-retry-failed/workflow.json)

```json
"Insert_Failed_Export_Record": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "AzureTables",
      "operationId": "upsertEntity",
      "serviceProviderId": "/serviceProviders/AzureTables"
    },
    "parameters": {
      "tableName": "FailedExports",
      "entity": {
        "PartitionKey": "@{items('For_Each_Failed_Train')?['travelDate']}",
        "RowKey": "@{guid()}",
        "TrainId": "@{items('For_Each_Failed_Train')?['trainId']}",
        "FailureReason": "@{items('For_Each_Failed_Train')?['failureReason']}",
        "RetryCount": 0
      }
    }
  }
}
```

---

### Use Case 2: Salesforce Negotiated Rates Export

<a id="snippet-servicebus-trigger"></a>
#### Service Bus Queue Trigger

> **Source:** [sources/logicapps/sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"When_messages_are_available_in_queue": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "serviceBus",
      "operationId": "receiveQueueMessages",
      "serviceProviderId": "/serviceProviders/serviceBus"
    },
    "parameters": {
      "queueName": "@appsetting('SERVICEBUS_QUEUE_NAME')",
      "isSessionsEnabled": false
    }
  },
  "splitOn": "@triggerOutputs()?['body']"
}
```

<a id="snippet-get-negotiated-rates"></a>
#### Get_Negotiated_Rates_From_API Action

> **Source:** [sources/logicapps/sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Get_Negotiated_Rates_From_API": {
  "type": "Http",
  "inputs": {
    "method": "POST",
    "uri": "@{appsetting('SALESFORCE_API_ENDPOINT')}/api/Salesforce/getNegotiatedRates",
    "headers": {
      "Content-Type": "application/json",
      "X-Correlation-Id": "@{variables('RunId')}"
    },
    "body": {
      "ids": "@body('Parse_Event_Data')?['negotiatedRateIds']"
    },
    "retryPolicy": {
      "type": "exponential",
      "count": 4,
      "interval": "PT10S"
    }
  }
}
```

<a id="snippet-process-route"></a>
#### ProcessRoute Method

> **Source:** [sources/functions/Transgrid.Functions/Functions/TransformNegotiatedRates.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformNegotiatedRates.cs)

```csharp
private RouteExtractResult ProcessRoute(string route, List<NegotiatedRateInput> rates)
{
    var result = new RouteExtractResult { RouteCode = route };

    switch (route.ToUpperInvariant())
    {
        case "IDL_S3":
            result.RouteName = "IDL/S3 (Internal Distribution)";
            filteredRates = _csvGeneratorService.FilterForIdlS3(rates).ToList();
            csvContent = _csvGeneratorService.GenerateIdlS3Csv(filteredRates);
            break;

        case "GDS_AIR":
            result.RouteName = "GDS Air (Travel Agents)";
            filteredRates = _csvGeneratorService.FilterForGdsAir(rates).ToList();
            csvContent = _csvGeneratorService.GenerateGdsAirCsv(filteredRates);
            break;

        case "BENE":
            result.RouteName = "BeNe (External Partners)";
            filteredRates = _csvGeneratorService.FilterForBeNe(rates).ToList();
            csvContent = _csvGeneratorService.GenerateBeneCsv(filteredRates);
            break;
    }

    result.CsvContent = csvContent;
    result.Success = true;
    return result;
}
```

<a id="snippet-filter-idl-s3"></a>
#### FilterForIdlS3 Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
private static readonly string[] IdlS3RecordTypes = { "GND BE", "GND NL", "FCE", "IDL" };

public IEnumerable<NegotiatedRateInput> FilterForIdlS3(IEnumerable<NegotiatedRateInput> rates)
{
    return rates.Where(r => 
        IdlS3RecordTypes.Any(t => r.CodeRecordType.Contains(t, StringComparison.OrdinalIgnoreCase)));
}
```

<a id="snippet-filter-gds-air"></a>
#### FilterForGdsAir Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
private static readonly string[] GdsAirRecordTypes = { "Amadeus", "Apollo", "Galileo", "Sabre" };

public IEnumerable<NegotiatedRateInput> FilterForGdsAir(IEnumerable<NegotiatedRateInput> rates)
{
    return rates.Where(r => 
        !string.IsNullOrWhiteSpace(r.GdsUsed) && 
        GdsAirRecordTypes.Any(t => r.GdsUsed.Contains(t, StringComparison.OrdinalIgnoreCase)));
}
```

<a id="snippet-filter-bene"></a>
#### FilterForBeNe Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
private static readonly string[] BeNeRecordTypes = { "GND BE", "GND NL" };

public IEnumerable<NegotiatedRateInput> FilterForBeNe(IEnumerable<NegotiatedRateInput> rates)
{
    return rates.Where(r => 
        !string.IsNullOrWhiteSpace(r.Distributor) && 
        BeNeRecordTypes.Any(t => r.CodeRecordType.Contains(t, StringComparison.OrdinalIgnoreCase)));
}
```

<a id="snippet-generate-idl-s3-csv"></a>
#### GenerateIdlS3Csv Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
public string GenerateIdlS3Csv(IEnumerable<NegotiatedRateInput> rates)
{
    var sb = new StringBuilder();
    sb.AppendLine("Account Manager,Account Name,Unique Code,Type,Road,Tariff Codes,Discounts,Action Type");
    
    foreach (var rate in rates)
    {
        var tariffCodes = string.Join("|", rate.TariffCodes);
        var discounts = string.Join("|", rate.Discounts.Select(d => $"{d.Value}%"));
        sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.CodeRecordType)},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{EscapeCsv(discounts)},{EscapeCsv(rate.ActionType)}");
    }
    
    return sb.ToString();
}
```

<a id="snippet-generate-gds-air-csv"></a>
#### GenerateGdsAirCsv Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
public string GenerateGdsAirCsv(IEnumerable<NegotiatedRateInput> rates)
{
    var sb = new StringBuilder();
    sb.AppendLine("Account Manager,Account Name,Unique Code,GDS Used,PCC,Road,Tariff Codes,Valid From,Valid To,Action Type");
    
    foreach (var rate in rates)
    {
        var tariffCodes = string.Join("|", rate.TariffCodes);
        sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.GdsUsed ?? "")},{EscapeCsv(rate.Pcc ?? "")},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{rate.ValidFrom?.ToString("yyyy-MM-dd")},{rate.ValidTo?.ToString("yyyy-MM-dd")},{EscapeCsv(rate.ActionType)}");
    }
    
    return sb.ToString();
}
```

<a id="snippet-generate-bene-csv"></a>
#### GenerateBeneCsv Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
public string GenerateBeneCsv(IEnumerable<NegotiatedRateInput> rates)
{
    var sb = new StringBuilder();
    sb.AppendLine("Account Manager,Account Name,Unique Code,Distributor,Road,Tariff Codes,Action Type");
    
    foreach (var rate in rates)
    {
        var tariffCodes = string.Join("|", rate.TariffCodes);
        sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.Distributor ?? "")},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{EscapeCsv(rate.ActionType)}");
    }
    
    return sb.ToString();
}
```

<a id="snippet-update-salesforce-status"></a>
#### Update_Salesforce_Status Action

> **Source:** [sources/logicapps/sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Update_Salesforce_Status": {
  "type": "Http",
  "inputs": {
    "method": "POST",
    "uri": "@{appsetting('SALESFORCE_API_ENDPOINT')}/api/Salesforce/updateExtractStatus",
    "headers": {
      "Content-Type": "application/json",
      "X-Correlation-Id": "@{variables('RunId')}"
    },
    "body": {
      "ids": "@body('Parse_Event_Data')?['negotiatedRateIds']",
      "status": "@if(equals(length(variables('FailedRoutes')), 0), 'Extracted', 'PartiallyExtracted')",
      "extractDate": "@utcNow()",
      "failedRoutes": "@variables('FailedRoutes')"
    }
  }
}
```

<a id="snippet-upload-csv-blob"></a>
#### Upload_CSV_To_Blob Action

> **Source:** [sources/logicapps/sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Upload_CSV_To_Blob": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "AzureBlob",
      "operationId": "uploadBlob",
      "serviceProviderId": "/serviceProviders/AzureBlob"
    },
    "parameters": {
      "containerName": "@{outputs('Determine_Container')}",
      "blobName": "@{outputs('Determine_Path_Prefix')}@{formatDateTime(utcNow(), 'yyyyMM')}/@{formatDateTime(utcNow(), 'yyyyMMdd_HHmmss')}_@{items('Process_Each_Route_In_Parallel')?['fileName']}",
      "content": "@items('Process_Each_Route_In_Parallel')?['csvContent']"
    }
  }
}
```

---

### Use Case 3: Network Rail CIF File Processing

<a id="snippet-process-cif-updates"></a>
#### ProcessCifUpdates Timer Function

> **Source:** [sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs)

```csharp
[Function("ProcessCifUpdates")]
public async Task ProcessCifUpdates(
    [TimerTrigger("0 0 * * * *")] TimerInfo timer)
{
    var runId = Guid.NewGuid().ToString();
    _logger.LogInformation("ProcessCifUpdates timer triggered. RunId: {RunId}", runId);

    var sampleContent = GenerateSampleCifContent(50);
    var result = await _cifProcessingService.ProcessCifContentAsync(
        sampleContent, runId, forceRefresh: false);
    
    _logger.LogInformation("Processed: {Processed}, Published: {Published}",
        result.Statistics.SchedulesProcessed, result.Statistics.EventsPublished);
}
```

<a id="snippet-process-cif-full-refresh"></a>
#### ProcessCifFullRefresh Timer Function

> **Source:** [sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs)

```csharp
[Function("ProcessCifFullRefresh")]
public async Task ProcessCifFullRefresh(
    [TimerTrigger("0 0 2 * * 0")] TimerInfo timer)
{
    var runId = Guid.NewGuid().ToString();
    _logger.LogInformation("ProcessCifFullRefresh timer triggered. RunId: {RunId}", runId);

    var sampleContent = GenerateSampleCifContent(200);
    var result = await _cifProcessingService.ProcessCifContentAsync(
        sampleContent, runId, forceRefresh: true);
    
    _logger.LogInformation("Processed: {Processed}, Published: {Published}",
        result.Statistics.SchedulesProcessed, result.Statistics.EventsPublished);
}
```

<a id="snippet-gzip-decompression"></a>
#### GZIP Decompression in ProcessCifStreamAsync

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
public async Task<CifProcessResult> ProcessCifStreamAsync(Stream stream, string runId, bool forceRefresh = false)
{
    // Check if it's a GZIP stream by reading the magic bytes
    var isGzip = await IsGzipStreamAsync(stream);
    stream.Position = 0;

    Stream readStream = isGzip 
        ? new GZipStream(stream, CompressionMode.Decompress) 
        : stream;

    using var reader = new StreamReader(readStream, Encoding.UTF8, bufferSize: 8192);
    
    string? line;
    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
    {
        await ProcessLineAsync(line, runId, forceRefresh, stats, result.Errors);
    }
}
```

<a id="snippet-process-cif-content"></a>
#### ProcessCifContentAsync Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
public async Task<CifProcessResult> ProcessCifContentAsync(string content, string runId, bool forceRefresh = false)
{
    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var line in lines)
    {
        await ProcessLineAsync(line.Trim(), runId, forceRefresh, stats, result.Errors);
    }
    
    return result;
}
```

<a id="snippet-process-line"></a>
#### ProcessLineAsync Method (Filtering)

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
private async Task ProcessLineAsync(string line, string runId, bool forceRefresh, CifProcessingStatistics stats)
{
    var record = JsonSerializer.Deserialize<CifRecord>(line, _jsonOptions);
    
    if (record?.JsonScheduleV1 == null) return;
    var schedule = record.JsonScheduleV1;

    // Filter: Only planning schedules (N = New)
    if (schedule.CIF_stp_indicator != "N")
    {
        stats.SchedulesFiltered++;
        return;
    }

    // Filter: Must have location data
    if (schedule.schedule_location == null || schedule.schedule_location.Count == 0)
    {
        stats.SchedulesFiltered++;
        return;
    }

    // Filter: Must have at least one mapped location
    var hasValidLocations = schedule.schedule_location.Any(loc => 
        StationMappings.ContainsKey(loc.tiploc_code));
    
    if (!hasValidLocations)
    {
        stats.SchedulesFiltered++;
        return;
    }

    stats.SchedulesProcessed++;
}
```

<a id="snippet-station-mappings"></a>
#### StationMappings Reference Data

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
private static readonly Dictionary<string, StationMapping> StationMappings = new()
{
    { "EUSTON", new StationMapping { TiplocCode = "EUSTON", StationCode = "EUS", StationName = "London Euston" } },
    { "KNGX", new StationMapping { TiplocCode = "KNGX", StationCode = "KGX", StationName = "London King's Cross" } },
    { "STPX", new StationMapping { TiplocCode = "STPX", StationCode = "STP", StationName = "London St Pancras" } },
    { "STPANCI", new StationMapping { TiplocCode = "STPANCI", StationCode = "STP", StationName = "London St Pancras International", IsEurostarConnection = true } },
    { "PADTON", new StationMapping { TiplocCode = "PADTON", StationCode = "PAD", StationName = "London Paddington" } },
    { "BHAM", new StationMapping { TiplocCode = "BHAM", StationCode = "BHM", StationName = "Birmingham New Street" } },
    { "MNCRPIC", new StationMapping { TiplocCode = "MNCRPIC", StationCode = "MAN", StationName = "Manchester Piccadilly" } },
    { "EDINBUR", new StationMapping { TiplocCode = "EDINBUR", StationCode = "EDB", StationName = "Edinburgh Waverley" } },
    { "ASHFKY", new StationMapping { TiplocCode = "ASHFKY", StationCode = "AFK", StationName = "Ashford International", IsEurostarConnection = true } }
};
```

<a id="snippet-transform-to-event"></a>
#### TransformToEvent Method

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
public InfrastructurePathwayConfirmedEvent TransformToEvent(JsonScheduleV1 schedule, string runId)
{
    var passagePoints = schedule.schedule_location?
        .Where(loc => StationMappings.ContainsKey(loc.tiploc_code))
        .Select(loc =>
        {
            var mapping = StationMappings[loc.tiploc_code];
            return new PassagePointEvent
            {
                LocationCode = mapping.StationCode,
                LocationName = mapping.StationName,
                ArrivalTime = FormatTime(loc.arrival ?? loc.public_arrival),
                DepartureTime = FormatTime(loc.departure ?? loc.public_departure),
                Platform = loc.platform ?? ""
            };
        }).ToList();

    return new InfrastructurePathwayConfirmedEvent
    {
        TrainServiceNumber = schedule.CIF_train_uid,
        TravelDate = schedule.schedule_start_date,
        Origin = passagePoints.FirstOrDefault()?.LocationCode ?? "UNKNOWN",
        Destination = passagePoints.LastOrDefault()?.LocationCode ?? "UNKNOWN",
        PassagePoints = passagePoints,
        Metadata = new EventMetadata
        {
            Domain = "planning.short_term",
            Name = "InfrastructurePathwayConfirmed",
            CorrelationId = runId
        }
    };
}
```

<a id="snippet-deduplication"></a>
#### Deduplication Check

> **Source:** [sources/functions/Transgrid.Functions/Services/CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
private readonly HashSet<string> _processedKeys = new();

// In ProcessLineAsync:
var dedupKey = $"{schedule.CIF_train_uid}_{schedule.schedule_start_date}";
if (!forceRefresh && _processedKeys.Contains(dedupKey))
{
    stats.DuplicatesSkipped++;
    return;
}

// After successful processing:
_processedKeys.Add(dedupKey);
stats.EventsPublished++;
```

---

### Infrastructure

<a id="snippet-sftp-connection"></a>
#### SFTP Connection Configuration

> **Source:** [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json)

```json
"SftpPrimary": {
  "parameterValues": {
    "sshHostAddress": "@appsetting('SFTP_PRIMARY_HOST')",
    "userName": "@appsetting('SFTP_USERNAME')",
    "password": "@appsetting('SFTP_PASSWORD')",
    "portNumber": 22,
    "acceptAnySshHostKey": true
  },
  "serviceProvider": {
    "id": "/serviceProviders/Sftp"
  },
  "displayName": "SFTP Primary Connection"
}
```

<a id="snippet-blob-connection"></a>
#### Azure Blob Storage Connection

> **Source:** [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json)

```json
"AzureBlob": {
  "parameterValues": {
    "connectionString": "@appsetting('BLOB_CONNECTION_STRING')"
  },
  "serviceProvider": {
    "id": "/serviceProviders/AzureBlob"
  },
  "displayName": "Blob Storage Connection"
}
```

<a id="snippet-table-connection"></a>
#### Azure Table Storage Connection

> **Source:** [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json)

```json
"AzureTables": {
  "parameterValues": {
    "connectionString": "@appsetting('TABLE_CONNECTION_STRING')"
  },
  "serviceProvider": {
    "id": "/serviceProviders/AzureTables"
  },
  "displayName": "Table Storage Connection"
}
```

<a id="snippet-servicebus-connection"></a>
#### Service Bus Connection

> **Source:** [sources/logicapps/connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json)

```json
"serviceBus": {
  "parameterValues": {
    "connectionString": "@appsetting('SERVICEBUS_CONNECTION_STRING')"
  },
  "serviceProvider": {
    "id": "/serviceProviders/serviceBus"
  },
  "displayName": "Service Bus Connection"
}
```
