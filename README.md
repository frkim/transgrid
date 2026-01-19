# Transgrid

Demo of Azure Integration Services for a train company. This simulates an integration platform that allows to exchange data from many solutions, softwares and different protocols.

## Architecture

The solution consists of:

- **Azure Functions** - XML transformation service for TAF-JSG format
- **Logic Apps Standard** - Workflow orchestration for RNE data export
- **Azure Storage** - Blob storage and Table storage for data persistence
- **SFTP Container Apps** - Primary and backup SFTP servers for RNE file delivery
- **Mock Server Container Apps** - Mock API server for OpsAPI, Salesforce, and Network Rail endpoints

## Quick Start

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-tools)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Mock Server container build)
- PowerShell 7+

### Deploy to Azure

```powershell
# Login to Azure
az login

# Full deployment (infrastructure + Mock Server + Functions + Logic Apps)
$password = ConvertTo-SecureString "YourSecurePassword123!" -AsPlainText -Force
.\infra\scripts\deploy-all.ps1 -SftpPassword $password

# Skip Mock Server if using external OpsAPI
.\infra\scripts\deploy-all.ps1 -SftpPassword $password -SkipMockServer -OpsApiEndpoint "https://your-api.example.com"

# Or deploy only code to existing infrastructure
.\infra\scripts\deploy-all.ps1 -ResourceGroupName "rg-transgrid-dev" -SkipInfrastructure
```

### Individual Deployments

```powershell
# Infrastructure only (with SecureString password)
$password = ConvertTo-SecureString "Password123!" -AsPlainText -Force
.\infra\deploy.ps1 -ResourceGroupName "rg-transgrid-dev" -SftpPassword $password

# Mock Server only (requires Docker)
.\infra\scripts\deploy-mockserver.ps1 -ResourceGroupName "rg-transgrid-dev"

# Azure Functions only
.\infra\scripts\deploy-functions.ps1 -ResourceGroupName "rg-transgrid-dev"

# Logic Apps only (with Mock Server endpoint)
.\infra\scripts\deploy-logicapps.ps1 -ResourceGroupName "rg-transgrid-dev" -OpsApiEndpoint "https://ca-transgrid-mock-dev.<region>.azurecontainerapps.io"
```

## Mock Server

The Transgrid Mock Server simulates three integration endpoints for Starline International train operations:

- **OpsAPI** - Train operational plans for Rail Network Europe (RNE) export
- **Salesforce** - Negotiated rates data with account information and tariff codes  
- **Network Rail API** - CIF schedule files with train service information

The server provides both REST APIs and a web-based administration interface with modern UI features including sortable/filterable tables, search functionality, and data management tools.

### Local Development

```bash
cd sources/server/Transgrid.MockServer
dotnet run
```

Then open http://localhost:5240 in your browser.

### Azure Deployment

The Mock Server is deployed as an Azure Container App using the same Container Apps Environment as the SFTP servers. It exposes HTTP endpoints accessible via HTTPS.

```powershell
# Deploy Mock Server to Azure Container Apps
.\infra\scripts\deploy-mockserver.ps1 -ResourceGroupName "rg-transgrid-dev"
```

See [sources/server/README.md](sources/server/README.md) for detailed documentation.

## Project Structure

```
transgrid/
├── infra/                      # Infrastructure as Code
│   ├── deploy.ps1              # Infrastructure deployment
│   ├── main.bicep              # Main Bicep template
│   ├── main.bicepparam         # Bicep parameters
│   ├── modules/                # Bicep modules
│   │   ├── function-app.bicep  # Azure Functions
│   │   ├── logic-app.bicep     # Logic Apps Standard
│   │   ├── mock-server.bicep   # Mock Server Container App
│   │   └── sftp-server.bicep   # SFTP Container Apps
│   └── scripts/
│       ├── deploy-all.ps1      # Master deployment script
│       ├── deploy-mockserver.ps1 # Mock Server deployment
│       ├── deploy-functions.ps1 # Functions deployment
│       └── deploy-logicapps.ps1 # Logic Apps deployment
├── sources/
│   ├── functions/              # Azure Functions
│   │   └── Transgrid.Functions/
│   ├── logicapps/              # Logic Apps Standard
│   │   ├── host.json
│   │   ├── connections.json
│   │   ├── rne-daily-export/
│   │   ├── rne-d2-export/
│   │   ├── rne-http-trigger/
│   │   └── rne-retry-failed/
│   ├── server/                 # Mock Server
│   │   └── Transgrid.MockServer/
│   │       ├── Dockerfile      # Container image build
│   │       └── ...
│   └── tests/                  # Test projects
└── documents/                  # Documentation
```
