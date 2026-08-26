// App Service resources for the TTB Label Verification prototype.
//
// The application currently targets net8.0 and is hosted as a Linux
// Azure App Service. Application deployment is handled separately by
// Azure Pipelines so infrastructure provisioning remains independent
// from application release operations.
//
// The Web App uses a system-assigned managed identity to authenticate
// to Azure services such as Document Intelligence without storing
// service credentials or API keys in application configuration.

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

@description('Document Intelligence endpoint used by the application.')
param documentIntelligenceEndpoint string

// -----------------------------------------------------------------------------
// App Service Plan
// -----------------------------------------------------------------------------
//
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

// -----------------------------------------------------------------------------
// Web App
// -----------------------------------------------------------------------------
//
// Public evaluator-facing prototype.
//
// The deployed application targets net8.0 even though the repository uses
// the .NET 10 SDK as its build toolchain.
//
// A system-assigned managed identity allows DefaultAzureCredential to
// authenticate to Azure Document Intelligence through Microsoft Entra ID
// rather than through a stored API key.
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: tags

  identity: {
    type: 'SystemAssigned'
  }

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

      // ASP.NET Core automatically maps environment-variable names using
      // double underscores to hierarchical configuration sections.
      //
      // For example:
      // DocumentIntelligence__Endpoint
      //
      // becomes:
      // configuration["DocumentIntelligence:Endpoint"]
      //
      // No API key is stored here because authentication is performed using
      // the Web App's managed identity.
      appSettings: [
        {
          name: 'DocumentIntelligence__Endpoint'
          value: documentIntelligenceEndpoint
        }
        {
          name: 'DocumentIntelligence__ModelId'
          value: 'prebuilt-read'
        }
        {
          name: 'DocumentIntelligence__TimeoutSeconds'
          value: '5'
        }
      ]
    }
  }
}

// -----------------------------------------------------------------------------
// Outputs
// -----------------------------------------------------------------------------

@description('Name of the deployed Azure Web App.')
output webAppName string = webApp.name

@description('Public HTTPS URL of the deployed Azure Web App.')
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'

@description('Microsoft Entra principal ID of the Web App managed identity.')
output webAppPrincipalId string = webApp.identity.principalId