// All Weather Monitor resources in one resource group. Design notes: docs/deployment.md and docs/adr/0011.
// - No secrets or connection strings with passwords: everything authenticates with one user-assigned managed identity.
// - The OpenWeatherMap key lives in Key Vault; app settings hold only a Key Vault reference.

param location string
param tags object

@minLength(13)
@maxLength(13)
@description('uniqueString() of subscription, environment and location; keeps resource names globally unique.')
param resourceToken string

@secure()
param openWeatherMapApiKey string

param webSkuName string

var sqlDatabaseName = 'weatherdb'
var deploymentContainerName = 'app-package'
var isFreeWebPlan = webSkuName == 'F1'

// Built-in role definition IDs.
var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
}

// ---------------------------------------------------------------------------------------------------------------
// Identity shared by the web app and the function app
// ---------------------------------------------------------------------------------------------------------------
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${resourceToken}'
  location: location
  tags: tags
}

// ---------------------------------------------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${resourceToken}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Key Vault with the OpenWeatherMap API key
// ---------------------------------------------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${resourceToken}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }

  resource apiKey 'secrets' = {
    name: 'OpenWeatherMapApiKey'
    properties: { value: openWeatherMapApiKey }
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, roles.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Azure SQL: Entra-only authentication; the apps' identity is the server administrator so that it can apply the
// EF Core migrations on start (trade-off documented in ADR 0011).
// ---------------------------------------------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: 'sql-${resourceToken}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: identity.name
      sid: identity.properties.principalId
      tenantId: subscription().tenantId
      principalType: 'Application'
    }
  }

  // Lets Azure services (the web and function apps) reach the server; no public client IPs are allowed.
  resource allowAzureServices 'firewallRules' = {
    name: 'AllowAllWindowsAzureIps'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  // Basic tier: a database written every minute never auto-pauses, so serverless would bill around the clock.
  resource database 'databases' = {
    name: sqlDatabaseName
    location: location
    tags: tags
    sku: { name: 'Basic', tier: 'Basic' }
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${identity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connect Timeout=30'
var apiKeyReference = '@Microsoft.KeyVault(SecretUri=${keyVault::apiKey.properties.secretUri})'

// ---------------------------------------------------------------------------------------------------------------
// Storage for the Functions host (timer leases) and Flex Consumption deployments; shared keys are disabled.
// ---------------------------------------------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${resourceToken}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
  }

  resource blobServices 'blobServices' = {
    name: 'default'

    resource deployments 'containers' = {
      name: deploymentContainerName
    }
  }
}

resource storageRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for role in [roles.storageBlobDataOwner, roles.storageQueueDataContributor, roles.storageTableDataContributor]: {
    name: guid(storage.id, identity.id, role)
    scope: storage
    properties: {
      principalId: identity.properties.principalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
    }
  }
]

// ---------------------------------------------------------------------------------------------------------------
// Function app (Flex Consumption, .NET 10 isolated worker)
// ---------------------------------------------------------------------------------------------------------------
resource functionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-func-${resourceToken}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { tier: 'FlexConsumption', name: 'FC1' }
  properties: { reserved: true }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'functions' })
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    keyVaultReferenceIdentity: identity.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: identity.id
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 512
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: identity.properties.clientId }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ConnectionStrings__WeatherDb', value: sqlConnectionString }
        { name: 'Database__ApplyMigrationsOnStartup', value: 'true' }
        { name: 'OpenWeatherMap__ApiKey', value: apiKeyReference }
      ]
    }
  }
  dependsOn: [
    storageRoles
    keyVaultSecretsUser
  ]
}

// ---------------------------------------------------------------------------------------------------------------
// Web app (App Service, Linux): MVC API + React build in wwwroot
// ---------------------------------------------------------------------------------------------------------------
resource webPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-web-${resourceToken}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: webSkuName }
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: !isFreeWebPlan
      healthCheckPath: isFreeWebPlan ? null : '/health'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      appSettings: [
        { name: 'ConnectionStrings__WeatherDb', value: sqlConnectionString }
        { name: 'Database__ApplyMigrationsOnStartup', value: 'true' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
      ]
    }
  }
}

output webUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
output functionAppName string = functionApp.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabaseName
output keyVaultName string = keyVault.name
