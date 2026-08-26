// RBAC configuration for Azure Cognitive Services.
//
// Grants the Web App's system-assigned managed identity data-plane access
// to Azure Document Intelligence without storing service credentials.

@description('Name of the Cognitive Services account receiving the role assignment.')
param cognitiveServicesAccountName string

@description('Microsoft Entra principal ID of the Web App managed identity.')
param principalId string

// Built-in Cognitive Services User role.
//
// Role definition ID:
// a97b65f3-24c7-4388-baec-2e87135dc908
//
// This role includes Cognitive Services data-plane permissions required
// by the application to call Document Intelligence using Microsoft Entra ID.
var cognitiveServicesUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'a97b65f3-24c7-4388-baec-2e87135dc908'
)

// Reference the Document Intelligence account created by the sibling module.
resource cognitiveServicesAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: cognitiveServicesAccountName
}

// Assign Cognitive Services User to the Web App managed identity.
//
// guid(...) produces a deterministic role-assignment name, making repeated
// Bicep deployments idempotent.
resource cognitiveServicesUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(
    cognitiveServicesAccount.id,
    principalId,
    cognitiveServicesUserRoleDefinitionId
  )

  scope: cognitiveServicesAccount

  properties: {
    roleDefinitionId: cognitiveServicesUserRoleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}