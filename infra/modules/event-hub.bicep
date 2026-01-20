// Event Hub Module
// Deploys an Azure Event Hub Namespace with Event Hubs for Salesforce integration

@description('Name suffix for Event Hub Namespace')
param nameSuffix string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Event Hub Namespace SKU')
@allowed(['Basic', 'Standard', 'Premium'])
param skuName string = 'Standard'

@description('Event Hub Namespace capacity (throughput units)')
@minValue(1)
@maxValue(20)
param skuCapacity int = 1

@description('Log Analytics Workspace ID for diagnostics')
param logAnalyticsWorkspaceId string = ''

// Variables
var eventHubNamespaceName = 'evhns-${nameSuffix}-${environment}'

// Tags for all resources
var tags = {
  Environment: environment
  Project: 'Transgrid Salesforce Integration'
  Component: 'Event Hub'
  ManagedBy: 'Bicep'
}

// Event Hub Namespace
resource eventHubNamespace 'Microsoft.EventHub/namespaces@2024-01-01' = {
  name: eventHubNamespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
    capacity: skuCapacity
  }
  properties: {
    isAutoInflateEnabled: skuName != 'Basic'
    maximumThroughputUnits: skuName != 'Basic' ? 4 : 0
    kafkaEnabled: skuName != 'Basic'
    zoneRedundant: false
    disableLocalAuth: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Event Hub for Salesforce Negotiated Rates events
resource salesforceEventsHub 'Microsoft.EventHub/namespaces/eventhubs@2024-01-01' = {
  parent: eventHubNamespace
  name: 'salesforce-negotiated-rates'
  properties: {
    messageRetentionInDays: 1
    partitionCount: 2
    status: 'Active'
  }
}

// Consumer group for Logic Apps
resource logicAppsConsumerGroup 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2024-01-01' = {
  parent: salesforceEventsHub
  name: 'logicapps-consumer'
  properties: {
    userMetadata: 'Consumer group for Logic Apps workflow processing'
  }
}

// Consumer group for monitoring
resource monitoringConsumerGroup 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2024-01-01' = {
  parent: salesforceEventsHub
  name: 'monitoring-consumer'
  properties: {
    userMetadata: 'Consumer group for monitoring and debugging'
  }
}

// Authorization rule for sending events (Salesforce/Mock Server)
resource sendAuthRule 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2024-01-01' = {
  parent: salesforceEventsHub
  name: 'SendPolicy'
  properties: {
    rights: [
      'Send'
    ]
  }
}

// Authorization rule for listening to events (Logic Apps)
resource listenAuthRule 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2024-01-01' = {
  parent: salesforceEventsHub
  name: 'ListenPolicy'
  properties: {
    rights: [
      'Listen'
    ]
  }
}

// Authorization rule with full access (for admin/deployment)
resource manageAuthRule 'Microsoft.EventHub/namespaces/authorizationRules@2024-01-01' = {
  parent: eventHubNamespace
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
  name: '${eventHubNamespaceName}-diagnostics'
  scope: eventHubNamespace
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
output eventHubNamespaceId string = eventHubNamespace.id
output eventHubNamespaceName string = eventHubNamespace.name
output eventHubName string = salesforceEventsHub.name
output eventHubNamespaceFqdn string = '${eventHubNamespace.name}.servicebus.windows.net'

// Connection strings (these are retrieved at deployment time)
output sendConnectionString string = sendAuthRule.listKeys().primaryConnectionString
output listenConnectionString string = listenAuthRule.listKeys().primaryConnectionString
output namespaceConnectionString string = manageAuthRule.listKeys().primaryConnectionString

// Keys for app settings
output sendPrimaryKey string = sendAuthRule.listKeys().primaryKey
output listenPrimaryKey string = listenAuthRule.listKeys().primaryKey
