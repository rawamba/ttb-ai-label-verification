using './main.bicep'

param location = 'eastus2'

param resourceGroupName = 'rg-ttb-label-verification-prototype'

param applicationName = 'ttb-label-verification'

param appServiceSku = 'B1'

param tags = {
  application: 'TTB Label Verification'
  environment: 'prototype'
  managedBy: 'Bicep'
  workload: 'AI Label Verification'
}