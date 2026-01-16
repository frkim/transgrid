# Transgrid

Demo of Azure Integration Services for a train company. This simulates an integration platform that allows to exchange data from many solutions, softwares and different protocols.

## Architecture

The solution consists of:

- **Azure Functions** - XML transformation service for TAF-JSG format
- **Logic Apps Standard** - Workflow orchestration for RNE data export
- **Azure Storage** - Blob storage and Table storage for data persistence
- **SFTP Container Apps** - Primary and backup SFTP servers for RNE file delivery

## Quick Start

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-tools)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell 7+

### Deploy to Azure

```powershell
# Login to Azure
az login

# Full deployment (infrastructure + code) - will prompt for password
.\infra\scripts\deploy-all.ps1

# Or provide password using SecureString
$password = ConvertTo-SecureString "YourSecurePassword123!" -AsPlainText -Force
.\infra\scripts\deploy-all.ps1 -SftpPassword $password

# Or deploy only code to existing infrastructure
.\infra\scripts\deploy-all.ps1 -ResourceGroupName "rg-transgrid-dev" -SkipInfrastructure
```

### Individual Deployments

```powershell
# Infrastructure only (with SecureString password)
$password = ConvertTo-SecureString "Password123!" -AsPlainText -Force
.\infra\deploy.ps1 -ResourceGroupName "rg-transgrid-dev" -SftpPassword $password

# Azure Functions only
.\infra\scripts\deploy-functions.ps1 -ResourceGroupName "rg-transgrid-dev"

# Logic Apps only
.\infra\scripts\deploy-logicapps.ps1 -ResourceGroupName "rg-transgrid-dev"
```

## Mock Server

The Transgrid Mock Server simulates three integration endpoints for Starline International train operations:

- **OpsAPI** - Train operational plans for Rail Network Europe (RNE) export
- **Salesforce** - Negotiated rates data with account information and tariff codes  
- **Network Rail API** - CIF schedule files with train service information

The server provides both REST APIs and a web-based administration interface with modern UI features including sortable/filterable tables, search functionality, and data management tools.

### Quick Start

```bash
cd sources/server/Transgrid.MockServer
dotnet run
```

Then open http://localhost:5240 in your browser.

See [sources/server/README.md](sources/server/README.md) for detailed documentation.

## Project Structure

```
transgrid/
├── infra/                      # Infrastructure as Code
│   ├── deploy.ps1              # Infrastructure deployment
│   ├── main.bicep              # Main Bicep template
│   ├── main.bicepparam         # Bicep parameters
│   ├── modules/                # Bicep modules
│   └── scripts/
│       ├── deploy-all.ps1      # Master deployment script
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
│   └── tests/                  # Test projects
└── documents/                  # Documentation
```
