<!-- markdownlint-disable MD024 -->
# Azure Integration Services Use Cases

## RNE Operational Plans Export

### WHAT IT DOES

Exports train operational plans to Rail Network Europe (RNE) via scheduled batch jobs with automatic retry for failures.

### TRIGGER

- Three cron schedulers:
  - Daily export
  - Future D+2 export
  - Retry failed exports

### SOURCE

- **System:** Operations GraphQL API (internal)
- **Data:** Train operational plans
  - serviceCode
  - pathway
  - travelDate
  - passagePoints
  - origin
  - destination
  - status
  - planType
- **Format:** JSON

### PROCESSING

1. Query GraphQL API to get train plans for travel date
2. Load reference data (location codes, weights, vehicle numbers)
3. Filter plans:
     - Country: FR or GB
     - Status: ACTIVE
     - Plan type: not EVOLUTION
4. For each train plan:
     - Validate plan has required fields
     - Transform JSON to XML using DataWeave
     - Validate XML against schema (TAF-JSG PassengerTrainCompositionProcessMessage v2.1.6)
5. On failure: store failed train IDs in Object Store for retry next run

### DESTINATIONS

1. **Azure Blob Storage (archive)**
     - Container: `ci-rne-export`
     - Path: `YYYY-MM/YYYY-MM-DD/filename.xml`
     - Format: XML

2. **RNE SFTP Server (primary)**
     - Format: XML

3. **RNE SFTP Server (backup)**
     - Format: XML

4. **Object Store (state for retry)**
     - Key: `failedTrains-YYYY-MM-DD`
     - Value: Array of failed train IDs

### KEY CAPABILITIES REQUIRED

- Cron scheduling with timezone support
- GraphQL API client
- JSON to XML transformation
- XML schema validation
- Azure Blob Storage connector
- SFTP connector
- Object Store / persistent cache for retry pattern

## SALESFORCE NEGOTIATED RATES EXPORT

### WHAT IT DOES

Listens to Salesforce Platform Events, extracts negotiated rates data via APEX web service, processes three different extract types in parallel, generates CSV files, and uploads to Azure Blob Storage.

### TRIGGER

- **Salesforce Platform Event listeners** (two types: replay and subscribe)
  - Channel: `/event/NegotiatedRateExtract__e`
  - Event contains: `Negotiated_Rates_Ids__c` (comma-separated Salesforce record IDs)

### SOURCE

- **System:** Salesforce
- **Step 1:** Platform Event trigger
- **Step 2:** APEX Web Service call (`getNegotiatedRateExtracts`)
- **Data:** Negotiated rates with account info, tariff codes, distributor codes
- **Format:** JSON
- **Source Objects:**
  - Negotiated_Rate__c
  - Contract
  - Account
  - Account_CTC__c
  - Account_PCC__c

### PROCESSING

1. Receive Platform Event with list of NR IDs
2. Call APEX web service to get full negotiated rate data
3. Process three extract types in parallel (scatter-gather):

     **Route 1 - IDL/S3 Extract:**
     - Filter by Code_Record_Type: GND BE, GND NL, FCE (New), Eurostar for Business code
     - Map tariff codes with percentage discounts
     - Generate CSV with columns: Account Manager, Account Name, Unique Code, Type, Road, Tariff Codes, Discounts, Action Type
     - Upload to Azure Blob (internal container)
     - Separate files for Priority and Normal

     **Route 2 - GDS Air Extract:**
     - Filter by Code_Record_Type: Corporate code Amadeus, Apollo, Galileo, Sabre
     - Additional lookup: query Salesforce for Account PCC information
     - Map tariff codes, PCC, GDS Used
     - Generate CSV with columns: Account Manager, Account Name, Unique Code, GDS Used, PCC, Road, Tariff Codes, Dates, Action Type
     - Upload to Azure Blob (internal container)
     - Separate files for Priority and Normal

     **Route 3 - BeNe Extract:**
     - Filter by Code_Record_Type: GND BE, GND NL
     - For external partners
     - Generate CSV with columns: Account Manager, Account Name, Unique Code, Distributor, Road, Tariff Codes, Action Type
     - Upload to Azure Blob (external container)

4. Update Salesforce records:
     - Set `Extract_Requested__c` to false
     - Set `B2b_Status__c` to Extracted
     - Update extraction dates

### DESTINATIONS

1. **Azure Blob Storage (internal container)**
     - Path: `S3/Priority Files/yyyyMM/yyyyMMdd HHmmss.csv`
     - Path: `S3/Normal Files/yyyyMM/yyyyMMdd HHmmss.csv`
     - Path: `GDS Air/Priority Files/yyyyMM/yyyyMMdd HHmmss.csv`
     - Path: `GDS Air/Normal Files/yyyyMM/yyyyMMdd HHmmss.csv`
     - Format: CSV

2. **Azure Blob Storage (external container)**
     - Path: `yyyyMM/yyyyMMdd HHmmss.csv`
     - Format: CSV

3. **Salesforce (record updates)**
     - `Extract_Requested__c`
     - `B2b_Status__c`
     - `B2b_Extract_Date__c`
     - `B2b_LastExtractionDate__c`

### KEY CAPABILITIES REQUIRED

- Salesforce Platform Event subscription (Streaming API)
- Salesforce APEX Web Service invocation
- Salesforce SOQL queries
- Salesforce record updates
- Parallel processing (scatter-gather pattern)
- Complex data transformation with filtering and mapping
- CSV file generation
- Azure Blob Storage connector (multiple containers)

## NETWORK RAIL CIF FILE PROCESSING

### WHAT IT DOES

Downloads large compressed schedule files from Network Rail, decompresses and parses them, then publishes events to internal message store.

### TRIGGER

- Two cron schedulers:
  - Daily poller (every 60 minutes)
  - Weekly full download

### SOURCE

- **System:** Network Rail NTROD API (external)
- **Endpoint:** `/ntrod/CifFileAuthenticate`
- **Authentication:** HTTP Basic Auth
- **Data:** CIF schedule files
- **Format:** GZIP compressed file containing newline-delimited JSON
- **File Size:** Several megabytes compressed (we end up at times with memory issues)

### PROCESSING

1. HTTP GET request to download GZIP file
2. Decompress GZIP to plain text/flat file
3. Split text by newline character
4. Filter: extract JsonScheduleV1 records where:
     - `CIF_stp_indicator` equals N (planning only)
     - Must have schedule location pass info
5. For each schedule:
     - Load reference data (station/points mapping)  
     - Transform to Protobuf event structure
     - Set event metadata:
         - Domain: `planning.short_term`
         - Name: `InfrastructurePathwayConfirmed`
     - Publish to message store with deduplication check

### DESTINATION

- **System:** Internal Message Store
- **Protocol:** gRPC
- **Format:** Protobuf
- **Event Type:** InfrastructurePathwayConfirmed
- **Tags:**
  - TrainServiceNumber
  - TravelDate

### KEY CAPABILITIES REQUIRED

- Cron scheduling
- HTTP file download (large files)
- GZIP decompression
- Text splitting and JSON parsing (line by line)
- Complex filtering logic
- Protobuf serialization
- gRPC client
- Reference data lookup
