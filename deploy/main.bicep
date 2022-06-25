param location string = 'eastus'

@description('The name of the containter instance that you wish to create.')
param clamavname string = 'aciclamav-${uniqueString(resourceGroup().id)}'

@description('Blob for quarantine')
param quarantineBlob string = 'quarantineblob'
@description('Blob that has been scanned and clean')
param cleanBlob string = 'cleanblob'

@description('Blob for uploading. Note : Please change the function trigger if you change the name here.')
param uploadBlob string = 'uploadblob'

module aci 'modules/aci.bicep' = {
  name: 'aciclamav${uniqueString(resourceGroup().id)}'
  params:{
    name: clamavname
    location: location
    image: 'clamav/clamav:latest_base'
    port: 3310
    cpuCores : 1
    memoryInGb : 2
  }
}

module storage 'modules/blob.bicep' = {
  name: 'storagedeploy-${uniqueString(resourceGroup().id)}'
  params: {
    location: location
    storageAccountName: 'betablob${uniqueString(resourceGroup().id)}'
    quarantineContainerName: quarantineBlob
    cleanContainerName: cleanBlob
    uploadContainerName: uploadBlob
  }
}

module function 'modules/function.bicep' = {
  name: 'functiondeploy-${uniqueString(resourceGroup().id)}'
  params:{
    location: location
    clamavfqdn: '${clamavname}.${location}.azurecontainer.io'
    clamavport: '3310'
    uploadblob: uploadBlob
    cleanblob: cleanBlob
    quarantineblob: quarantineBlob
    storageAccountType: 'Standard_LRS'
    appInsightsLocation: location
  }
}
