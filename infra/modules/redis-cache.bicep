// Azure Managed Redis Module
// Used for caching reference data in the Network Rail CIF processing use case
// Note: Azure Cache for Redis is being retired - migrated to Azure Managed Redis (Microsoft.Cache/redisEnterprise)

@description('Name suffix for the Managed Redis cluster')
param nameSuffix string

@description('Location for the Managed Redis cluster')
param location string = resourceGroup().location

@description('Environment name')
param environment string = 'dev'

@description('SKU name for the Managed Redis cluster')
@allowed([
  'Balanced_B0'
  'Balanced_B1'
  'Balanced_B3'
  'Balanced_B5'
  'Balanced_B10'
  'Balanced_B20'
  'Balanced_B50'
  'Balanced_B100'
  'Balanced_B150'
  'Balanced_B250'
  'Balanced_B350'
  'Balanced_B500'
  'Balanced_B700'
  'Balanced_B1000'
  'ComputeOptimized_X3'
  'ComputeOptimized_X5'
  'ComputeOptimized_X10'
  'ComputeOptimized_X20'
  'ComputeOptimized_X50'
  'ComputeOptimized_X100'
  'ComputeOptimized_X150'
  'ComputeOptimized_X250'
  'ComputeOptimized_X350'
  'ComputeOptimized_X500'
  'ComputeOptimized_X700'
  'MemoryOptimized_M10'
  'MemoryOptimized_M20'
  'MemoryOptimized_M50'
  'MemoryOptimized_M100'
  'MemoryOptimized_M150'
  'MemoryOptimized_M250'
  'MemoryOptimized_M350'
  'MemoryOptimized_M500'
  'MemoryOptimized_M700'
  'MemoryOptimized_M1000'
  'MemoryOptimized_M1500'
  'MemoryOptimized_M2000'
])
param skuName string = 'Balanced_B1'

@description('Minimum TLS version')
@allowed(['1.2', '1.3'])
param minimumTlsVersion string = '1.2'

// Variables
var managedRedisName = 'redisenterprise-${nameSuffix}-${environment}'
var databaseName = 'default'

// Tags
var tags = {
  Environment: environment
  Project: 'Transgrid Azure Integration Services'
  ManagedBy: 'Bicep'
  UseCase: 'NetworkRail-CIF-Processing'
}

// Azure Managed Redis (Redis Enterprise)
resource managedRedis 'Microsoft.Cache/redisEnterprise@2024-10-01' = {
  name: managedRedisName
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  properties: {
    minimumTlsVersion: minimumTlsVersion
  }
}

// Redis Enterprise Database
resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2024-10-01' = {
  parent: managedRedis
  name: databaseName
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    clusteringPolicy: 'OSSCluster'
    evictionPolicy: 'AllKeysLRU'
    persistence: {
      aofEnabled: false
      rdbEnabled: false
    }
  }
}

// Outputs
output redisCacheName string = managedRedis.name
output redisCacheId string = managedRedis.id
output redisCacheHostName string = managedRedis.properties.hostName
output redisCachePort int = redisDatabase.properties.port
output redisDatabaseName string = redisDatabase.name
output redisDatabaseId string = redisDatabase.id

// Connection string output (access key)
output redisCacheConnectionString string = '${managedRedis.properties.hostName}:${redisDatabase.properties.port},password=${redisDatabase.listKeys().primaryKey},ssl=True,abortConnect=False'
