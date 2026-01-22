// Main Bicep Template for Azure Integration Services Demo
// Deploys all Azure resources for the integration scenarios:
// - Use Case 1: RNE Operational Plans Export
// - Use Case 2: Salesforce Negotiated Rates Export (with Service Bus)
// - Use Case 3: Network Rail CIF File Processing

targetScope = 'resourceGroup'

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string = 'transgrid'

@description('SFTP username')
param sftpUsername string = 'rneuser'

@description('SFTP password')
@secure()
param sftpPassword string = 'MicrosoftAzure123!'

@description('Allowed IP addresses for SFTP access (CIDR notation)')
param allowedSftpIpRanges array = []

@description('Operations API endpoint (GraphQL mock server)')
param opsApiEndpoint string = 'http://localhost:5000'

@description('Enable Salesforce integration with Service Bus')
param enableSalesforceIntegration bool = true

@description('Enable Network Rail CIF processing with Redis cache')
param enableNetworkRailIntegration bool = true

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = 'st${baseName}${uniqueSuffix}'
var logAnalyticsName = 'log-${baseName}-${environment}'
var appInsightsName = 'appi-${baseName}-${environment}'
var containerAppsEnvName = 'cae-${baseName}-${environment}'
var vnetName = 'vnet-${baseName}-${environment}'

// Tags for all resources
var tags = {
  Environment: environment
  Project: 'Transgrid Azure Integration Services'
  ManagedBy: 'Bicep'
}

// Virtual Network for Container Apps
resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'snet-container-apps'
        properties: {
          addressPrefix: '10.0.0.0/23'
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'snet-integration'
        properties: {
          addressPrefix: '10.0.2.0/24'
          delegations: [
            {
              name: 'Microsoft.Web.serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

// Storage Account for all services
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    accessTier: 'Hot'
  }
}

// Blob Container for RNE exports
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource rneExportContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'ci-rne-export'
  properties: {
    publicAccess: 'None'
  }
}

// Blob Container for Salesforce internal exports (S3/IDL and GDS Air)
resource salesforceInternalContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'salesforce-internal'
  properties: {
    publicAccess: 'None'
  }
}

// Blob Container for Salesforce external exports (BeNe)
resource salesforceExternalContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'salesforce-external'
  properties: {
    publicAccess: 'None'
  }
}

// Blob Container for CIF archive files (Use Case 3)
resource cifArchiveContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'cif-archive'
  properties: {
    publicAccess: 'None'
  }
}

resource functionReleasesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'function-releases'
  properties: {
    publicAccess: 'None'
  }
}

// Table Storage for failed exports
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource failedExportsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'FailedExports'
}

// Table for Salesforce extract tracking
resource salesforceExtractsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'SalesforceExtracts'
}

// Table for CIF deduplication (Use Case 3)
resource cifDeduplicationTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'CifDeduplication'
}

// Table for CIF processing logs (Use Case 3)
resource cifProcessingLogsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'CifProcessingLogs'
}

// File Shares for SFTP
resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource sftpDataSharePrimary 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'sftpdata'
  properties: {
    shareQuota: 10
  }
}

resource sshKeysSharePrimary 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'sshkeys'
  properties: {
    shareQuota: 1
  }
}

resource scriptsSharePrimary 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'scripts'
  properties: {
    shareQuota: 1
  }
}

// Backup SFTP shares
resource sftpDataShareBackup 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'sftpdata-backup'
  properties: {
    shareQuota: 10
  }
}

resource sshKeysShareBackup 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'sshkeys-backup'
  properties: {
    shareQuota: 1
  }
}

resource scriptsShareBackup 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'scripts-backup'
  properties: {
    shareQuota: 1
  }
}

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-11-02-preview' = {
  name: containerAppsEnvName
  location: location
  tags: tags
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: vnet.properties.subnets[0].id
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// Service Bus for Salesforce Integration (Use Case 2)
module serviceBus 'modules/service-bus.bicep' = if (enableSalesforceIntegration) {
  name: 'servicebus-deployment'
  params: {
    nameSuffix: baseName
    location: location
    environment: environment
    skuName: 'Standard'
    logAnalyticsWorkspaceId: logAnalytics.id
  }
}

// Redis Cache for Network Rail CIF Processing (Use Case 3)
module redisCache 'modules/redis-cache.bicep' = if (enableNetworkRailIntegration) {
  name: 'redis-cache-deployment'
  params: {
    nameSuffix: baseName
    location: location
    environment: environment
    skuName: 'Balanced_B1'
  }
}

// Primary SFTP Server
module sftpPrimary 'modules/sftp-server.bicep' = {
  name: 'sftp-primary-deployment'
  params: {
    nameSuffix: 'rne-primary'
    location: location
    environment: environment
    sftpUsername: sftpUsername
    sftpPassword: sftpPassword
    containerAppsEnvironmentId: containerAppsEnvironment.id
    storageAccountName: storageAccount.name
    storageAccountKey: storageAccount.listKeys().keys[0].value
    allowedIpRanges: allowedSftpIpRanges
  }
}

// Backup SFTP Server
module sftpBackup 'modules/sftp-server.bicep' = {
  name: 'sftp-backup-deployment'
  params: {
    nameSuffix: 'rne-backup'
    location: location
    environment: environment
    sftpUsername: sftpUsername
    sftpPassword: sftpPassword
    containerAppsEnvironmentId: containerAppsEnvironment.id
    storageAccountName: storageAccount.name
    storageAccountKey: storageAccount.listKeys().keys[0].value
    allowedIpRanges: allowedSftpIpRanges
    externalPort: 2222
  }
}

// Mock Server for OpsAPI, Salesforce, and Network Rail endpoints
module mockServer 'modules/mock-server.bicep' = {
  name: 'mock-server-deployment'
  params: {
    nameSuffix: 'transgrid-mock'
    location: location
    environment: environment
    containerAppsEnvironmentId: containerAppsEnvironment.id
    containerImage: 'mcr.microsoft.com/dotnet/aspnet:9.0'
    targetPort: 8080
    cpuCores: '0.5'
    memoryGb: '1'
    functionUrl: functionApp.outputs.functionEndpoint
    functionKey: '' // Function key will be set by deploy-mockserver.ps1 after function deployment
    serviceBusConnectionString: enableSalesforceIntegration ? serviceBus.outputs.sendConnectionString : ''
    serviceBusQueueName: enableSalesforceIntegration ? serviceBus.outputs.queueName : ''
  }
}

// Azure Function for transformation
module functionApp 'modules/function-app.bicep' = {
  name: 'function-app-deployment'
  params: {
    functionAppName: '${baseName}-transform'
    location: location
    environment: environment
    storageAccountName: storageAccount.name
    storageConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
    appInsightsInstrumentationKey: appInsights.properties.InstrumentationKey
    appInsightsConnectionString: appInsights.properties.ConnectionString
    opsApiEndpoint: opsApiEndpoint
  }
}

// Logic App Standard
module logicApp 'modules/logic-app.bicep' = {
  name: 'logic-app-deployment'
  params: {
    logicAppName: '${baseName}-rne-export'
    location: location
    environment: environment
    storageAccountName: storageAccount.name
    storageConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
    appInsightsInstrumentationKey: appInsights.properties.InstrumentationKey
    appInsightsConnectionString: appInsights.properties.ConnectionString
    blobConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
    tableConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
    primarySftpEndpoint: sftpPrimary.outputs.sftpEndpoint
    backupSftpEndpoint: sftpBackup.outputs.sftpEndpoint
    primarySftpHost: sftpPrimary.outputs.containerAppFqdn
    backupSftpHost: sftpBackup.outputs.containerAppFqdn
    sftpUsername: sftpUsername
    sftpPassword: sftpPassword
    functionEndpoint: functionApp.outputs.functionEndpoint
    opsApiEndpoint: mockServer.outputs.mockServerEndpoint
    serviceBusConnectionString: enableSalesforceIntegration ? serviceBus.outputs.listenConnectionString : ''
    serviceBusQueueName: enableSalesforceIntegration ? serviceBus.outputs.queueName : ''
    salesforceApiEndpoint: mockServer.outputs.mockServerEndpoint
  }
}

// Outputs
output resourceGroupName string = resourceGroup().name
output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output blobContainerName string = rneExportContainer.name
output tableStorageName string = failedExportsTable.name

output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerAppsEnvironmentName string = containerAppsEnvironment.name

output primarySftpEndpoint string = sftpPrimary.outputs.sftpEndpoint
output primarySftpFqdn string = sftpPrimary.outputs.containerAppFqdn
output backupSftpEndpoint string = sftpBackup.outputs.sftpEndpoint
output backupSftpFqdn string = sftpBackup.outputs.containerAppFqdn

output functionAppName string = functionApp.outputs.functionAppName
output functionEndpoint string = functionApp.outputs.functionEndpoint

output mockServerName string = mockServer.outputs.containerAppName
output mockServerEndpoint string = mockServer.outputs.mockServerEndpoint
output mockServerFqdn string = mockServer.outputs.containerAppFqdn

output logicAppName string = logicApp.outputs.logicAppName
output logicAppHostname string = logicApp.outputs.logicAppDefaultHostname

output appInsightsName string = appInsights.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey

// Service Bus outputs (for Salesforce integration)
output serviceBusNamespaceName string = enableSalesforceIntegration ? serviceBus.outputs.serviceBusNamespaceName : ''
output serviceBusQueueName string = enableSalesforceIntegration ? serviceBus.outputs.queueName : ''
output serviceBusNamespaceFqdn string = enableSalesforceIntegration ? serviceBus.outputs.serviceBusNamespaceFqdn : ''

// Salesforce storage containers
output salesforceInternalContainer string = salesforceInternalContainer.name
output salesforceExternalContainer string = salesforceExternalContainer.name

// Network Rail CIF Processing outputs (Use Case 3)
output cifArchiveContainer string = cifArchiveContainer.name
output redisCacheName string = enableNetworkRailIntegration ? redisCache.outputs.redisCacheName : ''
output redisCacheHostName string = enableNetworkRailIntegration ? redisCache.outputs.redisCacheHostName : ''
