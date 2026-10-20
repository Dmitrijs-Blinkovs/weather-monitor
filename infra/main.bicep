// Entry point for `azd provision` / `azd up`: creates the resource group and everything in it (resources.bicep).
targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the azd environment, e.g. "weather-prod". Names the resource group and tags every resource.')
param environmentName string

@minLength(1)
@description('Azure region for all resources, e.g. "northeurope" or "swedencentral".')
param location string

@secure()
@minLength(1)
@description('OpenWeatherMap API key. Stored in Key Vault; app settings only hold a Key Vault reference.')
param openWeatherMapApiKey string

@allowed(['F1', 'B1', 'B2', 'S1', 'P0v3'])
@description('App Service plan of the web app. F1 is free but unloads when idle and has no health check; B1 is the recommended minimum.')
param webSkuName string = 'B1'

@description('Optional e-mail address for the "no weather data for 15 minutes" alert.')
param alertEmail string = ''

var tags = {
  'azd-env-name': environmentName
  application: 'weather-monitor'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'weather-monitor-resources'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    resourceToken: toLower(uniqueString(subscription().id, environmentName, location))
    openWeatherMapApiKey: openWeatherMapApiKey
    webSkuName: webSkuName
    alertEmail: alertEmail
  }
}

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = resourceGroup.name
output WEB_URL string = resources.outputs.webUrl
output WEB_APP_NAME string = resources.outputs.webAppName
output FUNCTION_APP_NAME string = resources.outputs.functionAppName
output SQL_SERVER_NAME string = resources.outputs.sqlServerName
output SQL_DATABASE_NAME string = resources.outputs.sqlDatabaseName
output KEY_VAULT_NAME string = resources.outputs.keyVaultName
