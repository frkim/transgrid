// Logic Apps Standard Module
// Deploys a Logic Apps Standard instance with Workflow Service Plan

@description('Name of the Logic App')
param logicAppName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Storage Account name for Logic App state')
param storageAccountName string

@description('Storage Account connection string')
@secure()
param storageConnectionString string

@description('Application Insights Instrumentation Key')
param appInsightsInstrumentationKey string = ''

@description('Application Insights Connection String')
param appInsightsConnectionString string = ''

@description('Blob Storage connection string for RNE export')
@secure()
param blobConnectionString string = ''

@description('Table Storage connection string for retry state')
@secure()
param tableConnectionString string = ''

@description('Primary SFTP endpoint')
param primarySftpEndpoint string = ''

@description('Backup SFTP endpoint')
param backupSftpEndpoint string = ''

@description('Primary SFTP host (without port)')
param primarySftpHost string = ''

@description('Backup SFTP host (without port)')
param backupSftpHost string = ''

@description('SFTP username')
param sftpUsername string = ''

@description('SFTP password')
@secure()
param sftpPassword string = ''

@description('Azure Function endpoint for transformation')
param functionEndpoint string = ''

@description('Azure Function key for authentication')
@secure()
param functionKey string = ''

@description('Operations API endpoint (GraphQL)')
param opsApiEndpoint string = ''

// Variables
var hostingPlanName = 'asp-${logicAppName}'
var logicAppFullName = 'logic-${logicAppName}-${environment}'

// App Service Plan (Workflow Standard WS1)
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'WS1'
    tier: 'WorkflowStandard'
  }
  kind: 'elastic'
  properties: {
    elasticScaleEnabled: true
    maximumElasticWorkerCount: 20
    isSpot: false
    reserved: false
  }
}

// Logic App Standard
resource logicApp 'Microsoft.Web/sites@2023-12-01' = {
  name: logicAppFullName
  location: location
  kind: 'functionapp,workflowapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'node'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~22'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(logicAppFullName)
        }
        {
          name: 'AzureFunctionsJobHost__extensionBundle__id'
          value: 'Microsoft.Azure.Functions.ExtensionBundle.Workflows'
        }
        {
          name: 'AzureFunctionsJobHost__extensionBundle__version'
          value: '[1.*, 2.0.0)'
        }
        {
          name: 'APP_KIND'
          value: 'workflowApp'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        // Custom app settings for workflow connections
        {
          name: 'BLOB_CONNECTION_STRING'
          value: blobConnectionString
        }
        {
          name: 'TABLE_CONNECTION_STRING'
          value: tableConnectionString
        }
        {
          name: 'PRIMARY_SFTP_ENDPOINT'
          value: primarySftpEndpoint
        }
        {
          name: 'BACKUP_SFTP_ENDPOINT'
          value: backupSftpEndpoint
        }
        {
          name: 'SFTP_PRIMARY_HOST'
          value: primarySftpHost
        }
        {
          name: 'SFTP_BACKUP_HOST'
          value: backupSftpHost
        }
        {
          name: 'SFTP_USERNAME'
          value: sftpUsername
        }
        {
          name: 'SFTP_PASSWORD'
          value: sftpPassword
        }
        {
          name: 'FUNCTION_ENDPOINT'
          value: functionEndpoint
        }
        {
          name: 'FUNCTION_KEY'
          value: functionKey
        }
        {
          name: 'OPS_API_ENDPOINT'
          value: opsApiEndpoint
        }
      ]
    }
  }
}

// Outputs
output logicAppId string = logicApp.id
output logicAppName string = logicApp.name
output logicAppDefaultHostname string = logicApp.properties.defaultHostName
output logicAppPrincipalId string = logicApp.identity.principalId
output hostingPlanId string = hostingPlan.id
