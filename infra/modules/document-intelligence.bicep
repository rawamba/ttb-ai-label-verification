// Azure Document Intelligence resource for the TTB Label Verification prototype.
//
// The service provides the primary OCR capability used to extract textual
// evidence from validated label images.
//
// Microsoft Entra authentication is used instead of API keys. A custom
// subdomain is therefore configured so managed identity / token-based
// authentication can be used by the Web App.

@description('Azure region for the Document Intelligence resource.')
param location string

@description('Globally unique Document Intelligence resource name.')
param accountName string

@description('Resource tags.')
param tags object = {}

// -----------------------------------------------------------------------------
// Azure Document Intelligence
// -----------------------------------------------------------------------------
//
// Document Intelligence is provisioned through the Cognitive Services
// resource provider using the FormRecognizer kind.
resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  kind: 'FormRecognizer'
  tags: tags

  sku: {
    name: 'S0'
  }

  properties: {
    // Required for Microsoft Entra token-based authentication.
    customSubDomainName: accountName

    // Disable local API-key authentication so the prototype uses
    // managed identity / Microsoft Entra authentication exclusively.
    disableLocalAuth: true

    // Public access is required for this evaluator-facing prototype.
    // Production networking can later use private endpoints as appropriate.
    publicNetworkAccess: 'Enabled'
  }
}

// -----------------------------------------------------------------------------
// Outputs
// -----------------------------------------------------------------------------

@description('Document Intelligence resource name.')
output accountName string = documentIntelligence.name

@description('Azure resource ID of the Document Intelligence account.')
output accountId string = documentIntelligence.id

@description('Custom-subdomain endpoint used by the application.')
output endpoint string = 'https://${documentIntelligence.name}.cognitiveservices.azure.com/'