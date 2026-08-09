@description('Environment name (staging|prod)')
param environmentName string = 'staging'

@description('Azure region')
param location string = resourceGroup().location

@secure()
param sqlAdminPassword string

@secure()
@description('JWT signing key (32+ chars)')
param jwtKey string

@description('App Service plan SKU')
param appServiceSku string = 'B1'

@description('Deploy Redis backplane (requires Azure Managed Redis quota; leave false for single-instance staging)')
param deployRedis bool = false

var suffix = uniqueString(resourceGroup().id, environmentName)
var sqlServerName = 'aibrand-sql-${environmentName}-${suffix}'
var sqlDbName = 'AiBrandStudio'
var redisName = 'aibrand-amr-${environmentName}-${suffix}'
var storageName = take('aibrand${environmentName}${suffix}', 24)
var planName = 'aibrand-plan-${environmentName}'
var apiAppName = 'aibrand-api-${environmentName}-${suffix}'
var webAppName = 'aibrand-web-${environmentName}-${suffix}'
var insightsName = 'aibrand-ai-${environmentName}-${suffix}'
var webOrigin = 'https://${webAppName}.azurewebsites.net'
var apiOrigin = 'https://${apiAppName}.azurewebsites.net'

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: insightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'aibrandadmin'
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource sqlFw 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Classic Microsoft.Cache/redis is retired on new subscriptions.
// Optional Azure Managed Redis (redisEnterprise) for multi-instance SignalR.
resource redisEnterprise 'Microsoft.Cache/redisEnterprise@2024-10-01' = if (deployRedis) {
  name: redisName
  location: location
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2024-10-01' = if (deployRedis) {
  parent: redisEnterprise
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    evictionPolicy: 'NoEviction'
    clusteringPolicy: 'OSSCluster'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource assetsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: 'assets'
  properties: {
    publicAccess: 'None'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: appServiceSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

var sqlConnection = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDbName};User ID=aibrandadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
var storageConnection = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var redisConnection = deployRedis
  ? '${redisEnterprise!.properties.hostName}:${redisDatabase!.properties.port},password=${redisDatabase!.listKeys().primaryKey},ssl=true,abortConnect=False'
  : ''

// Free F1 does not support alwaysOn
var apiAlwaysOn = appServiceSku != 'F1' && appServiceSku != 'D1'

var apiAppSettings = concat(
  [
    {
      name: 'ASPNETCORE_ENVIRONMENT'
      value: environmentName == 'prod' ? 'Production' : 'Staging'
    }
    {
      name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
      value: insights.properties.ConnectionString
    }
    {
      name: 'Observability__Enabled'
      value: 'true'
    }
    {
      name: 'Storage__Provider'
      value: 'AzureBlob'
    }
    {
      name: 'Storage__Azure__ConnectionString'
      value: storageConnection
    }
    {
      name: 'Storage__Azure__Container'
      value: 'assets'
    }
    {
      name: 'Jwt__Issuer'
      value: 'AiBrandStudio'
    }
    {
      name: 'Jwt__Audience'
      value: 'AiBrandStudio'
    }
    {
      name: 'Jwt__Key'
      value: jwtKey
    }
    {
      name: 'Cors__Origins'
      value: webOrigin
    }
    {
      name: 'AI__DefaultTextProvider'
      value: 'Mock'
    }
    {
      name: 'AI__DefaultImageProvider'
      value: 'Mock'
    }
    {
      name: 'WEBSITES_PORT'
      value: '8080'
    }
    {
      name: 'ASPNETCORE_URLS'
      value: 'http://0.0.0.0:8080'
    }
  ],
  deployRedis ? [
    {
      name: 'ConnectionStrings__Redis'
      value: redisConnection
    }
  ] : []
)

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: apiAlwaysOn
      ftpsState: 'Disabled'
      webSocketsEnabled: true
      appSettings: apiAppSettings
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnection
          type: 'SQLAzure'
        }
      ]
    }
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|22-lts'
      alwaysOn: false
      ftpsState: 'Disabled'
      appCommandLine: 'node server.js'
      appSettings: [
        {
          name: 'NEXT_PUBLIC_API_URL'
          value: apiOrigin
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: insights.properties.ConnectionString
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'ENABLE_ORYX_BUILD'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'PORT'
          value: '8080'
        }
        {
          name: 'HOSTNAME'
          value: '0.0.0.0'
        }
      ]
    }
  }
  dependsOn: [
    apiApp
  ]
}

output apiAppName string = apiApp.name
output webAppName string = webApp.name
output apiHostName string = apiApp.properties.defaultHostName
output webHostName string = webApp.properties.defaultHostName
output apiUrl string = apiOrigin
output webUrl string = webOrigin
output appInsightsConnectionString string = insights.properties.ConnectionString
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output storageAccountName string = storage.name
output redisHostName string = deployRedis ? redisEnterprise!.properties.hostName : ''
output resourceGroupName string = resourceGroup().name
output deployRedis bool = deployRedis
