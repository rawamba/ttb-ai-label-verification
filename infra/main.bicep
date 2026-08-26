// Subscription-level infrastructure deployment for the
// TTB Label Verification prototype.
//
// The prototype infrastructure is defined declaratively so the Azure
// environment can be reproduced without manual portal configuration.

targetScope = 'subscription'

@description('Azure region used for prototype resources.')
param location string = 'eastus2'

@description('Resource group containing prototype resources.')
param resourceGroupName string = 'rg-ttb-label-verification-prototype'

@description('Base application name used when constructing resource names.')
param applicationName string = 'ttb-label-verification'

@description('App Service Plan SKU.')
param appServiceSku string = 'B1'

@description('Tags applied to prototype resources.')
param tags object = {
  application: 'TTB Label Verification'
  environment: 'prototype'
  managedBy: 'Bicep'
}

// -----------------------------------------------------------------------------
// Resource naming
// -----------------------------------------------------------------------------

// Generate a deterministic suffix so globally unique Azure resources retain
// stable names across repeated deployments to the same subscription.
var uniqueSuffix = uniqueString(subscription().id, resourceGroupName)

var webAppName = '${applicationName}-${uniqueSuffix}'
var appServicePlanName = 'asp-${applicationName}-prototype'
var documentIntelligenceName = 'docintel-${applicationName}-${uniqueSuffix}'

// -----------------------------------------------------------------------------
// Resource Group
// -----------------------------------------------------------------------------

resource prototypeResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// -----------------------------------------------------------------------------
// Azure Document Intelligence
// -----------------------------------------------------------------------------

// Primary OCR service used to extract textual evidence from validated
// alcohol label images.
module documentIntelligence 'modules/document-intelligence.bicep' = {
  name: 'deploy-document-intelligence'
  scope: prototypeResourceGroup

  params: {
    location: location
    accountName: documentIntelligenceName
    tags: tags
  }
}

// -----------------------------------------------------------------------------
// Azure App Service
// -----------------------------------------------------------------------------

// Deploy the evaluator-facing Web App.
//
// The Document Intelligence endpoint is injected as application configuration.
// Authentication uses the Web App's system-assigned managed identity rather
// than a Cognitive Services API key.
module appService 'modules/app-service.bicep' = {
  name: 'deploy-app-service'
  scope: prototypeResourceGroup

  params: {
    location: location
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    sku: appServiceSku
    tags: tags
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
  }
}

// -----------------------------------------------------------------------------
// Document Intelligence RBAC
// -----------------------------------------------------------------------------

// Grant the Web App managed identity Cognitive Services data-plane access.
module documentIntelligenceAccess 'modules/cognitive-services-rbac.bicep' = {
  name: 'grant-document-intelligence-access'
  scope: prototypeResourceGroup

  params: {
    cognitiveServicesAccountName: documentIntelligence.outputs.accountName
    principalId: appService.outputs.webAppPrincipalId
  }
}

// -----------------------------------------------------------------------------
// Outputs
// -----------------------------------------------------------------------------

@description('Prototype resource group name.')
output resourceGroupName string = prototypeResourceGroup.name

@description('Deployed Web App name.')
output webAppName string = appService.outputs.webAppName

@description('Public prototype Web App URL.')
output webAppUrl string = appService.outputs.webAppUrl

@description('Azure Document Intelligence resource name.')
output documentIntelligenceName string = documentIntelligence.outputs.accountName

@description('Azure Document Intelligence endpoint.')
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint