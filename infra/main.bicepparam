using 'main.bicep'

// Environment configuration
param environment = 'dev'
param location = 'westeurope'
param baseName = 'transgrid'

// SFTP Configuration
param sftpUsername = 'rneuser'
param sftpPassword = '' // Set via command line: --parameters sftpPassword='your-secure-password'

// IP Restrictions for SFTP (add your allowed IPs)
// Example: ['10.0.0.0/8', '192.168.1.0/24']
param allowedSftpIpRanges = []

// Operations API (Mock Server) endpoint
// For local development, use the mock server URL
// For production, use the actual Operations GraphQL API
param opsApiEndpoint = 'http://localhost:5000/graphql'
