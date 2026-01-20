// Service Bus Module
// Deploys an Azure Service Bus Namespace with Queues for Salesforce integration

@description('Name suffix for Service Bus Namespace')
param nameSuffix string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Service Bus Namespace SKU')
@allowed(['Basic', 'Standard', 'Premium'])
param skuName string = 'Standard'

@description('Log Analytics Workspace ID for diagnostics')
param logAnalyticsWorkspaceId string = ''

// Variables
var serviceBusNamespaceName = 'sbns-${nameSuffix}-${environment}'

// Tags for all resources
var tags = {
  Environment: environment
  Project: 'Transgrid Salesforce Integration'
  Component: 'Service Bus'
  ManagedBy: 'Bicep'
}

// Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: false
  }
}

// Queue for Salesforce Negotiated Rates events
resource salesforceQueue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'salesforce-negotiated-rates'
  properties: {
    lockDuration: 'PT1M'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    maxDeliveryCount: 10
    enablePartitioning: false
    enableExpress: false
  }
}

// Authorization rule for sending messages (Salesforce/Mock Server)
resource sendAuthRule 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2024-01-01' = {
  parent: salesforceQueue
  name: 'SendPolicy'
  properties: {
    rights: [
      'Send'
    ]
  }
}

// Authorization rule for listening to messages (Logic Apps)
resource listenAuthRule 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2024-01-01' = {
  parent: salesforceQueue
  name: 'ListenPolicy'
  properties: {
    rights: [
      'Listen'
    ]
  }
}

// Authorization rule with full access (for admin/deployment)
resource manageAuthRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'ManagePolicy'
  properties: {
    rights: [
      'Listen'
      'Manage'
      'Send'
    ]
  }
}

// Diagnostic settings (if Log Analytics workspace is provided)
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: '${serviceBusNamespaceName}-diagnostics'
  scope: serviceBusNamespace
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// Outputs
output serviceBusNamespaceId string = serviceBusNamespace.id
output serviceBusNamespaceName string = serviceBusNamespace.name
output queueName string = salesforceQueue.name
output serviceBusNamespaceFqdn string = '${serviceBusNamespace.name}.servicebus.windows.net'

// Connection strings (these are retrieved at deployment time)
output sendConnectionString string = sendAuthRule.listKeys().primaryConnectionString
output listenConnectionString string = listenAuthRule.listKeys().primaryConnectionString
output namespaceConnectionString string = manageAuthRule.listKeys().primaryConnectionString

// Keys for app settings
output sendPrimaryKey string = sendAuthRule.listKeys().primaryKey
output listenPrimaryKey string = listenAuthRule.listKeys().primaryKey
