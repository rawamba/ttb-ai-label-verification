// Subscription-level deployment for the TTB Label Verification prototype.
//
// This template creates the prototype resource group and delegates the
// App Service resources to a resource-group-scoped module. Keeping the
// environment declarative makes Azure infrastructure reproducible across
// developer and CI/CD environments.

targetScope = 'subscription'

@description('Azure region used for prototype resources.')
param location string = 'eastus2'

@description('Resource group containing the prototype application resources.')
param resourceGroupName string = 'rg-ttb-label-verification-prototype'

@description('Base name used when constructing Azure resource names.')
param applicationName string = 'ttb-label-verification'

@description('App Service Plan SKU. B1 provides dedicated compute for the prototype.')
param appServiceSku string = 'B1'

@description('Tags applied to prototype resources.')
param tags object = {
  application: 'TTB Label Verification'
  environment: 'prototype'
  managedBy: 'Bicep'
}

// App Service names must be globally unique because they form part of the
// public *.azurewebsites.net hostname. A deterministic suffix avoids
// hard-coding an account- or developer-specific value.
var uniqueSuffix = uniqueString(subscription().id, resourceGroupName)
var webAppName = '${applicationName}-${uniqueSuffix}'
var appServicePlanName = 'asp-${applicationName}-prototype'

// The resource group itself is managed as infrastructure as code rather
// than being manually created in the Azure Portal.
resource prototypeResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// Deploy application-hosting resources into the prototype resource group.
module appService 'modules/app-service.bicep' = {
  name: 'deploy-app-service'
  scope: prototypeResourceGroup
  params: {
    location: location
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    sku: appServiceSku
    tags: tags
  }
}

output resourceGroupName string = prototypeResourceGroup.name
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl