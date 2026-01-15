# Transgrid

Demo of Azure Integration Services for a train company. This simulates an integration platform that allows to exchange data from many solutions, softwares and different protocols.

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

Then open http://localhost:5000 in your browser.

See [sources/server/README.md](sources/server/README.md) for detailed documentation.
