// Azure Functions Module
// Deploys an Azure Functions app with Flex Consumption plan for XML transformation
// Following Azure Functions Best Practices for .NET Isolated Worker

@description('Name of the Function App')
param functionAppName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Storage Account name')
param storageAccountName string

@description('Storage Account connection string')
@secure()
param storageConnectionString string

@description('Application Insights Instrumentation Key')
param appInsightsInstrumentationKey string = ''

@description('Application Insights Connection String')
param appInsightsConnectionString string = ''

@description('Operations API endpoint (GraphQL)')
param opsApiEndpoint string = ''

// Variables
var hostingPlanName = 'asp-func-${functionAppName}'
var functionAppFullName = 'func-${functionAppName}-${environment}'

// Tags for all resources
var tags = {
  Environment: environment
  Project: 'Transgrid RNE Export'
  Component: 'Azure Functions'
  ManagedBy: 'Bicep'
}

// Flex Consumption Plan (FC1) - Recommended for Azure Functions
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  kind: 'functionapp'
  properties: {
    reserved: true // Required for Linux
  }
}

// Function App with .NET 8 Isolated Worker
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppFullName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
        supportCredentials: false
      }
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'OPS_API_ENDPOINT'
          value: opsApiEndpoint
        }
      ]
    }
    // Function App Config for Flex Consumption
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storageAccountName}.blob.${az.environment().suffixes.storage}/function-releases'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'AzureWebJobsStorage'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
    }
  }
}

// Outputs
output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppDefaultHostname string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
output functionBaseUrl string = 'https://${functionApp.properties.defaultHostName}'
output functionEndpoint string = 'https://${functionApp.properties.defaultHostName}/api'
output hostingPlanId string = hostingPlan.id
