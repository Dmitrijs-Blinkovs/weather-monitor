// All Weather Monitor resources in one resource group. Design notes: docs/deployment.md, docs/security.md and
// docs/adr/0011 + 0015.
// - No passwords or keys: the apps authenticate with user-assigned managed identities, one per app, least privilege.
// - The OpenWeatherMap key lives in Key Vault; the function's app setting holds only a Key Vault reference.

param location string
param tags object

@minLength(13)
@maxLength(13)
@description('uniqueString() of subscription, environment and location; keeps resource names globally unique.')
param resourceToken string

@secure()
param openWeatherMapApiKey string

param webSkuName string

@description('Optional e-mail address that receives the "no weather data" alert.')
param alertEmail string = ''

var sqlDatabaseName = 'weatherdb'
var deploymentContainerName = 'app-package'
var isFreeWebPlan = webSkuName == 'F1'
var webAppName = 'app-${resourceToken}'

// Built-in role definition IDs.
var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
}

// ---------------------------------------------------------------------------------------------------------------
// Identities: one per app, so a compromise of the public web app cannot write data, read the API key or replace
// the function's code.
// ---------------------------------------------------------------------------------------------------------------
resource functionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-func-${resourceToken}'
  location: location
  tags: tags
}

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-web-${resourceToken}'
  location: location
  tags: tags
}

// ---------------------------------------------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
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

resource alertActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmail)) {
  name: 'ag-${resourceToken}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'weather'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// Fires when no city has been fetched successfully for 15 minutes (expired key, broken timer, provider outage).
// Based on the weather.fetch.attempts metric the function exports with OpenTelemetry.
resource noWeatherDataAlert 'Microsoft.Insights/scheduledQueryRules@2026-03-01' = {
  name: 'alert-no-weather-data-${resourceToken}'
  location: location
  tags: tags
  properties: {
    displayName: 'Weather Monitor: a city without a successful weather fetch for 15 minutes'
    severity: 1
    enabled: true
    scopes: [logAnalytics.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      allOf: [
        {
          // A row per city that was attempted but never succeeded in the window, plus one row when nothing succeeded
          // at all (also when no attempt was recorded, e.g. a stopped timer).
          query: '''
            let attempts = AppMetrics
              | where Name == "weather.fetch.attempts"
              | extend City = tostring(Properties["city"]), Outcome = tostring(Properties["outcome"]);
            let silentCities = attempts
              | summarize Successes = sumif(Sum, Outcome == "success") by City
              | where Successes == 0;
            let nothingStored = attempts
              | summarize Successes = sumif(Sum, Outcome == "success")
              | where Successes == 0
              | extend City = "(all cities)";
            union silentCities, nothingStored
            '''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: empty(alertEmail) ? [] : [alertActionGroup.id]
    }
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Key Vault with the OpenWeatherMap API key
// ---------------------------------------------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = {
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

// Who read the secret, and when (Key Vault audit log).
// The only GA version (2016-09-01) has no categoryGroup ('audit'); the preview is the documented way to collect audit
// logs by category group, so the API-age rule is suppressed here on purpose.
#disable-next-line use-recent-api-versions
resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'audit-to-log-analytics'
  scope: keyVault
  properties: {
    workspaceId: logAnalytics.id
    logs: [{ categoryGroup: 'audit', enabled: true }]
  }
}

// Only the function reads the key.
resource functionKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionIdentity.id, roles.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Azure SQL: Entra-only authentication. The function identity is the Entra administrator so it can apply the EF Core
// migrations on start; it then grants the web identity db_datareader (Database__ReadOnlyIdentity settings below).
// ---------------------------------------------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2025-01-01' = {
  name: 'sql-${resourceToken}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: functionIdentity.name
      sid: functionIdentity.properties.principalId
      tenantId: subscription().tenantId
      principalType: 'Application'
    }
  }

  // Lets Azure services (the web and function apps) reach the server; no public client IPs are allowed.
  // Note: this admits any Azure-hosted client network-wise (Entra authentication still applies); private endpoints
  // remove that exposure - see "Hardening options" in docs/deployment.md.
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

  // Server-level auditing (default groups: successful/failed logins and completed batches) to Log Analytics.
  resource auditing 'auditingSettings' = {
    name: 'default'
    properties: {
      state: 'Enabled'
      isAzureMonitorTargetEnabled: true
    }
  }
}

// SQL audit records reach Log Analytics through a diagnostic setting on the master database.
resource sqlMasterDatabase 'Microsoft.Sql/servers/databases@2025-01-01' existing = {
  parent: sqlServer
  name: 'master'
}

// The only GA version (2016-09-01) has no categoryGroup ('audit'); the preview is the documented way to collect audit
// logs by category group, so the API-age rule is suppressed here on purpose.
#disable-next-line use-recent-api-versions
resource sqlAuditDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'audit-to-log-analytics'
  scope: sqlMasterDatabase
  properties: {
    workspaceId: logAnalytics.id
    logs: [{ category: 'SQLSecurityAuditEvents', enabled: true }]
  }
}

// Each app appends ";User Id=<its identity's client ID>".
var sqlConnectionStringBase = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=30'

var apiKeyReference = '@Microsoft.KeyVault(SecretUri=${keyVault::apiKey.properties.secretUri})'

// ---------------------------------------------------------------------------------------------------------------
// Storage for the Functions host (timer leases) and Flex Consumption deployments; shared keys are disabled.
// ---------------------------------------------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2025-06-01' = {
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
    allowCrossTenantReplication: false
    defaultToOAuthAuthentication: true
  }

  resource blobServices 'blobServices' = {
    name: 'default'

    resource deployments 'containers' = {
      name: deploymentContainerName
    }
  }
}

// Timer triggers and Flex deployments use blobs only, so the function needs no queue or table roles.
resource functionStorageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionIdentity.id, roles.storageBlobDataOwner)
  scope: storage
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataOwner)
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Function app (Flex Consumption, .NET 10 isolated worker)
// ---------------------------------------------------------------------------------------------------------------
resource functionPlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: 'plan-func-${resourceToken}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { tier: 'FlexConsumption', name: 'FC1' }
  properties: { reserved: true }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: 'func-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'functions' })
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${functionIdentity.id}': {} }
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    keyVaultReferenceIdentity: functionIdentity.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: functionIdentity.id
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
      ftpsState: 'Disabled'
      // Timer-only app: nothing needs to call it, so inbound HTTP (including the admin API) is closed.
      ipSecurityRestrictionsDefaultAction: 'Deny'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: functionIdentity.properties.clientId }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ConnectionStrings__WeatherDb', value: '${sqlConnectionStringBase};User Id=${functionIdentity.properties.clientId}' }
        { name: 'Database__ApplyMigrationsOnStartup', value: 'true' }
        { name: 'Database__ReadOnlyIdentity__Name', value: webIdentity.name }
        { name: 'Database__ReadOnlyIdentity__ClientId', value: webIdentity.properties.clientId }
        { name: 'OpenWeatherMap__ApiKey', value: apiKeyReference }
      ]
    }
  }
  dependsOn: [
    functionStorageBlobOwner
    functionKeyVaultSecretsUser
  ]
}

// ---------------------------------------------------------------------------------------------------------------
// Web app (App Service, Linux): MVC API + React build in wwwroot; read-only database access
// ---------------------------------------------------------------------------------------------------------------
resource webPlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: 'plan-web-${resourceToken}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: webSkuName }
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${webIdentity.id}': {} }
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    clientAffinityEnabled: false // stateless app: no sticky-session cookie
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: !isFreeWebPlan
      // Liveness only: a database outage must not make App Service recycle healthy instances.
      healthCheckPath: isFreeWebPlan ? null : '/health/live'
      minTlsVersion: '1.3'
      ftpsState: 'Disabled'
      http20Enabled: true
      appSettings: [
        { name: 'ConnectionStrings__WeatherDb', value: '${sqlConnectionStringBase};User Id=${webIdentity.properties.clientId}' }
        { name: 'Database__ApplyMigrationsOnStartup', value: 'false' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        // Client IPs for the rate limiter come from X-Forwarded-For set by the App Service front end.
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        // Host header filtering; add custom domains here (semicolon separated).
        { name: 'AllowedHosts', value: '${webAppName}.azurewebsites.net' }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
      ]
    }
  }
}

// Deployments authenticate with Entra ID (azd); username/password publishing (FTP and SCM basic auth) is disabled.
resource basicAuthPolicies 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = [
  for policy in [
    { site: webApp.name, name: 'ftp' }
    { site: webApp.name, name: 'scm' }
    { site: functionApp.name, name: 'ftp' }
    { site: functionApp.name, name: 'scm' }
  ]: {
    name: '${policy.site}/${policy.name}'
    properties: { allow: false }
  }
]

output webUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
output functionAppName string = functionApp.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabaseName
output keyVaultName string = keyVault.name
