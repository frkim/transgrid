# Transgrid Mock Server

A comprehensive mock server for Starline International train operations, simulating integration services for OpsAPI, Salesforce, and Network Rail API.

## Overview

This mock server provides three API endpoints with web-based administration interfaces:

1. **OpsAPI** - Train operational plans for Rail Network Europe (RNE) export
2. **Salesforce** - Negotiated rates data with account information and tariff codes
3. **Network Rail** - CIF schedule files with train service information

## Technology Stack

- **.NET 10** - Latest .NET framework
- **ASP.NET Core** - Web framework
- **Razor Pages** - Server-side UI rendering
- **Web API** - RESTful API endpoints
- **Bootstrap 5** - Responsive design (loaded via CDN)
- **Bootstrap Icons** - Icon library (loaded via CDN)

## Features

### Data Management
- **In-Memory Storage** - All data stored in memory (no database)
- **Auto-Generated Data** - Realistic baseline dataset generated on startup
  - 25 train operational plans
  - 30 negotiated rates
  - 20 CIF schedules
- **Generate New Data** - Button to create fresh random data
- **Reset to Baseline** - Button to restore original dataset

### Web Interface
- **Modern UI** - Gradient backgrounds, animations, and visual effects
- **Responsive Design** - Works on desktop, tablet, and mobile
- **Sortable Tables** - Click column headers to sort ascending/descending
- **Real-time Filtering** - Search bar for instant data filtering
- **Detail Popups** - View complete record details in modal windows
- **Record Counts** - Dynamic badges showing number of records

### REST API Endpoints

#### OpsAPI - Train Plans
- `GET /api/OpsApi` - List all train plans
- `GET /api/OpsApi/{id}` - Get specific train plan
- `POST /api/OpsApi` - Create train plan
- `PUT /api/OpsApi/{id}` - Update train plan
- `DELETE /api/OpsApi/{id}` - Delete train plan

#### Salesforce - Negotiated Rates
- `GET /api/Salesforce` - List all negotiated rates
- `GET /api/Salesforce/{id}` - Get specific rate
- `POST /api/Salesforce` - Create rate
- `PUT /api/Salesforce/{id}` - Update rate
- `DELETE /api/Salesforce/{id}` - Delete rate

#### Network Rail - CIF Schedules
- `GET /api/NetworkRail` - List all schedules
- `GET /api/NetworkRail/{id}` - Get specific schedule
- `POST /api/NetworkRail` - Create schedule
- `PUT /api/NetworkRail/{id}` - Update schedule
- `DELETE /api/NetworkRail/{id}` - Delete schedule

#### Data Management
- `POST /api/DataManagement/generate` - Generate new random data
- `POST /api/DataManagement/reset` - Reset to baseline dataset

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed on your machine

### Running the Server

1. Navigate to the project directory:
```bash
cd sources/server/Transgrid.MockServer
```

2. Run the application:
```bash
dotnet run
```

3. Open your browser and navigate to:
```
http://localhost:5000
```

Or for HTTPS:
```
https://localhost:5001
```

### Building the Project

To build the project without running:
```bash
cd sources/server/Transgrid.MockServer
dotnet build
```

### Publishing for Deployment

To create a production-ready build:
```bash
cd sources/server/Transgrid.MockServer
dotnet publish -c Release -o ./publish
```

## Project Structure

```
sources/server/Transgrid.MockServer/
├── Controllers/              # REST API controllers
│   ├── OpsApiController.cs
│   ├── SalesforceController.cs
│   ├── NetworkRailController.cs
│   └── DataManagementController.cs
├── Models/                   # Data models
│   ├── TrainPlan.cs
│   ├── NegotiatedRate.cs
│   └── CifSchedule.cs
├── Services/                 # Business logic
│   └── DataStore.cs         # In-memory data storage
├── Pages/                    # Razor Pages UI
│   ├── Index.cshtml         # Home page
│   ├── OpsApi/
│   ├── Salesforce/
│   ├── NetworkRail/
│   └── Shared/
│       └── _Layout.cshtml   # Main layout
├── wwwroot/                  # Static files
│   ├── css/
│   │   └── site.css         # Custom styles
│   └── js/
│       └── site.js          # Custom JavaScript
└── Program.cs                # Application entry point
```

## Data Models

### Train Plan (OpsAPI)
Based on RNE Operational Plans Export requirements:
- Service Code
- Pathway (e.g., UK-FR, UK-BE)
- Travel Date
- Origin and Destination stations
- Passage Points (intermediate stations)
- Status (ACTIVE, CANCELLED, DELAYED)
- Plan Type (STANDARD, EVOLUTION, ALTERNATIVE)
- Country (GB, FR, BE, NL)

### Negotiated Rate (Salesforce)
Based on Salesforce Negotiated Rates Extract:
- Account Name and Manager
- Unique Code
- Code Record Type (GND BE, GND NL, FCE, Corporate code Amadeus, Apollo, Galileo, Sabre)
- GDS Used (for GDS types)
- PCC (for GDS types)
- Distributor (for BeNe types)
- Road (route)
- Tariff Codes with Discounts
- Priority (Normal, Priority)
- B2B Status (Pending, Extracted, Failed)

### CIF Schedule (Network Rail)
Based on Network Rail CIF File Processing:
- Train Service Number
- Travel Date
- Operator (Network Rail, Starline International, etc.)
- Train Class (Standard, First, Business)
- Power Type (EMU, DMU, HST)
- Train Category
- CIF STP Indicator
- Schedule Locations with:
  - Location Code and Name
  - Arrival and Departure Times
  - Platform
  - Activity Code

## Usage Examples

### Viewing Data
1. Navigate to any of the three data pages from the home page
2. Use the search bar to filter records in real-time
3. Click column headers to sort data
4. Click "View" button to see complete record details in a popup

### Managing Data
1. Click "Generate New Data" in the navigation bar to create a fresh dataset
2. Click "Reset Data" to restore the original baseline dataset
3. All changes are in-memory only and lost when the server restarts

### Using the API
Example API calls using curl:

```bash
# Get all train plans
curl http://localhost:5000/api/OpsApi

# Get a specific train plan
curl http://localhost:5000/api/OpsApi/{id}

# Generate new data
curl -X POST http://localhost:5000/api/DataManagement/generate

# Reset to baseline
curl -X POST http://localhost:5000/api/DataManagement/reset
```

## Development

### Adding New Fields
1. Update the model in `Models/` directory
2. Update the data generation in `Services/DataStore.cs`
3. Update the UI table in `Pages/{ApiName}/Index.cshtml`
4. Update the detail view in the modal

### Customizing Data Generation
Edit the `GenerateBaselineData()` method in `Services/DataStore.cs` to customize:
- Number of records generated
- Data values and ranges
- Random data patterns

## Notes

- **No External Dependencies**: Uses only CDN resources for Bootstrap and Icons
- **Thread-Safe**: In-memory storage uses locking for thread safety
- **CORS Enabled**: APIs can be called from any origin
- **Development Mode**: Configured for development with detailed error pages

## License

This is a demonstration/mock server for the Transgrid project simulating Starline International operations.
