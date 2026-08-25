// App Service resources for the TTB Label Verification prototype.
//
// The application currently targets net8.0 and is hosted as a Linux
// Azure App Service. Application deployment is handled separately by
// Azure Pipelines so infrastructure provisioning remains independent
// from application release operations.

@description('Azure region used for App Service resources.')
param location string

@description('Name of the Linux App Service Plan.')
param appServicePlanName string

@description('Globally unique name of the Web App.')
param webAppName string

@description('App Service Plan SKU.')
param sku string = 'B1'

@description('Tags applied to the App Service resources.')
param tags object = {}

// Dedicated Linux App Service Plan.
//
// reserved=true identifies the App Service Plan as Linux.
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  tags: tags

  sku: {
    name: sku
  }

  properties: {
    reserved: true
  }
}

// Public prototype Web App.
//
// The deployed application targets net8.0 even though the repository uses
// the .NET 10 SDK as its build toolchain.
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: tags

  properties: {
    serverFarmId: appServicePlan.id

    // Require HTTPS for evaluator and operational access.
    httpsOnly: true

    // Public access is intentional for the evaluator-facing prototype.
    publicNetworkAccess: 'Enabled'

    siteConfig: {
      // Run the application using the .NET 8 App Service runtime.
      linuxFxVersion: 'DOTNETCORE|8.0'

      // Keep the prototype warm to reduce avoidable cold-start latency
      // during evaluator demonstrations.
      alwaysOn: true

      // Enable HTTP/2 while continuing to require HTTPS.
      http20Enabled: true

      // Disable legacy FTP/FTPS deployment because releases are performed
      // through Azure Pipelines.
      ftpsState: 'Disabled'

      // Require TLS 1.2 or later.
      minTlsVersion: '1.2'
    }
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'