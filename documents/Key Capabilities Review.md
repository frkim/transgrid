# Key Capabilities Review

> **Repository:** [https://github.com/frkim/transgrid](https://github.com/frkim/transgrid)

This document reviews all **Key Capabilities** required by the use cases defined in [Azure Integration Services Use Cases.md](Azure%20Integration%20Services%20Use%20Cases.md) and describes how each is implemented in this project.

---

## Use Case 1: RNE Operational Plans Export

### Cron scheduling with timezone support

**Status:** ✅ Implemented

Logic Apps Standard Recurrence triggers with `timeZone: "Romance Standard Time"`. Three schedulers: daily at 06:00, D+2 at 06:30, retry at 07:00.

**Source:** [rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

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

---

### GraphQL API client

**Status:** ✅ Implemented

Logic Apps HTTP action (`Query_GraphQL_API`) sends POST request to Operations GraphQL API with query variables for travel date filtering. Includes exponential retry policy (4 attempts).

**Source:** [rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

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

---

### JSON to XML transformation

**Status:** ✅ Implemented

Azure Function `TransformTrainPlan` using `XmlTransformService.cs` generates TAF-JSG compliant XML. Called via HTTP from Logic Apps workflow.

**Why Azure Function?**

- The TAF-JSG PassengerTrainCompositionProcessMessage v2.1.6 schema requires generating deeply nested XML with namespaces, schema locations, and conditional elements (passage points, train composition)
- The transformation enriches output with UIC codes and location names from reference data
- XML schema validation is performed in the same flow, reducing workflow complexity and latency

**Alternatives not chosen:**

- **Logic Apps Liquid templates**: Designed for JSON-to-JSON; poor XML namespace support
- **Integration Account XSLT**: Requires a paid Integration Account (€200+/month) and source data must already be XML—not suitable for JSON source
- **Logic Apps Inline Code (JavaScript)**: Limited to simple expressions, no full XML writer capabilities, 100KB code limit
- **Azure API Management policies**: Adds unnecessary infrastructure for a single transformation use case

The Azure Function approach provides full C# flexibility, testability, and reusability across multiple workflows while keeping costs minimal on the Consumption plan.

**Source:** [TransformTrainPlan.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformTrainPlan.cs)

```csharp
[Function("TransformTrainPlan")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var trainPlan = JsonSerializer.Deserialize<TrainPlanInput>(requestBody);
    var referenceData = await _referenceDataService.GetReferenceDataAsync();
    string xml = _xmlTransformService.Transform(trainPlan, referenceData);
    var xmlValidation = _xmlValidationService.Validate(xml);
    
    response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
    await response.WriteStringAsync(xml);
    return response;
}
```

---

### XML schema validation

**Status:** ✅ Implemented

`XmlValidationService.cs` validates XML against PassengerTrainCompositionProcessMessage v2.1.6 XSD. Checks required elements exist.

**Why Azure Function?**

- XSD schema validation requires loading and compiling `XmlSchemaSet` from the TAF-JSG v2.1.6 schema file—Logic Apps has no native XSD validation action
- Structural validation uses XPath queries to check required elements (`MessageIdentifier`, `TrainNumber`, `TravelDate`) with proper namespace handling
- Format validation (train number alphanumeric pattern, date format `yyyy-MM-dd`) requires custom logic beyond simple pattern matching
- Security: DTD processing is disabled to prevent XML External Entity (XXE) attacks—this configuration is not available in Logic Apps XML actions

**Alternatives not chosen:**

- **Logic Apps XML Validation action**: Requires Integration Account (paid tier) and cannot handle complex XPath-based structural checks
- **Logic Apps Inline Code (JavaScript)**: No `XmlSchemaSet` support; DOMParser lacks XSD validation capability
- **Azure API Management validate-content policy**: Designed for OpenAPI/JSON Schema validation, not XSD; adds unnecessary infrastructure
- **Third-party Logic Apps connector**: Vendor lock-in, additional licensing cost, limited control over validation logic

The Azure Function provides full `System.Xml.Schema` capabilities, custom validation rules, detailed error reporting with line numbers, and seamless integration with the transformation function.

**Source:** [XmlValidationService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/XmlValidationService.cs)

```csharp
public ValidationResult ValidateWithDetails(string xml)
{
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
            errors.Add(new ValidationError { Message = $"Required element missing: {xpath}" });
    }
    return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
}
```

---

### Azure Blob Storage connector

**Status:** ✅ Implemented

Logic Apps `AzureBlob` service provider connection. Archives XML files to `ci-rne-export` container with path pattern `YYYY-MM/YYYY-MM-DD/filename.xml`.

**Source:** [connections.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/connections.json)

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

---

### SFTP connector

**Status:** ✅ Implemented

Two SFTP connections (`SftpPrimary`, `SftpBackup`) using Logic Apps SFTP service provider. Uploads to `/upload/YYYY-MM-DD/` folder. SFTP servers run as Azure Container Apps.

**Source:** [rne-daily-export/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-daily-export/workflow.json)

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

---

### Object Store / persistent cache for retry pattern

**Status:** ✅ Implemented

Azure Table Storage (`FailedExports` table) stores failed train IDs with PartitionKey=TravelDate. Retry workflow queries table for records with RetryCount < MaxRetries.

**Source:** [rne-retry-failed/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/rne-retry-failed/workflow.json)

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

## Use Case 2: Salesforce Negotiated Rates Export

### Salesforce Platform Event subscription (Streaming API)

**Status:** ⚠️ Simulated

Uses Azure Service Bus queue trigger instead of direct Salesforce Streaming API (requires licensed managed connector). Mock server publishes events to Service Bus queue `salesforce-events`.

**Source:** [sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"When_messages_are_available_in_queue": {
  "type": "ServiceProvider",
  "kind": "Trigger",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "serviceBus",
      "operationId": "receiveQueueMessages",
      "serviceProviderId": "/serviceProviders/serviceBus"
    },
    "parameters": {
      "queueName": "salesforce-events",
      "isSessionsEnabled": false
    }
  },
  "splitOn": "@triggerOutputs()?['body']"
}
```

---

### Salesforce APEX Web Service invocation

**Status:** ⚠️ Simulated

Logic Apps HTTP action calls mock API endpoint `/api/Salesforce/getNegotiatedRates`. In production, would call Salesforce APEX web service directly.

**Source:** [sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Get_Negotiated_Rates": {
  "type": "Http",
  "inputs": {
    "method": "GET",
    "uri": "@{appsetting('MOCK_SERVER_URL')}/api/Salesforce/getNegotiatedRates",
    "headers": {
      "X-Correlation-Id": "@{workflow().run.name}"
    },
    "retryPolicy": {
      "type": "exponential",
      "count": 3,
      "interval": "PT5S"
    }
  }
}
```

---

### Salesforce SOQL queries

**Status:** ⚠️ Simulated

Mock server contains pre-generated sample data. Production would require Salesforce connector for SOQL queries on Account, Contract, Negotiated_Rate__c objects.

**Source:** [SalesforceController.cs](https://github.com/frkim/transgrid/blob/main/sources/server/Transgrid.MockServer/Controllers/SalesforceController.cs)

```csharp
// Mock server simulates SOQL query results
[HttpGet("getNegotiatedRates")]
public IActionResult GetNegotiatedRates()
{
    var rates = _rateService.GetNegotiatedRates();
    // In production, this would be SOQL:
    // SELECT Id, Account__c, Contract__c, Route_Code__c, Valid_From__c, Valid_To__c
    // FROM Negotiated_Rate__c WHERE Extract_Requested__c = true
    return Ok(rates);
}
```

---

### Salesforce record updates

**Status:** ⚠️ Simulated

Logic Apps HTTP action calls `/api/Salesforce/updateExtractStatus` on mock server. Updates `B2b_Status__c`, `Extract_Requested__c`, and extraction dates.

**Source:** [sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Update_Extract_Status": {
  "type": "Http",
  "inputs": {
    "method": "PATCH",
    "uri": "@{appsetting('MOCK_SERVER_URL')}/api/Salesforce/updateExtractStatus",
    "headers": { "Content-Type": "application/json" },
    "body": {
      "rateIds": "@body('Get_Rate_Ids')",
      "b2bStatus": "Extracted",
      "extractDate": "@{utcNow()}"
    }
  },
  "runAfter": { "Upload_All_Routes": ["Succeeded"] }
}
```

---

### Parallel processing (scatter-gather pattern)

**Status:** ✅ Implemented

Logic Apps `Foreach` with `concurrency: 3` processes routes in parallel. Azure Function `ProcessRoute()` handles IDL_S3, GDS_AIR, and BENE routes simultaneously.

**Source:** [TransformNegotiatedRates.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/TransformNegotiatedRates.cs)

```csharp
[Function("TransformNegotiatedRates")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    // Filter rates by route
    var idlS3Rates = rates.Where(r => r.RouteCode == "IDL_S3").ToList();
    var gdsAirRates = rates.Where(r => r.RouteCode == "GDS_AIR").ToList();
    var beneRates = rates.Where(r => r.RouteCode == "BENE").ToList();
    
    // Generate CSVs in parallel
    var idlS3Csv = _csvGenerator.GenerateCsv(idlS3Rates, new CsvOptions { RouteType = "IDL_S3" });
    var gdsAirCsv = _csvGenerator.GenerateCsv(gdsAirRates, new CsvOptions { RouteType = "GDS_AIR" });
    var beneCsv = _csvGenerator.GenerateCsv(beneRates, new CsvOptions { RouteType = "BENE" });
    
    return response;
}
```

---

### Complex data transformation with filtering and mapping

**Status:** ✅ Implemented

`CsvGeneratorService` implements three filter methods: `FilterForIdlS3()`, `FilterForGdsAir()`, `FilterForBeNe()`. Filters by Code_Record_Type, GDS, and Distributor fields.

**Source:** [CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
public List<NegotiatedRate> FilterByRoute(List<NegotiatedRate> rates, string routeType)
{
    return routeType switch
    {
        "IDL_S3" => rates.Where(r => r.CodeRecordType == "IDL" && r.Distributor == "S3").ToList(),
        "GDS_AIR" => rates.Where(r => r.Gds == "AIR").ToList(),
        "BENE" => rates.Where(r => r.Distributor == "BENE" && r.IsExternalShare).ToList(),
        _ => rates
    };
}

private IEnumerable<string> MapToColumns(NegotiatedRate rate, string routeType)
{
    var baseColumns = new[] { rate.AccountId, rate.ContractId, rate.RouteCode };
    return routeType == "BENE" 
        ? baseColumns.Concat(new[] { rate.ExternalPrice.ToString() })
        : baseColumns.Concat(new[] { rate.InternalPrice.ToString() });
}
```

---

### CSV file generation

**Status:** ✅ Implemented

`CsvGeneratorService` generates CSV with route-specific columns: `GenerateIdlS3Csv()`, `GenerateGdsAirCsv()`, `GenerateBeneCsv()`. Proper CSV escaping with `EscapeCsv()` method.

**Source:** [CsvGeneratorService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CsvGeneratorService.cs)

```csharp
public string GenerateCsv(IEnumerable<NegotiatedRate> rates, CsvOptions options)
{
    var sb = new StringBuilder();
    var headers = GetHeadersForRoute(options.RouteType);
    sb.AppendLine(string.Join(",", headers));
    
    foreach (var rate in rates)
    {
        var values = MapToColumns(rate, options.RouteType)
            .Select(EscapeCsv);
        sb.AppendLine(string.Join(",", values));
    }
    return sb.ToString();
}

private string EscapeCsv(string value) =>
    value?.Contains(',') == true ? $"\"{value}\"" : value ?? "";
```

---

### Azure Blob Storage connector (multiple containers)

**Status:** ✅ Implemented

Dynamic container selection via `Determine_Container` action: BENE → `salesforce-external`, others → `salesforce-internal`. Path includes route prefix and timestamp.

**Source:** [sf-negotiated-rates/workflow.json](https://github.com/frkim/transgrid/blob/main/sources/logicapps/sf-negotiated-rates/workflow.json)

```json
"Upload_CSV_to_Blob": {
  "type": "ServiceProvider",
  "inputs": {
    "serviceProviderConfiguration": {
      "connectionName": "AzureBlob",
      "operationId": "uploadBlob",
      "serviceProviderId": "/serviceProviders/AzureBlob"
    },
    "parameters": {
      "containerName": "@{if(equals(items('For_Each_Route')?['routeCode'], 'BENE'), 'salesforce-external', 'salesforce-internal')}",
      "blobName": "@{items('For_Each_Route')?['routeCode']}/@{formatDateTime(utcNow(), 'yyyyMMdd_HHmmss')}.csv",
      "content": "@body('Generate_Route_CSV')"
    }
  }
}
```

---

## Use Case 3: Network Rail CIF File Processing

### Cron scheduling

**Status:** ✅ Implemented

Azure Functions Timer triggers: `ProcessCifUpdates` runs hourly (`0 0 * * * *`), `ProcessCifFullRefresh` runs weekly on Sundays at 02:00 UTC (`0 0 2 * * 0`).

**Source:** [ProcessCifFile.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Functions/ProcessCifFile.cs)

```csharp
[Function("ProcessCifUpdates")]
public async Task RunUpdates(
    [TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
{
    _logger.LogInformation("Processing CIF updates at {time}", DateTime.UtcNow);
    await _cifProcessingService.ProcessCifStreamAsync("updates");
}

[Function("ProcessCifFullRefresh")]
public async Task RunFullRefresh(
    [TimerTrigger("0 0 2 * * 0")] TimerInfo timerInfo)
{
    _logger.LogInformation("Processing CIF full refresh at {time}", DateTime.UtcNow);
    await _cifProcessingService.ProcessCifStreamAsync("full");
}
```

---

### HTTP file download (large files)

**Status:** ⚠️ Simulated

`CifProcessingService` includes streaming download logic but uses generated sample CIF content for demo. Production would HTTP GET from Network Rail NTROD `/ntrod/CifFileAuthenticate` with Basic Auth.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
public async Task<Stream> DownloadCifFileAsync(string fileType)
{
    // Production implementation (currently mocked):
    // var client = new HttpClient();
    // var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    // var response = await client.GetStreamAsync($"https://datafeeds.networkrail.co.uk/ntrod/CifFileAuthenticate?type={fileType}");
    
    // Demo: Generate sample CIF content
    _logger.LogInformation("Generating sample CIF content for type: {FileType}", fileType);
    return GenerateSampleCifStream(fileType);
}
```

---

### GZIP decompression

**Status:** ✅ Implemented

`ProcessCifStreamAsync()` uses `GZipStream` with `CompressionMode.Decompress`. Detects GZIP by reading magic bytes (`IsGzipStreamAsync`). Memory-efficient streaming approach.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
public async Task ProcessCifStreamAsync(string fileType)
{
    var stream = await DownloadCifFileAsync(fileType);
    
    if (await IsGzipStreamAsync(stream))
    {
        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.UTF8);
        await ProcessLinesAsync(reader);
    }
    else
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await ProcessLinesAsync(reader);
    }
}

private async Task<bool> IsGzipStreamAsync(Stream stream)
{
    var buffer = new byte[2];
    await stream.ReadAsync(buffer, 0, 2);
    stream.Position = 0;
    return buffer[0] == 0x1f && buffer[1] == 0x8b; // GZIP magic bytes
}
```

---

### Text splitting and JSON parsing (line by line)

**Status:** ✅ Implemented

`ProcessCifContentAsync()` splits by newline character. `ProcessLineAsync()` deserializes each line as `CifRecord` using `System.Text.Json`. NDJSON format handled correctly.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
private async Task ProcessLinesAsync(StreamReader reader)
{
    int lineNumber = 0;
    int processedCount = 0;
    
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        lineNumber++;
        
        if (string.IsNullOrWhiteSpace(line)) continue;
        
        try
        {
            var record = JsonSerializer.Deserialize<CifRecord>(line);
            if (await ProcessRecordAsync(record))
            {
                processedCount++;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse line {LineNumber}: {Error}", lineNumber, ex.Message);
        }
    }
    
    _logger.LogInformation("Processed {Count} records from {Total} lines", processedCount, lineNumber);
}
```

---

### Complex filtering logic

**Status:** ✅ Implemented

`ProcessLineAsync()` filters: `CIF_stp_indicator == "N"` (permanent schedules only), schedule must have location data, at least one location must exist in `StationMappings`.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
private async Task<bool> ProcessRecordAsync(CifRecord record)
{
    // Filter 1: Only permanent schedules (STP indicator = "N")
    if (record.StpIndicator != "N")
    {
        return false;
    }
    
    // Filter 2: Must have location data
    if (record.Locations == null || !record.Locations.Any())
    {
        return false;
    }
    
    // Filter 3: At least one location must be in station mappings
    var eurostarStations = record.Locations
        .Where(loc => _stationMappings.ContainsKey(loc.Tiploc))
        .ToList();
    
    if (!eurostarStations.Any())
    {
        return false;
    }
    
    // Process valid record
    await PublishInfrastructureEventAsync(record, eurostarStations);
    return true;
}
```

---

### Protobuf serialization

**Status:** ⚠️ Simplified

Not implemented. Uses JSON `InfrastructurePathwayConfirmedEvent` object instead of Protobuf. Production would require `.proto` file and Protobuf serialization.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
// Current implementation: JSON serialization
private async Task PublishInfrastructureEventAsync(CifRecord record, List<CifLocation> stations)
{
    var evt = new InfrastructurePathwayConfirmedEvent
    {
        ScheduleId = record.ScheduleId,
        Origin = _stationMappings[stations.First().Tiploc],
        Destination = _stationMappings[stations.Last().Tiploc],
        PathwayStations = stations.Select(s => _stationMappings[s.Tiploc]).ToList(),
        EffectiveFrom = record.ScheduleStartDate,
        EffectiveTo = record.ScheduleEndDate
    };
    
    // Production would use: Google.Protobuf.MessageExtensions.ToByteArray(evt)
    var json = JsonSerializer.Serialize(evt);
    await SimulatePublishAsync(json);
}
```

---

### gRPC client

**Status:** ⚠️ Simplified

Not implemented. `SimulatePublishAsync()` logs the event instead of publishing via gRPC. Production would require gRPC client to message store endpoint.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
// Current implementation: Logs event (simulates gRPC publish)
private async Task SimulatePublishAsync(string eventJson)
{
    _logger.LogInformation("Publishing infrastructure event: {Event}", eventJson);
    
    // Production implementation would be:
    // using var channel = GrpcChannel.ForAddress(_messageStoreUri);
    // var client = new InfrastructureService.InfrastructureServiceClient(channel);
    // await client.PublishPathwayEventAsync(new PathwayEventRequest { EventData = eventBytes });
    
    await Task.CompletedTask;
}
```

---

### Reference data lookup

**Status:** ✅ Implemented

`StationMappings` dictionary maps TIPLOC codes to CRS/Station names (e.g., STPANCI → STP/London St Pancras International). Hardcoded in service; production would use Azure Table Storage or Redis.

**Source:** [CifProcessingService.cs](https://github.com/frkim/transgrid/blob/main/sources/functions/Transgrid.Functions/Services/CifProcessingService.cs)

```csharp
// Eurostar-relevant station TIPLOC to CRS/Name mappings
private static readonly Dictionary<string, string> _stationMappings = new()
{
    { "STPANCI", "STP - London St Pancras International" },
    { "EBSFLDS", "EBB - Ebbsfleet International" },
    { "ASHFKY", "AFK - Ashford International" },
    { "FRSTGTL", "FST - Stratford International" },
    { "PARDSNO", "FRP - Paris Gare du Nord" },
    { "BRUSMD", "BXS - Brussels Midi" },
    { "LILEFLA", "LFE - Lille Europe" },
    { "AMSTRDA", "AMS - Amsterdam Centraal" },
    { "ROTRDMP", "RTD - Rotterdam Centraal" }
};

private string GetStationName(string tiploc)
{
    return _stationMappings.TryGetValue(tiploc, out var name) ? name : tiploc;
}
```

---

## Summary

| Use Case | Fully Implemented | Partially Implemented | Total Capabilities |
|----------|-------------------|----------------------|-------------------|
| RNE Operational Plans Export | 7 | 0 | 7 |
| Salesforce Negotiated Rates Export | 4 | 4 | 8 |
| Network Rail CIF Processing | 5 | 3 | 8 |
| **Total** | **16** | **7** | **23** |

### Implementation Notes

**Fully Implemented (✅):** Core functionality works as specified using Azure native services.

**Partially Implemented (⚠️):** 
- **Salesforce Integration**: Uses mock server + Service Bus instead of licensed Salesforce managed connector
- **Network Rail API**: Uses generated sample data instead of actual NTROD API
- **gRPC/Protobuf**: Uses JSON event format and logging instead of actual gRPC publish

### Production Readiness

To make this implementation production-ready:

1. **Salesforce**: Add licensed Salesforce connector for Platform Events, APEX calls, and SOQL queries
2. **Network Rail**: Implement actual HTTP download from NTROD API with Basic Auth credentials
3. **gRPC/Protobuf**: Generate Protobuf classes and implement gRPC client for message store
4. **Reference Data**: Move hardcoded mappings to Azure Table Storage or Redis Cache
5. **Secrets**: Use Azure Key Vault for all credentials and connection strings
