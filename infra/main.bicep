// Listmate on Azure: Container Apps (consumption) + Postgres Flexible Server, optional Redis for scale-out.
// Optional and manual: `az deployment group create -g <rg> -f infra/main.bicep -p postgresAdminPassword=...`
targetScope = 'resourceGroup'

@description('Prefix for resource names.')
param name string = 'listmate'

param location string = resourceGroup().location

@description('Registry and tag of the images built by `docker compose build` and pushed by you.')
param imageRegistry string = 'ghcr.io/example'
param imageTag string = 'latest'

param postgresAdminLogin string = 'listmateadmin'

@secure()
param postgresAdminPassword string

@description('Azure Cache for Redis as SignalR backplane + shared Data Protection keys, allowing up to 3 API replicas.')
param enableRedis bool = false

module env 'modules/environment.bicep' = {
  name: 'environment'
  params: {
    name: name
    location: location
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${name}-pg-${uniqueString(resourceGroup().id)}'
    location: location
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    databaseName: 'listmate'
  }
}

module redis 'modules/redis.bicep' = if (enableRedis) {
  name: 'redis'
  params: {
    name: '${name}-redis-${uniqueString(resourceGroup().id)}'
    location: location
  }
}

var postgresConnection = 'Host=${postgres.outputs.fqdn};Database=listmate;Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require'
var redisSecrets = enableRedis ? [{ name: 'redis', value: redis!.outputs.connectionString }] : []
var redisEnv = enableRedis ? [{ name: 'ConnectionStrings__Redis', secretRef: 'redis' }] : []

module api 'modules/containerapp.bicep' = {
  name: 'app-api'
  params: {
    name: 'api'
    location: location
    environmentId: env.outputs.environmentId
    image: '${imageRegistry}/listmate-api:${imageTag}'
    targetPort: 8080
    external: false
    maxReplicas: enableRedis ? 3 : 1
    secrets: concat([{ name: 'postgres', value: postgresConnection }], redisSecrets)
    env: concat([{ name: 'ConnectionStrings__Postgres', secretRef: 'postgres' }], redisEnv)
  }
}

module web 'modules/containerapp.bicep' = {
  name: 'app-web'
  params: {
    name: '${name}-web'
    location: location
    environmentId: env.outputs.environmentId
    image: '${imageRegistry}/listmate-web:${imageTag}'
    targetPort: 8080
    external: true
    secrets: []
    env: []
  }
}

output webUrl string = web.outputs.url
