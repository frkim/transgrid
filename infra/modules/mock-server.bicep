// Mock Server on Azure Container Apps
// Hosts the OpsAPI, Salesforce, and Network Rail mock endpoints
// Uses .NET ASP.NET Core container

@description('Name suffix for resources')
param nameSuffix string = 'mockserver'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Container Apps Environment ID')
param containerAppsEnvironmentId string

@description('Container Registry name (leave empty for Docker Hub or MCR)')
param containerRegistry string = ''

@description('Container Registry username for authentication')
param containerRegistryUsername string = ''

@description('Container Registry password for authentication')
@secure()
param containerRegistryPassword string = ''

@description('Container image (default uses MCR .NET 9 base image)')
param containerImage string = 'mcr.microsoft.com/dotnet/aspnet:9.0'

@description('CPU cores for the container')
param cpuCores string = '0.5'

@description('Memory in GB for the container')
param memoryGb string = '1'

@description('Target port for the application')
param targetPort int = 8080

@description('Environment variables for the container')
param environmentVariables array = []

@description('Function URL for the TransformTrainPlan function')
param functionUrl string = ''

@description('Function Base URL (Azure Functions base endpoint without /api/...)')
param functionBaseUrl string = ''

@description('Function Key for the TransformTrainPlan function')
@secure()
param functionKey string = ''

@description('Service Bus connection string for sending Salesforce messages')
@secure()
param serviceBusConnectionString string = ''

@description('Service Bus queue name for Salesforce messages')
param serviceBusQueueName string = ''

// Variables
var containerAppName = 'ca-${nameSuffix}-${environment}'

// Build secrets array conditionally (only include secrets with values)
var functionKeySecret = !empty(functionKey) ? [{ name: 'function-key', value: functionKey }] : []
var serviceBusSecret = !empty(serviceBusConnectionString) ? [{ name: 'servicebus-connection', value: serviceBusConnectionString }] : []
var registryPasswordSecret = !empty(containerRegistryPassword) ? [{ name: 'registry-password', value: containerRegistryPassword }] : []
var allSecrets = concat(functionKeySecret, serviceBusSecret, registryPasswordSecret)

// Build environment variables for secrets only when secrets exist
var functionKeyEnv = !empty(functionKey) ? [{ name: 'FunctionDebug__FunctionKey', secretRef: 'function-key' }] : [{ name: 'FunctionDebug__FunctionKey', value: '' }]
var serviceBusEnv = !empty(serviceBusConnectionString) ? [{ name: 'ServiceBus__ConnectionString', secretRef: 'servicebus-connection' }] : [{ name: 'ServiceBus__ConnectionString', value: '' }]

// Container App for Mock Server
resource mockServerContainerApp 'Microsoft.App/containerApps@2023-11-02-preview' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: allSecrets
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'http'
        allowInsecure: false
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          maxAge: 3600
        }
      }
      registries: !empty(containerRegistry) ? [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ] : []
    }
    template: {
      containers: [
        {
          name: 'mock-server'
          image: containerImage
          resources: {
            cpu: json(cpuCores)
            memory: '${memoryGb}Gi'
          }
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment == 'prod' ? 'Production' : 'Development'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:${targetPort}'
            }
            {
              name: 'FunctionDebug__FunctionUrl'
              value: functionUrl
            }
            {
              name: 'FunctionDebug__FunctionBaseUrl'
              value: functionBaseUrl
            }
            {
              name: 'ServiceBus__QueueName'
              value: serviceBusQueueName
            }
          ], functionKeyEnv, serviceBusEnv, environmentVariables)
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

// Outputs
output containerAppId string = mockServerContainerApp.id
output containerAppName string = mockServerContainerApp.name
output containerAppFqdn string = mockServerContainerApp.properties.configuration.ingress.fqdn
output mockServerEndpoint string = 'https://${mockServerContainerApp.properties.configuration.ingress.fqdn}'
