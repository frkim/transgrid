// SFTP Server on Azure Container Apps
// Uses atmoz/sftp:alpine image with Azure Files for persistent storage
// Includes SSH key persistence to prevent host key changes on restart

@description('Name suffix for resources')
param nameSuffix string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('SFTP username')
param sftpUsername string

@description('SFTP password')
@secure()
param sftpPassword string

@description('Container Apps Environment ID')
param containerAppsEnvironmentId string

@description('Storage Account name for Azure Files')
param storageAccountName string

@description('Storage Account key')
@secure()
param storageAccountKey string

@description('Allowed IP addresses for SFTP access (CIDR notation)')
param allowedIpRanges array = []

@description('CPU cores for the container')
param cpuCores string = '0.5'

@description('Memory in GB for the container')
param memoryGb string = '1'

// Variables
var containerAppName = 'sftp-${nameSuffix}-${environment}'
var sftpUsersEnvVar = '${sftpUsername}:${sftpPassword}:::upload'

// File shares configuration
var fileShares = [
  {
    name: 'sftpdata-${nameSuffix}'
    shareName: 'sftpdata'
    accessMode: 'ReadWrite'
    mountPath: '/home/${sftpUsername}/upload'
  }
  {
    name: 'sshkeys-${nameSuffix}'
    shareName: 'sshkeys'
    accessMode: 'ReadOnly'
    mountPath: '/etc/sftpkeys'
  }
  {
    name: 'scripts-${nameSuffix}'
    shareName: 'scripts'
    accessMode: 'ReadOnly'
    mountPath: '/etc/sftp.d'
  }
]

// Container App for SFTP
resource sftpContainerApp 'Microsoft.App/containerApps@2023-11-02-preview' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 22
        transport: 'tcp'
        exposedPort: 22
        ipSecurityRestrictions: [for (ip, i) in allowedIpRanges: {
          name: 'allow-rule-${i}'
          description: 'Allow access from ${ip}'
          ipAddressRange: ip
          action: 'Allow'
        }]
      }
      secrets: [
        {
          name: 'sftp-users'
          value: sftpUsersEnvVar
        }
        {
          name: 'storage-key'
          value: storageAccountKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'sftp'
          image: 'atmoz/sftp:alpine'
          resources: {
            cpu: json(cpuCores)
            memory: '${memoryGb}Gi'
          }
          env: [
            {
              name: 'SFTP_USERS'
              secretRef: 'sftp-users'
            }
          ]
          volumeMounts: [for share in fileShares: {
            volumeName: share.name
            mountPath: share.mountPath
          }]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
      volumes: [for share in fileShares: {
        name: share.name
        storageName: share.name
        storageType: 'AzureFile'
      }]
    }
  }
}

// Storage mounts in Container Apps Environment
resource storageMounts 'Microsoft.App/managedEnvironments/storages@2023-11-02-preview' = [for share in fileShares: {
  name: share.name
  parent: containerAppsEnvironment
  properties: {
    azureFile: {
      accountName: storageAccountName
      accountKey: storageAccountKey
      shareName: share.shareName
      accessMode: share.accessMode
    }
  }
}]

// Reference to existing Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: last(split(containerAppsEnvironmentId, '/'))
}

// Outputs
output containerAppId string = sftpContainerApp.id
output containerAppName string = sftpContainerApp.name
output containerAppFqdn string = sftpContainerApp.properties.configuration.ingress.fqdn
output sftpEndpoint string = '${sftpContainerApp.properties.configuration.ingress.fqdn}:22'
