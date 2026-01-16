# Logic Apps Standard Project

This directory contains the Azure Logic Apps Standard workflows for the Transgrid RNE Export solution.

## Structure

```
logicapps/
├── host.json                    # Logic Apps host configuration
├── local.settings.json          # Local development settings (not deployed)
├── connections.json             # API connections (shared across workflows)
├── parameters.json              # Shared parameters
├── .funcignore                  # Deployment ignore file
├── rne-daily-export/
│   └── workflow.json            # Daily export workflow (6:00 AM)
├── rne-d2-export/
│   └── workflow.json            # D+2 export workflow (6:30 AM)
├── rne-http-trigger/
│   └── workflow.json            # HTTP trigger for manual exports
└── rne-retry-failed/
    └── workflow.json            # Retry failed exports (7:00 AM)
```

## Prerequisites

1. **VS Code Extensions**:
   - Azure Logic Apps (Standard) extension
   - Azure Account extension
   - Azurite (for local storage emulator)

2. **Azure Storage Emulator**:
   Start Azurite before running locally:
   ```bash
   azurite --silent --location .azurite --debug .azurite/debug.log
   ```

## Local Development

1. Update `local.settings.json` with your settings:
   - `WORKFLOWS_TENANT_ID`: Your Azure AD tenant ID
   - `WORKFLOWS_SUBSCRIPTION_ID`: Your subscription ID
   - `OPS_API_ENDPOINT`: GraphQL API endpoint
   - `FUNCTION_ENDPOINT`: Azure Function endpoint

2. Start the Logic Apps runtime:
   - Open the folder in VS Code
   - Right-click on a workflow.json → Overview
   - Click "Use connectors from Azure" or "Use built-in connectors"

3. Run a workflow:
   - Open the workflow designer
   - Click "Run Trigger"

## Workflows

### rne-daily-export
- **Trigger**: Daily recurrence at 6:00 AM (Romance Standard Time)
- **Purpose**: Export train plans for current day
- **Actions**: Query GraphQL API → Transform to XML → Upload to SFTP + Blob

### rne-d2-export
- **Trigger**: Daily recurrence at 6:30 AM (Romance Standard Time)
- **Purpose**: Export train plans for D+2 (day after tomorrow)
- **Actions**: Same as daily export, for future date

### rne-http-trigger
- **Trigger**: HTTP POST request
- **Purpose**: Manual/ad-hoc exports
- **Request Body**:
  ```json
  {
    "travelDate": "2026-01-20",
    "exportType": "ADHOC",
    "trainIds": ["9001", "9002"]
  }
  ```

### rne-retry-failed
- **Trigger**: Daily recurrence at 7:00 AM (Romance Standard Time)
- **Purpose**: Retry failed exports from Azure Table Storage
- **Max Retries**: 3 attempts per failed export

## Connections

The `connections.json` file defines the following managed connections:
- **AzureBlob**: Azure Blob Storage for archiving
- **SftpPrimary**: Primary SFTP server for RNE
- **SftpBackup**: Backup SFTP server
- **AzureTableStorage**: Table Storage for failed exports tracking

## Deployment

Deploy to Azure using Azure CLI or VS Code:

```bash
# Deploy using Azure CLI
az logicapp deployment source config-zip \
  --name <logic-app-name> \
  --resource-group <resource-group> \
  --src <zip-file-path>
```

Or use the VS Code extension:
1. Right-click on the project folder
2. Select "Deploy to Logic App..."
3. Choose the target Logic App
